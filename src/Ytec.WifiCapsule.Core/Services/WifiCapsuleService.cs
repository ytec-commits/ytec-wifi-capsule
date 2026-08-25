using Ytec.WifiCapsule.Core.Models;

namespace Ytec.WifiCapsule.Core.Services;

public sealed class WifiCapsuleService
{
    public const string BackupExtension = ".ywcwifi";

    private readonly IWifiProfileStore _profileStore;
    private readonly Func<byte[]> _keyProvider;
    private readonly Func<DateTimeOffset> _clock;

    public WifiCapsuleService(
        IWifiProfileStore profileStore,
        Func<byte[]> keyProvider,
        Func<DateTimeOffset>? clock = null)
    {
        _profileStore = profileStore
            ?? throw new ArgumentNullException(nameof(profileStore));
        _keyProvider = keyProvider
            ?? throw new ArgumentNullException(nameof(keyProvider));
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public IReadOnlyList<WifiAdapterInfo> GetAdapters() =>
        _profileStore.GetAdapters();

    public IReadOnlyList<WifiStoredProfile> GetProfiles(
        Guid adapterId) =>
        _profileStore.GetProfiles(adapterId);

    public WifiBackupResult CreateBackup(
        Guid adapterId,
        IReadOnlyCollection<string> selectedProfileNames,
        string outputPath)
    {
        if (selectedProfileNames is null)
        {
            throw new ArgumentNullException(
                nameof(selectedProfileNames));
        }

        var selected = selectedProfileNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (selected.Length == 0)
        {
            throw new InvalidOperationException(
                "バックアップするWi-Fi設定を選択してください。");
        }

        var finalPath = ValidateNewOutputPath(outputPath);
        var available = _profileStore
            .GetProfiles(adapterId)
            .ToDictionary(
                profile => profile.Name,
                StringComparer.Ordinal);
        if (selected.Any(name => !available.ContainsKey(name)))
        {
            throw new InvalidOperationException(
                "選択後にWi-Fi設定が変更されました。一覧を更新してください。");
        }

        var documents = new List<WifiProfileDocument>();
        byte[]? encrypted = null;
        var masterKey = _keyProvider();
        try
        {
            foreach (var name in selected)
            {
                var xml = _profileStore.ExportProfile(
                    adapterId,
                    name);
                try
                {
                    var metadata =
                        WifiProfileXmlValidator.Validate(xml);
                    if (!string.Equals(
                            metadata.Name,
                            name,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "Windowsから取得したWi-Fi設定名が一致しません。");
                    }

                    if (metadata.ContainsProtectedKey)
                    {
                        throw new InvalidOperationException(
                            "Wi-Fiキーを平文として取得できませんでした。管理者として起動し直してください。");
                    }

                    documents.Add(
                        new WifiProfileDocument(name, xml));
                }
                finally
                {
                    ZeroMemory(xml);
                }
            }

            encrypted = WifiCapsuleContainer.Encrypt(
                masterKey,
                documents,
                _clock());
            WriteAndVerify(
                finalPath,
                encrypted,
                masterKey,
                selected);
            return new WifiBackupResult(
                documents.Count,
                finalPath,
                new FileInfo(finalPath).Length);
        }
        finally
        {
            foreach (var document in documents)
            {
                document.Dispose();
            }

            ZeroMemory(masterKey);
            if (encrypted is not null)
            {
                ZeroMemory(encrypted);
            }
        }
    }

    public WifiCapsuleDocument OpenBackup(string inputPath)
    {
        var fullPath = ValidateExistingInputPath(inputPath);
        var encrypted = File.ReadAllBytes(fullPath);
        var masterKey = _keyProvider();
        try
        {
            return WifiCapsuleContainer.Decrypt(
                masterKey,
                encrypted);
        }
        finally
        {
            ZeroMemory(masterKey);
            ZeroMemory(encrypted);
        }
    }

    public WifiRestoreResult Restore(
        Guid adapterId,
        WifiCapsuleDocument document,
        IReadOnlyCollection<string> selectedProfileNames,
        bool overwriteExisting)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (selectedProfileNames is null)
        {
            throw new ArgumentNullException(
                nameof(selectedProfileNames));
        }

        var selected = new HashSet<string>(
            selectedProfileNames.Where(
                name => !string.IsNullOrWhiteSpace(name)),
            StringComparer.Ordinal);
        if (selected.Count == 0)
        {
            throw new InvalidOperationException(
                "復元するWi-Fi設定を選択してください。");
        }

        var profiles = document.Profiles
            .Where(profile => selected.Contains(profile.Name))
            .ToArray();
        if (profiles.Length != selected.Count)
        {
            throw new InvalidDataException(
                "選択されたWi-Fi設定がバックアップ内にありません。");
        }

        var currentNames = new HashSet<string>(
            _profileStore
                .GetProfiles(adapterId)
                .Select(profile => profile.Name),
            StringComparer.Ordinal);
        var restored = 0;
        var skipped = 0;
        var failed = 0;
        var errors = new List<string>();
        for (var index = 0; index < profiles.Length; index++)
        {
            var profile = profiles[index];
            if (currentNames.Contains(profile.Name) &&
                !overwriteExisting)
            {
                skipped++;
                continue;
            }

            var xml = profile.GetXmlCopy();
            try
            {
                WifiProfileXmlValidator.Validate(xml);
                _profileStore.ImportProfile(
                    adapterId,
                    xml,
                    overwriteExisting);
                restored++;
            }
            catch (InvalidOperationException)
            {
                failed++;
                errors.Add(
                    $"Wi-Fi設定 {index + 1:N0} を登録できませんでした。");
            }
            catch (InvalidDataException)
            {
                failed++;
                errors.Add(
                    $"Wi-Fi設定 {index + 1:N0} を登録できませんでした。");
            }
            finally
            {
                ZeroMemory(xml);
            }
        }

        return new WifiRestoreResult(
            selected.Count,
            restored,
            skipped,
            failed,
            errors);
    }

