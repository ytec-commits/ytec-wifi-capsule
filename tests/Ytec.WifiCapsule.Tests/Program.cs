using System.ComponentModel;
using System.Text;
using Ytec.WifiCapsule.Core.Models;
using Ytec.WifiCapsule.Core.Services;
using Ytec.WifiCapsule.Windows;

namespace Ytec.WifiCapsule.Tests;

internal static class Program
{
    private static readonly byte[] TestKey =
    {
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
        0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
        0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
        0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
    };

    private static int Main(string[] args)
    {
        if (args.Length == 2 &&
            args[0].Equals(
                "--write-cross-arch",
                StringComparison.OrdinalIgnoreCase))
        {
            WriteCrossArchitectureContainer(args[1]);
            return 0;
        }

        if (args.Length == 2 &&
            args[0].Equals(
                "--read-cross-arch",
                StringComparison.OrdinalIgnoreCase))
        {
            ReadCrossArchitectureContainer(args[1]);
            return 0;
        }

        if (args.Length == 2 &&
            args[0].Equals(
                "--write-application-key",
                StringComparison.OrdinalIgnoreCase))
        {
            WriteApplicationKeyContainer(args[1]);
            return 0;
        }

        if (args.Length == 2 &&
            args[0].Equals(
                "--read-application-key",
                StringComparison.OrdinalIgnoreCase))
        {
            ReadApplicationKeyContainer(args[1]);
            return 0;
        }

        if (args.Length == 1 &&
            args[0].Equals(
                "--probe-native-wifi",
                StringComparison.OrdinalIgnoreCase))
        {
            ProbeNativeWifi();
            return 0;
        }

        var tests = new[]
        {
            new TestCase(
                "暗号化コンテナを往復できる",
                ContainerRoundTrips),
            new TestCase(
                "同じ内容でも暗号文が毎回異なる",
                EncryptionUsesRandomSaltAndIv),
            new TestCase(
                "異なるアプリ鍵を拒否する",
                WrongKeyIsRejected),
            new TestCase(
                "ヘッダー改ざんを拒否する",
                HeaderTamperingIsRejected),
            new TestCase(
                "暗号文改ざんを拒否する",
                CiphertextTamperingIsRejected),
            new TestCase(
                "認証タグ改ざんを拒否する",
                TagTamperingIsRejected),
            new TestCase(
                "切断されたファイルを拒否する",
                TruncationIsRejected),
            new TestCase(
                "末尾追加を拒否する",
                AppendedBytesAreRejected),
            new TestCase(
                "未知の形式版と鍵IDを拒否する",
                UnknownHeaderValuesAreRejected),
            new TestCase(
                "平文SSIDとWi-Fiキーをコンテナへ残さない",
                ContainerDoesNotExposePlaintext),
            new TestCase(
                "DTDを含むXMLを拒否する",
                DtdXmlIsRejected),
            new TestCase(
                "保護されたWindowsキーを検出する",
                ProtectedKeyIsDetected),
            new TestCase(
                "XMLと表示名の不一致を拒否する",
                ProfileNameMismatchIsRejected),
            new TestCase(
                "大文字小文字だけ違う重複名を拒否する",
                DuplicateNamesAreRejected),
            new TestCase(
                "プロファイル数上限を超える入力を拒否する",
                ProfileCountLimitIsEnforced),
            new TestCase(
                "選択した設定だけをバックアップする",
                ServiceBacksUpSelectedProfilesOnly),
            new TestCase(
                "選択した設定だけを復元する",
                ServiceRestoresSelectedProfilesOnly),
            new TestCase(
                "同名設定を既定でスキップする",
                ExistingProfilesAreSkippedByDefault),
            new TestCase(
                "明示時だけ同名設定を上書きする",
                ExistingProfilesCanBeOverwritten),
            new TestCase(
                "完成ファイルを上書きしない",
                ExistingBackupIsNotOverwritten),
            new TestCase(
                "失敗時にpartialを残さない",
                FailedBackupLeavesNoPartialFile),
            new TestCase(
                "破損バックアップをサービスでも拒否する",
                ServiceRejectsCorruptedBackup),
            new TestCase(
                "不正な拡張子を拒否する",
                InvalidExtensionIsRejected),
            new TestCase(
                "32バイト以外の鍵を拒否する",
                InvalidKeyLengthIsRejected),
            new TestCase(
                "アプリ鍵ビルドモードを一意に判定する",
                ApplicationKeyModeIsCoherent),
        };

        var failed = 0;
        Console.WriteLine(
            $"Y-TEC Wi-Fi Capsule tests ({(Environment.Is64BitProcess ? "64-bit" : "32-bit")})");
        foreach (var test in tests)
        {
            try
            {
                test.Action();
                Console.WriteLine($"PASS: {test.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine(
                    $"FAIL: {test.Name} - {exception.GetType().Name}: {exception.Message}");
            }
        }

        Console.WriteLine(
            $"{tests.Length - failed}/{tests.Length} tests passed.");
        return failed == 0 ? 0 : 1;
    }

