using System.Security.Cryptography;
using System.Text;

namespace Ytec.WifiCapsule.Windows;

public static class ApplicationWifiKey
{
    private const int KeySize = 32;
    private const string OfficialKeyResourceName =
        "Ytec.WifiCapsule.Windows.OfficialApplicationKey";
    private const string CustomKeyResourceName =
        "Ytec.WifiCapsule.Windows.CustomApplicationKey";
    private const string DevelopmentKeyLabel =
        "Y-TEC Wi-Fi Capsule public development key v1 - not for real backups";

    public static bool IsOfficialBuild =>
        typeof(ApplicationWifiKey).Assembly.GetManifestResourceInfo(
            OfficialKeyResourceName) is not null;

    public static bool IsCustomKeyBuild =>
        typeof(ApplicationWifiKey).Assembly.GetManifestResourceInfo(
            CustomKeyResourceName) is not null;

    public static bool UsesPublicDevelopmentKey =>
        !IsOfficialBuild && !IsCustomKeyBuild;

    public static byte[] GetKey()
    {
        var assembly = typeof(ApplicationWifiKey).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            IsOfficialBuild
                ? OfficialKeyResourceName
                : CustomKeyResourceName);
        if (stream is null)
        {
            return GetDevelopmentKey();
        }

        var key = new byte[KeySize];
        var offset = 0;
        while (offset < key.Length)
        {
            var read = stream.Read(key, offset, key.Length - offset);
            if (read <= 0)
            {
                ZeroMemory(key);
                throw new InvalidOperationException(
                    "公式アプリ鍵リソースの長さが不正です。");
            }

            offset += read;
        }

        if (stream.ReadByte() != -1)
        {
            ZeroMemory(key);
            throw new InvalidOperationException(
                "公式アプリ鍵リソースの長さが不正です。");
        }

        return key;
    }

    private static byte[] GetDevelopmentKey()
    {
        var label = Encoding.UTF8.GetBytes(DevelopmentKeyLabel);
        try
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(label);
        }
        finally
        {
            ZeroMemory(label);
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