    private static void WriteAndVerify(
        string finalPath,
        byte[] encrypted,
        byte[] masterKey,
        IReadOnlyCollection<string> expectedNames)
    {
        var partialPath =
            finalPath + $".partial-{Guid.NewGuid():N}";
        try
        {
            using (var stream = new FileStream(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.WriteThrough))
            {
                stream.Write(
                    encrypted,
                    0,
                    encrypted.Length);
                stream.Flush(flushToDisk: true);
            }

            var verifyBytes = File.ReadAllBytes(partialPath);
            try
            {
                using var verified =
                    WifiCapsuleContainer.Decrypt(
                        masterKey,
                        verifyBytes);
                var actualNames = new HashSet<string>(
                    verified.Profiles.Select(
                        profile => profile.Name),
                    StringComparer.Ordinal);
                if (!actualNames.SetEquals(expectedNames))
                {
                    throw new InvalidDataException(
                        "作成後のWi-Fiバックアップ検証に失敗しました。");
                }
            }
            finally
            {
                ZeroMemory(verifyBytes);
            }

            File.Move(partialPath, finalPath);
        }
        finally
        {
            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }
        }
    }

    private static string ValidateNewOutputPath(string path)
    {
        var fullPath = NormalizePath(path);
        if (!fullPath.EndsWith(
                BackupExtension,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"バックアップの拡張子は{BackupExtension}にしてください。",
                nameof(path));
        }

        if (File.Exists(fullPath) ||
            Directory.Exists(fullPath))
        {
            throw new IOException(
                "同名のファイルまたはフォルダーが既にあります。上書きは行いません。");
        }

        ValidateParentDirectory(fullPath);
        return fullPath;
    }

    private static string ValidateExistingInputPath(string path)
    {
        var fullPath = NormalizePath(path);
        var info = new FileInfo(fullPath);
        if (!info.Exists ||
            (info.Attributes & FileAttributes.ReparsePoint) != 0 ||
            info.Length <= 0 ||
            info.Length > WifiCapsuleContainer.MaximumContainerBytes)
        {
            throw new InvalidDataException(
                "Wi-Fiバックアップファイルが見つからないか不正です。");
        }

        return info.FullName;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "ファイルを指定してください。",
                nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        if (fullPath.StartsWith(
                @"\\",
                StringComparison.Ordinal) ||
            fullPath.StartsWith(
                @"\\?\",
                StringComparison.Ordinal) ||
            fullPath.StartsWith(
                @"\??\",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "ネットワークパスとデバイスパスは使用できません。");
        }

        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException(
                "保存先ドライブを確認できません。");
        }

        var drive = new DriveInfo(root);
        if (drive.DriveType == DriveType.Network)
        {
            throw new InvalidOperationException(
                "ネットワークドライブは使用できません。");
        }

        return fullPath;
    }

    private static void ValidateParentDirectory(
        string fullPath)
    {
        var parent = Directory.GetParent(fullPath)
            ?? throw new InvalidOperationException(
                "保存先フォルダーを確認できません。");
        if (!parent.Exists ||
            (parent.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "保存先フォルダーが存在しないか、安全に使用できません。");
        }
    }

    private static void ZeroMemory(byte[] bytes)
    {
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = 0;
        }
    }
}