    private static void ProbeNativeWifi()
    {
        var store = new NativeWifiProfileStore();
        try
        {
            var adapters = store.GetAdapters();
            Console.WriteLine(
                "NATIVE_WIFI_OK process={0}-bit adapters={1}",
                Environment.Is64BitProcess ? 64 : 32,
                adapters.Count);
        }
        catch (InvalidOperationException exception)
            when (exception.InnerException is Win32Exception win32Exception &&
                  win32Exception.NativeErrorCode == 1062)
        {
            Console.WriteLine(
                "NATIVE_WIFI_OK process={0}-bit service=stopped",
                Environment.Is64BitProcess ? 64 : 32);
        }
    }

    private static void ContainerRoundTrips()
    {
        using var first = CreateDocument("SYNTHETIC-HOME");
        using var second = CreateDocument(
            "SYNTHETIC-GUEST",
            openNetwork: true);
        var encrypted = WifiCapsuleContainer.Encrypt(
            CloneKey(),
            new[] { first, second },
            new DateTimeOffset(
                2026,
                7,
                28,
                12,
                34,
                0,
                TimeSpan.FromHours(9)));
        using var actual = WifiCapsuleContainer.Decrypt(
            CloneKey(),
            encrypted);
        Assert(actual.Profiles.Count == 2, "件数が一致しません。");
        Assert(
            actual.Profiles[0].Name == "SYNTHETIC-HOME",
            "1件目の名前が一致しません。");
        Assert(
            actual.Profiles[1].Name == "SYNTHETIC-GUEST",
            "2件目の名前が一致しません。");
        Assert(
            actual.CreatedAt.Offset == TimeSpan.FromHours(9),
            "作成日時のオフセットが一致しません。");
    }

    private static void EncryptionUsesRandomSaltAndIv()
    {
        using var profile = CreateDocument("SYNTHETIC-RANDOM");
        var first = WifiCapsuleContainer.Encrypt(
            CloneKey(),
            new[] { profile },
            DateTimeOffset.UtcNow);
        var second = WifiCapsuleContainer.Encrypt(
            CloneKey(),
            new[] { profile },
            DateTimeOffset.UtcNow);
        Assert(!first.SequenceEqual(second), "暗号文が同一です。");
    }

    private static void WrongKeyIsRejected()
    {
        var encrypted = CreateContainer("SYNTHETIC-WRONG-KEY");
        var wrongKey = Enumerable
            .Repeat((byte)0xAA, 32)
            .ToArray();
        AssertThrows<InvalidDataException>(
            () => WifiCapsuleContainer.Decrypt(
                wrongKey,
                encrypted));
    }

    private static void HeaderTamperingIsRejected()
    {
        var encrypted = CreateContainer("SYNTHETIC-HEADER");
        encrypted[0] ^= 0x20;
        AssertThrows<InvalidDataException>(
            () => WifiCapsuleContainer.Decrypt(
                CloneKey(),
                encrypted));
    }

    private static void CiphertextTamperingIsRejected()
    {
        var encrypted = CreateContainer("SYNTHETIC-CIPHER");
        encrypted[50] ^= 0x10;
        AssertThrows<InvalidDataException>(
            () => WifiCapsuleContainer.Decrypt(
                CloneKey(),
                encrypted));
    }

    private static void TagTamperingIsRejected()
    {
        var encrypted = CreateContainer("SYNTHETIC-TAG");
        encrypted[encrypted.Length - 1] ^= 0x01;
        AssertThrows<InvalidDataException>(
            () => WifiCapsuleContainer.Decrypt(
                CloneKey(),
                encrypted));
    }

