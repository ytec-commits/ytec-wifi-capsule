using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Ytec.WifiCapsule.Core.Models;

namespace Ytec.WifiCapsule.Core.Services;

public static class WifiCapsuleContainer
{
    private static readonly byte[] Magic =
        Encoding.ASCII.GetBytes("YTECWCAP");
    private static readonly byte[] KeyDerivationLabel =
        Encoding.ASCII.GetBytes(
            "Y-TEC Wi-Fi Capsule encrypted container v1");

    private const byte FormatVersion = 1;
    private const byte ApplicationKeyId = 1;
    private const int MasterKeySize = 32;
    private const int SaltSize = 16;
    private const int IvSize = 16;
    private const int TagSize = 32;
    private const int HeaderSize =
        8 + 1 + 1 + SaltSize + IvSize + sizeof(int);
    private const int MaximumProfiles = 256;
    private const int MaximumPlaintextBytes = 24 * 1024 * 1024;
    private const int MaximumCombinedProfileBytes = 16 * 1024 * 1024;
    private const string ProductId = "ytec-wifi-capsule";

    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Error,
        DateParseHandling = DateParseHandling.DateTimeOffset,
    };

    public static int MaximumContainerBytes =>
        HeaderSize + MaximumPlaintextBytes + 32 + TagSize;

    public static byte[] Encrypt(
        byte[] masterKey,
        IReadOnlyList<WifiProfileDocument> profiles,
        DateTimeOffset createdAt)
    {
        ValidateMasterKey(masterKey);
        ValidateProfiles(profiles);

        var payloadModel = new Payload
        {
            SchemaVersion = 1,
            ProductId = ProductId,
            CreatedAt = createdAt,
        };
        try
        {
            foreach (var profile in profiles)
            {
                payloadModel.Profiles.Add(new PayloadProfile
                {
                    Name = profile.Name,
                    Xml = profile.GetXmlCopy(),
                });
            }

            var payload = Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(
                    payloadModel,
                    Formatting.None,
                    SerializerSettings));
            if (payload.Length > MaximumPlaintextBytes)
            {
                ZeroMemory(payload);
                throw new InvalidDataException(
                    "Wi-Fiバックアップの暗号化前サイズが上限を超えています。");
            }

            return EncryptPayload(masterKey, payload);
        }
        finally
        {
            payloadModel.Clear();
        }
    }

    public static WifiCapsuleDocument Decrypt(
        byte[] masterKey,
        byte[] container)
    {
        ValidateMasterKey(masterKey);
        if (container is null)
        {
            throw new ArgumentNullException(nameof(container));
        }

        if (container.Length < HeaderSize + TagSize ||
            !MatchesAt(container, 0, Magic) ||
            container[8] != FormatVersion ||
            container[9] != ApplicationKeyId)
        {
            throw new InvalidDataException(
                "対応していないWi-Fi Capsule形式です。");
        }

        var ciphertextLength = ReadInt32LittleEndian(
            container,
            HeaderSize - sizeof(int));
        if (ciphertextLength <= 0 ||
            ciphertextLength > MaximumPlaintextBytes + 32 ||
            container.Length != HeaderSize + ciphertextLength + TagSize)
        {
            throw new InvalidDataException(
                "Wi-Fiバックアップの長さが不正です。");
        }

        var salt = new byte[SaltSize];
        var iv = new byte[IvSize];
        Buffer.BlockCopy(container, 10, salt, 0, SaltSize);
        Buffer.BlockCopy(
            container,
            10 + SaltSize,
            iv,
            0,
            IvSize);
        var encryptionKey = DeriveKey(masterKey, salt, 1);
        var authenticationKey = DeriveKey(masterKey, salt, 2);
        byte[]? plaintext = null;
        try
        {
            byte[] expectedTag;
            using (var hmac = new HMACSHA256(authenticationKey))
            {
                expectedTag = hmac.ComputeHash(
                    container,
                    0,
                    HeaderSize + ciphertextLength);
            }

            var actualTag = new byte[TagSize];
            Buffer.BlockCopy(
                container,
                HeaderSize + ciphertextLength,
                actualTag,
                0,
                TagSize);
            var authentic = FixedTimeEquals(expectedTag, actualTag);
            ZeroMemory(expectedTag);
            ZeroMemory(actualTag);
            if (!authentic)
            {
                throw new InvalidDataException(
                    "Wi-Fiバックアップを認証できません。破損または異なる製品形式です。");
            }

            var ciphertext = new byte[ciphertextLength];
            Buffer.BlockCopy(
                container,
                HeaderSize,
                ciphertext,
                0,
                ciphertextLength);
            try
            {
                using var aes = Aes.Create();
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = encryptionKey;
                aes.IV = iv;
                using var decryptor = aes.CreateDecryptor();
                plaintext = decryptor.TransformFinalBlock(
                    ciphertext,
                    0,
                    ciphertext.Length);
            }
            finally
            {
                ZeroMemory(ciphertext);
            }

            return DeserializeAndValidate(plaintext);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException(
                "Wi-Fiバックアップを復号できません。破損または異なる製品形式です。",
                exception);
        }
        finally
        {
            ZeroMemory(salt);
            ZeroMemory(iv);
            ZeroMemory(encryptionKey);
            ZeroMemory(authenticationKey);
            if (plaintext is not null)
            {
                ZeroMemory(plaintext);
            }
        }
    }

    private static byte[] EncryptPayload(
        byte[] masterKey,
        byte[] payload)
    {
        var salt = RandomBytes(SaltSize);
        var iv = RandomBytes(IvSize);
        var encryptionKey = DeriveKey(masterKey, salt, 1);
        var authenticationKey = DeriveKey(masterKey, salt, 2);
        byte[]? ciphertext = null;
        try
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = encryptionKey;
                aes.IV = iv;
                using var encryptor = aes.CreateEncryptor();
                ciphertext = encryptor.TransformFinalBlock(
                    payload,
                    0,
                    payload.Length);
            }

            if (ciphertext.Length > MaximumPlaintextBytes + 32)
            {
                throw new InvalidDataException(
                    "Wi-Fiバックアップの暗号化後サイズが上限を超えています。");
            }

            var container =
                new byte[HeaderSize + ciphertext.Length + TagSize];
            Buffer.BlockCopy(
                Magic,
                0,
                container,
                0,
                Magic.Length);
            container[8] = FormatVersion;
            container[9] = ApplicationKeyId;
            Buffer.BlockCopy(
                salt,
                0,
                container,
                10,
                salt.Length);
            Buffer.BlockCopy(
                iv,
                0,
                container,
                10 + SaltSize,
                iv.Length);
            WriteInt32LittleEndian(
                container,
                HeaderSize - sizeof(int),
                ciphertext.Length);
            Buffer.BlockCopy(
                ciphertext,
                0,
                container,
                HeaderSize,
                ciphertext.Length);

            byte[] tag;
            using (var hmac = new HMACSHA256(authenticationKey))
            {
                tag = hmac.ComputeHash(
                    container,
                    0,
                    HeaderSize + ciphertext.Length);
            }

            Buffer.BlockCopy(
                tag,
                0,
                container,
                HeaderSize + ciphertext.Length,
                TagSize);
            ZeroMemory(tag);
            return container;
        }
        finally
        {
            ZeroMemory(payload);
            ZeroMemory(salt);
            ZeroMemory(iv);
            ZeroMemory(encryptionKey);
            ZeroMemory(authenticationKey);
            if (ciphertext is not null)
            {
                ZeroMemory(ciphertext);
            }
        }
    }

    private static WifiCapsuleDocument DeserializeAndValidate(
        byte[] plaintext)
    {
        Payload? payload;
        try
        {
            payload = JsonConvert.DeserializeObject<Payload>(
                Encoding.UTF8.GetString(plaintext),
                SerializerSettings);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Wi-Fiバックアップの内容が不正です。",
                exception);
        }

        if (payload is null ||
            payload.SchemaVersion != 1 ||
            !string.Equals(
                payload.ProductId,
                ProductId,
                StringComparison.Ordinal))
        {
            payload?.Clear();
            throw new InvalidDataException(
                "対応していないWi-Fiバックアップ内容です。");
        }

        var documents = new List<WifiProfileDocument>();
        try
        {
            if (payload.Profiles.Count <= 0 ||
                payload.Profiles.Count > MaximumProfiles)
            {
                throw new InvalidDataException(
                    "Wi-Fiプロファイル数が上限範囲外です。");
            }

            var totalBytes = 0L;
            var names =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var profile in payload.Profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.Name) ||
                    profile.Xml is null)
                {
                    throw new InvalidDataException(
                        "Wi-Fiプロファイルの内容が不正です。");
                }

                var metadata =
                    WifiProfileXmlValidator.Validate(profile.Xml);
                if (!string.Equals(
                        metadata.Name,
                        profile.Name,
                        StringComparison.Ordinal) ||
                    !names.Add(profile.Name))
                {
                    throw new InvalidDataException(
                        "Wi-Fiプロファイル名が不正または重複しています。");
                }

                totalBytes += profile.Xml.Length;
                if (totalBytes > MaximumCombinedProfileBytes)
                {
                    throw new InvalidDataException(
                        "Wi-Fiプロファイルの合計サイズが上限を超えています。");
                }

                documents.Add(
                    new WifiProfileDocument(
                        profile.Name,
                        profile.Xml));
            }

            return new WifiCapsuleDocument(
                payload.CreatedAt,
                documents);
        }
        catch
        {
            foreach (var document in documents)
            {
                document.Dispose();
            }

            throw;
        }
        finally
        {
            payload.Clear();
        }
    }

    private static void ValidateMasterKey(byte[] masterKey)
    {
        if (masterKey is null)
        {
            throw new ArgumentNullException(nameof(masterKey));
        }

        if (masterKey.Length != MasterKeySize)
        {
            throw new ArgumentException(
                "Wi-Fi暗号化マスター鍵は32バイトである必要があります。",
                nameof(masterKey));
        }
    }

    private static void ValidateProfiles(
        IReadOnlyList<WifiProfileDocument> profiles)
    {
        if (profiles is null)
        {
            throw new ArgumentNullException(nameof(profiles));
        }

        if (profiles.Count <= 0 || profiles.Count > MaximumProfiles)
        {
            throw new InvalidDataException(
                "Wi-Fiプロファイル数が上限範囲外です。");
        }

        var combinedBytes = 0L;
        var names =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
        {
            if (!names.Add(profile.Name))
            {
                throw new InvalidDataException(
                    "同じ名前のWi-Fiプロファイルが重複しています。");
            }

            var xml = profile.GetXmlCopy();
            try
            {
                var metadata =
                    WifiProfileXmlValidator.Validate(xml);
                if (!string.Equals(
                        metadata.Name,
                        profile.Name,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Wi-Fiプロファイル名とXMLが一致しません。");
                }

                combinedBytes += xml.Length;
                if (combinedBytes > MaximumCombinedProfileBytes)
                {
                    throw new InvalidDataException(
                        "Wi-Fiプロファイルの合計サイズが上限を超えています。");
                }
            }
            finally
            {
                ZeroMemory(xml);
            }
        }
    }

    private static byte[] DeriveKey(
        byte[] masterKey,
        byte[] salt,
        byte purpose)
    {
        var material =
            new byte[KeyDerivationLabel.Length + 1 + salt.Length];
        Buffer.BlockCopy(
            KeyDerivationLabel,
            0,
            material,
            0,
            KeyDerivationLabel.Length);
        material[KeyDerivationLabel.Length] = purpose;
        Buffer.BlockCopy(
            salt,
            0,
            material,
            KeyDerivationLabel.Length + 1,
            salt.Length);
        try
        {
            using var hmac = new HMACSHA256(masterKey);
            return hmac.ComputeHash(material);
        }
        finally
        {
            ZeroMemory(material);
        }
    }

    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        using var generator = RandomNumberGenerator.Create();
        generator.GetBytes(bytes);
        return bytes;
    }

    private static bool MatchesAt(
        byte[] source,
        int offset,
        byte[] expected)
    {
        if (source.Length - offset < expected.Length)
        {
            return false;
        }

        var difference = 0;
        for (var index = 0; index < expected.Length; index++)
        {
            difference |= source[offset + index] ^ expected[index];
        }

        return difference == 0;
    }

    private static bool FixedTimeEquals(
        byte[] first,
        byte[] second)
    {
        if (first.Length != second.Length)
        {
            return false;
        }

        var difference = 0;
        for (var index = 0; index < first.Length; index++)
        {
            difference |= first[index] ^ second[index];
        }

        return difference == 0;
    }

    private static void WriteInt32LittleEndian(
        byte[] buffer,
        int offset,
        int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    private static int ReadInt32LittleEndian(
        byte[] buffer,
        int offset) =>
        buffer[offset] |
        (buffer[offset + 1] << 8) |
        (buffer[offset + 2] << 16) |
        (buffer[offset + 3] << 24);

    private static void ZeroMemory(byte[] bytes)
    {
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = 0;
        }
    }

    private sealed class Payload
    {
        public int SchemaVersion { get; set; }

        public string ProductId { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public List<PayloadProfile> Profiles { get; set; } = new();

        public void Clear()
        {
            foreach (var profile in Profiles)
            {
                if (profile.Xml is not null)
                {
                    ZeroMemory(profile.Xml);
                }
            }

            Profiles.Clear();
        }
    }

    private sealed class PayloadProfile
    {
        public string Name { get; set; } = string.Empty;

        public byte[]? Xml { get; set; }
    }
}