    private static void TruncationIsRejected()
    {
        var encrypted = CreateContainer("SYNTHETIC-TRUNCATED");
        Array.Resize(ref encrypted, encrypted.Length - 1);
        AssertThrows<InvalidDataException>(
            () => WifiCapsuleContainer.Decrypt(
                CloneKey(),
                encrypted));
    }

    private static void AppendedBytesAreRejected()
    {
        var encrypted = CreateContainer("SYNTHETIC-APPENDED");
        Array.Resize(ref encrypted, encrypted.Length + 1);
        AssertThrows<InvalidDataException>(
            () => WifiCapsuleContainer.Decrypt(
                CloneKey(),
                encrypted));
    }

    private static void UnknownHeaderValuesAreRejected()
    {
        var version = CreateContainer("SYNTHETIC-VERSION");
        version[8] = 99;
        AssertThrows<InvalidDataException>(
            () => WifiCapsuleContainer.Decrypt(
                CloneKey(),
                version));

        var keyId = CreateContainer("SYNTHETIC-KEY-ID");
        keyId[9] = 99;
        AssertThrows<InvalidDataException>(
            () => WifiCapsuleContainer.Decrypt(
                CloneKey(),
                keyId));
    }

    private static void ContainerDoesNotExposePlaintext()
    {
        const string name = "SYNTHETIC-SECRET-NAME";
        const string password = "SYNTHETIC-SECRET-PASSWORD";
        using var profile = new WifiProfileDocument(
            name,
            CreateProfileXml(name, password));
        var encrypted = WifiCapsuleContainer.Encrypt(
            CloneKey(),
            new[] { profile },
            DateTimeOffset.UtcNow);
        Assert(
            !ContainsSequence(
                encrypted,
                Encoding.UTF8.GetBytes(name)),
            "平文名が含まれています。");
        Assert(
            !ContainsSequence(
                encrypted,
                Encoding.UTF8.GetBytes(password)),
            "平文キーが含まれています。");
    }

    private static void DtdXmlIsRejected()
    {
        var xml = Encoding.UTF8.GetBytes(
            """
            <!DOCTYPE WLANProfile [<!ENTITY xxe SYSTEM "file:///C:/Windows/win.ini">]>
            <WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
              <name>&xxe;</name>
            </WLANProfile>
            """);
        AssertThrows<InvalidDataException>(
            () => WifiProfileXmlValidator.Validate(xml));
    }

    private static void ProtectedKeyIsDetected()
    {
        var metadata = WifiProfileXmlValidator.Validate(
            CreateProfileXml(
                "SYNTHETIC-PROTECTED",
                "001122",
                protectedKey: true));
        Assert(
            metadata.ContainsProtectedKey,
            "保護済みキーを検出していません。");
        Assert(
            !metadata.ContainsPlaintextKey,
            "保護済みキーを平文と判定しました。");
    }

    private static void ProfileNameMismatchIsRejected()
    {
        using var profile = new WifiProfileDocument(
            "SYNTHETIC-DISPLAY",
            CreateProfileXml(
                "SYNTHETIC-XML",
                "synthetic-key"));
        AssertThrows<InvalidDataException>(
            () => WifiCapsuleContainer.Encrypt(
                CloneKey(),
                new[] { profile },
                DateTimeOffset.UtcNow));
    }

    private static void DuplicateNamesAreRejected()
    {
        using var first = CreateDocument("SYNTHETIC-DUPLICATE");
        using var second = CreateDocument("synthetic-duplicate");
        AssertThrows<InvalidDataException>(
            () => WifiCapsuleContainer.Encrypt(
                CloneKey(),
                new[] { first, second },
                DateTimeOffset.UtcNow));
    }

    private static void ProfileCountLimitIsEnforced()
    {
        var profiles = Enumerable
            .Range(0, 257)
            .Select(
                index => CreateDocument(
                    $"SYNTHETIC-LIMIT-{index:D3}",
                    openNetwork: true))
            .ToArray();
        try
        {
            AssertThrows<InvalidDataException>(
                () => WifiCapsuleContainer.Encrypt(
                    CloneKey(),
                    profiles,
                    DateTimeOffset.UtcNow));
        }
        finally
        {
            foreach (var profile in profiles)
            {
                profile.Dispose();
            }
        }
    }

    private static void ServiceBacksUpSelectedProfilesOnly()
    {
        using var workspace = new SyntheticWorkspace();
        var store = CreateStore(
            "SYNTHETIC-ONE",
            "SYNTHETIC-TWO",
            "SYNTHETIC-THREE");
        var service = CreateService(store);
        var output = workspace.GetPath("selected.ywcwifi");
        var result = service.CreateBackup(
            store.AdapterId,
            new[] { "SYNTHETIC-TWO" },
            output);
        Assert(result.ProfileCount == 1, "保存件数が不正です。");
        Assert(
            store.ExportRequests.SequenceEqual(
                new[] { "SYNTHETIC-TWO" }),
            "選択外の設定を取得しました。");
        using var opened = service.OpenBackup(output);
        Assert(
            opened.Profiles.Count == 1 &&
            opened.Profiles[0].Name == "SYNTHETIC-TWO",
            "選択外の設定が含まれています。");
    }

    private static void ServiceRestoresSelectedProfilesOnly()
    {
        using var workspace = new SyntheticWorkspace();
        var source = CreateStore(
            "SYNTHETIC-ONE",
            "SYNTHETIC-TWO");
        var sourceService = CreateService(source);
        var output = workspace.GetPath("restore-selected.ywcwifi");
        sourceService.CreateBackup(
            source.AdapterId,
            new[] { "SYNTHETIC-ONE", "SYNTHETIC-TWO" },
            output);
        using var document = sourceService.OpenBackup(output);

        var target = CreateStore();
        var targetService = CreateService(target);
        var result = targetService.Restore(
            target.AdapterId,
            document,
            new[] { "SYNTHETIC-TWO" },
            overwriteExisting: false);
        Assert(result.RestoredProfiles == 1, "復元件数が不正です。");
        Assert(
            target.ImportRequests.SequenceEqual(
                new[] { "SYNTHETIC-TWO" }),
            "選択外の設定を復元しました。");
    }

    private static void ExistingProfilesAreSkippedByDefault()
    {
        using var workspace = new SyntheticWorkspace();
        var source = CreateStore("SYNTHETIC-EXISTING");
        var sourceService = CreateService(source);
        var output = workspace.GetPath("skip-existing.ywcwifi");
        sourceService.CreateBackup(
            source.AdapterId,
            new[] { "SYNTHETIC-EXISTING" },
            output);
        using var document = sourceService.OpenBackup(output);

        var target = CreateStore("SYNTHETIC-EXISTING");
        var result = CreateService(target).Restore(
            target.AdapterId,
            document,
            new[] { "SYNTHETIC-EXISTING" },
            overwriteExisting: false);
        Assert(result.SkippedProfiles == 1, "スキップされません。");
        Assert(
            target.ImportRequests.Count == 0,
            "既存設定を変更しました。");
    }

    private static void ExistingProfilesCanBeOverwritten()
    {
        using var workspace = new SyntheticWorkspace();
        var source = CreateStore("SYNTHETIC-OVERWRITE");
        var sourceService = CreateService(source);
        var output = workspace.GetPath("overwrite.ywcwifi");
        sourceService.CreateBackup(
            source.AdapterId,
            new[] { "SYNTHETIC-OVERWRITE" },
            output);
        using var document = sourceService.OpenBackup(output);

        var target = CreateStore("SYNTHETIC-OVERWRITE");
        var result = CreateService(target).Restore(
            target.AdapterId,
            document,
            new[] { "SYNTHETIC-OVERWRITE" },
            overwriteExisting: true);
        Assert(result.RestoredProfiles == 1, "上書きできません。");
        Assert(
            target.ImportRequests.Count == 1,
            "登録処理が呼ばれていません。");
    }

    private static void ExistingBackupIsNotOverwritten()
    {
        using var workspace = new SyntheticWorkspace();
        var store = CreateStore("SYNTHETIC-NO-OVERWRITE");
        var service = CreateService(store);
        var output = workspace.GetPath("existing.ywcwifi");
        service.CreateBackup(
            store.AdapterId,
            new[] { "SYNTHETIC-NO-OVERWRITE" },
            output);
        var original = File.ReadAllBytes(output);
        AssertThrows<IOException>(
            () => service.CreateBackup(
                store.AdapterId,
                new[] { "SYNTHETIC-NO-OVERWRITE" },
                output));
        Assert(
            original.SequenceEqual(File.ReadAllBytes(output)),
            "既存ファイルが変化しました。");
    }

    private static void FailedBackupLeavesNoPartialFile()
    {
        using var workspace = new SyntheticWorkspace();
        var store = CreateStore("SYNTHETIC-FAIL");
        store.FailExport = true;
        var service = CreateService(store);
        AssertThrows<InvalidOperationException>(
            () => service.CreateBackup(
                store.AdapterId,
                new[] { "SYNTHETIC-FAIL" },
                workspace.GetPath("failed.ywcwifi")));
        Assert(
            Directory.GetFiles(
                workspace.Root,
                "*.partial-*",
                SearchOption.TopDirectoryOnly).Length == 0,
            "partialファイルが残っています。");
    }

    private static void ServiceRejectsCorruptedBackup()
    {
        using var workspace = new SyntheticWorkspace();
        var store = CreateStore("SYNTHETIC-CORRUPT");
        var service = CreateService(store);
        var output = workspace.GetPath("corrupt.ywcwifi");
        service.CreateBackup(
            store.AdapterId,
            new[] { "SYNTHETIC-CORRUPT" },
            output);
        var bytes = File.ReadAllBytes(output);
        bytes[bytes.Length / 2] ^= 0x40;
        File.WriteAllBytes(output, bytes);
        AssertThrows<InvalidDataException>(
            () => service.OpenBackup(output));
    }

    private static void InvalidExtensionIsRejected()
    {
        using var workspace = new SyntheticWorkspace();
        var store = CreateStore("SYNTHETIC-EXTENSION");
        AssertThrows<ArgumentException>(
            () => CreateService(store).CreateBackup(
                store.AdapterId,
                new[] { "SYNTHETIC-EXTENSION" },
                workspace.GetPath("backup.bin")));
    }

    private static void InvalidKeyLengthIsRejected()
    {
        using var profile = CreateDocument("SYNTHETIC-BAD-KEY");
        AssertThrows<ArgumentException>(
            () => WifiCapsuleContainer.Encrypt(
                new byte[31],
                new[] { profile },
                DateTimeOffset.UtcNow));
    }

    private static void ApplicationKeyModeIsCoherent()
    {
        var modes = new[]
        {
            ApplicationWifiKey.IsOfficialBuild,
            ApplicationWifiKey.IsCustomKeyBuild,
            ApplicationWifiKey.UsesPublicDevelopmentKey,
        };
        Assert(
            modes.Count(enabled => enabled) == 1,
            "アプリ鍵ビルドモードが一意ではありません。");

        var first = ApplicationWifiKey.GetKey();
        var second = ApplicationWifiKey.GetKey();
        try
        {
            Assert(first.Length == 32, "アプリ鍵の長さが不正です。");
            Assert(first.SequenceEqual(second), "アプリ鍵が安定していません。");
            Assert(first.Any(value => value != 0), "アプリ鍵がゼロだけです。");
        }
        finally
        {
            Array.Clear(first, 0, first.Length);
            Array.Clear(second, 0, second.Length);
        }
    }

    private static void WriteCrossArchitectureContainer(
        string path)
    {
        var fullPath = Path.GetFullPath(path);
        using var first = CreateDocument("SYNTHETIC-X86-X64-A");
        using var second = CreateDocument(
            "SYNTHETIC-X86-X64-B",
            openNetwork: true);
        var encrypted = WifiCapsuleContainer.Encrypt(
            CloneKey(),
            new[] { first, second },
            new DateTimeOffset(
                2026,
                7,
                28,
                0,
                0,
                0,
                TimeSpan.Zero));
        using (var stream = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            stream.Write(encrypted, 0, encrypted.Length);
            stream.Flush(flushToDisk: true);
        }

        Console.WriteLine(
            $"WROTE {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}: {encrypted.Length} bytes");
    }

    private static void ReadCrossArchitectureContainer(
        string path)
    {
        var encrypted = File.ReadAllBytes(
            Path.GetFullPath(path));
        using var document = WifiCapsuleContainer.Decrypt(
            CloneKey(),
            encrypted);
        Assert(
            document.Profiles.Count == 2,
            "相互読込の件数が不正です。");
        Assert(
            document.Profiles[0].Name ==
                "SYNTHETIC-X86-X64-A" &&
            document.Profiles[1].Name ==
                "SYNTHETIC-X86-X64-B",
            "相互読込の内容が不正です。");
        Console.WriteLine(
            $"READ {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}: {encrypted.Length} bytes");
    }

    private static void WriteApplicationKeyContainer(string path)
    {
        var key = ApplicationWifiKey.GetKey();
        try
        {
            using var profile = CreateDocument(
                "SYNTHETIC-APPLICATION-KEY-COMPAT");
            var encrypted = WifiCapsuleContainer.Encrypt(
                key,
                new[] { profile },
                new DateTimeOffset(
                    2026,
                    8,
                    25,
                    0,
                    0,
                    0,
                    TimeSpan.Zero));
            using var stream = new FileStream(
                Path.GetFullPath(path),
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            stream.Write(encrypted, 0, encrypted.Length);
            stream.Flush(flushToDisk: true);
        }
        finally
        {
            Array.Clear(key, 0, key.Length);
        }
    }

    private static void ReadApplicationKeyContainer(string path)
    {
        var encrypted = File.ReadAllBytes(Path.GetFullPath(path));
        var key = ApplicationWifiKey.GetKey();
        try
        {
            using var document = WifiCapsuleContainer.Decrypt(
                key,
                encrypted);
            Assert(
                document.Profiles.Count == 1 &&
                document.Profiles[0].Name ==
                    "SYNTHETIC-APPLICATION-KEY-COMPAT",
                "アプリ鍵互換コンテナの内容が不正です。");
        }
        finally
        {
            Array.Clear(key, 0, key.Length);
            Array.Clear(encrypted, 0, encrypted.Length);
        }
    }

    private static byte[] CreateContainer(string name)
    {
        using var profile = CreateDocument(name);
        return WifiCapsuleContainer.Encrypt(
            CloneKey(),
            new[] { profile },
            DateTimeOffset.UtcNow);
    }

    private static WifiProfileDocument CreateDocument(
        string name,
        bool openNetwork = false)
    {
        return new WifiProfileDocument(
            name,
            openNetwork
                ? CreateOpenProfileXml(name)
                : CreateProfileXml(
                    name,
                    "synthetic-key-material"));
    }

    private static byte[] CreateProfileXml(
        string name,
        string key,
        bool protectedKey = false)
    {
        return Encoding.UTF8.GetBytes(
            $"""
            <?xml version="1.0"?>
            <WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
              <name>{name}</name>
              <SSIDConfig><SSID><name>{name}</name></SSID></SSIDConfig>
              <connectionType>ESS</connectionType>
              <connectionMode>auto</connectionMode>
              <MSM>
                <security>
                  <authEncryption>
                    <authentication>WPA2PSK</authentication>
                    <encryption>AES</encryption>
                    <useOneX>false</useOneX>
                  </authEncryption>
                  <sharedKey>
                    <keyType>passPhrase</keyType>
                    <protected>{protectedKey.ToString().ToLowerInvariant()}</protected>
                    <keyMaterial>{key}</keyMaterial>
                  </sharedKey>
                </security>
              </MSM>
            </WLANProfile>
            """);
    }

    private static byte[] CreateOpenProfileXml(string name)
    {
        return Encoding.UTF8.GetBytes(
            $"""
            <?xml version="1.0"?>
            <WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
              <name>{name}</name>
              <SSIDConfig><SSID><name>{name}</name></SSID></SSIDConfig>
              <connectionType>ESS</connectionType>
              <connectionMode>manual</connectionMode>
              <MSM>
                <security>
                  <authEncryption>
                    <authentication>open</authentication>
                    <encryption>none</encryption>
                    <useOneX>false</useOneX>
                  </authEncryption>
                </security>
              </MSM>
            </WLANProfile>
            """);
    }

    private static SyntheticWifiStore CreateStore(
        params string[] names)
    {
        var store = new SyntheticWifiStore();
        foreach (var name in names)
        {
            store.Add(
                name,
                CreateProfileXml(
                    name,
                    $"synthetic-key-{name}"));
        }

        return store;
    }

    private static WifiCapsuleService CreateService(
        SyntheticWifiStore store)
    {
        return new WifiCapsuleService(
            store,
            CloneKey,
            () => new DateTimeOffset(
                2026,
                7,
                28,
                12,
                0,
                0,
                TimeSpan.FromHours(9)));
    }

    private static byte[] CloneKey()
    {
        return (byte[])TestKey.Clone();
    }

    private static bool ContainsSequence(
        byte[] source,
        byte[] sequence)
    {
        for (var offset = 0;
             offset <= source.Length - sequence.Length;
             offset++)
        {
            var matches = true;
            for (var index = 0;
                 index < sequence.Length;
                 index++)
            {
                if (source[offset + index] != sequence[index])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{typeof(TException).Name}が発生しませんでした。");
    }

    private sealed class TestCase
    {
        public TestCase(string name, Action action)
        {
            Name = name;
            Action = action;
        }

        public string Name { get; }

        public Action Action { get; }
    }

    private sealed class SyntheticWifiStore :
        IWifiProfileStore
    {
        private readonly Dictionary<string, byte[]> _profiles =
            new(StringComparer.Ordinal);

        public Guid AdapterId { get; } =
            new("121D256E-C539-4688-957C-AE78401C7541");

        public List<string> ExportRequests { get; } = new();

        public List<string> ImportRequests { get; } = new();

        public bool FailExport { get; set; }

        public void Add(string name, byte[] xml)
        {
            _profiles[name] = (byte[])xml.Clone();
        }

        public IReadOnlyList<WifiAdapterInfo> GetAdapters()
        {
            return new[]
            {
                new WifiAdapterInfo(
                    AdapterId,
                    "SYNTHETIC-ADAPTER",
                    "合成",
                    true),
            };
        }

        public IReadOnlyList<WifiStoredProfile> GetProfiles(
            Guid adapterId)
        {
            EnsureAdapter(adapterId);
            return _profiles.Keys
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(
                    name => new WifiStoredProfile(
                        name,
                        false,
                        false))
                .ToArray();
        }

        public byte[] ExportProfile(
            Guid adapterId,
            string profileName)
        {
            EnsureAdapter(adapterId);
            ExportRequests.Add(profileName);
            if (FailExport)
            {
                throw new InvalidOperationException(
                    "合成エクスポート失敗");
            }

            return (byte[])_profiles[profileName].Clone();
        }

        public void ImportProfile(
            Guid adapterId,
            byte[] profileXml,
            bool overwriteExisting)
        {
            EnsureAdapter(adapterId);
            var metadata =
                WifiProfileXmlValidator.Validate(profileXml);
            if (_profiles.ContainsKey(metadata.Name) &&
                !overwriteExisting)
            {
                throw new InvalidOperationException(
                    "同名の合成設定があります。");
            }

            ImportRequests.Add(metadata.Name);
            _profiles[metadata.Name] =
                (byte[])profileXml.Clone();
        }

        private void EnsureAdapter(Guid adapterId)
        {
            if (adapterId != AdapterId)
            {
                throw new InvalidOperationException(
                    "合成アダプターが不正です。");
            }
        }
    }

    private sealed class SyntheticWorkspace : IDisposable
    {
        private readonly string _parent;

        public SyntheticWorkspace()
        {
            _parent = Path.GetFullPath(Path.GetTempPath());
            Root = Path.Combine(
                _parent,
                $"ytec-wifi-capsule-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string GetPath(string fileName)
        {
            return Path.Combine(Root, fileName);
        }

        public void Dispose()
        {
            if (!Directory.Exists(Root))
            {
                return;
            }

            var fullRoot = Path.GetFullPath(Root);
            var parent =
                Directory.GetParent(fullRoot)?.FullName;
            if (!string.Equals(
                    Path.GetFullPath(parent ?? string.Empty),
                    _parent.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fullRoot).StartsWith(
                    "ytec-wifi-capsule-tests-",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "合成テスト領域の削除範囲が不正です。");
            }

            Directory.Delete(fullRoot, recursive: true);
        }
    }
}
