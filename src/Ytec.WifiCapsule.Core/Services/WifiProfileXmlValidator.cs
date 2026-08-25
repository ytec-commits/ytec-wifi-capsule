using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Ytec.WifiCapsule.Core.Services;

public static class WifiProfileXmlValidator
{
    public const int MaximumProfileBytes = 1024 * 1024;
    private const string WlanProfileNamespaceHttp =
        "http://www.microsoft.com/networking/WLAN/profile/v1";
    private const string WlanProfileNamespaceHttps =
        "https://www.microsoft.com/networking/WLAN/profile/v1";

    public static WifiProfileXmlMetadata Validate(byte[] xml)
    {
        if (xml is null)
        {
            throw new ArgumentNullException(nameof(xml));
        }

        if (xml.Length == 0 || xml.Length > MaximumProfileBytes)
        {
            throw new InvalidDataException("Wi-FiプロファイルXMLのサイズが不正です。");
        }

        try
        {
            using var stream = new MemoryStream(xml, writable: false);
            using var reader = XmlReader.Create(
                stream,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                    MaxCharactersInDocument = MaximumProfileBytes,
                    MaxCharactersFromEntities = 0,
                });
            var document = XDocument.Load(reader, LoadOptions.None);
            var root = document.Root
                ?? throw new InvalidDataException("Wi-FiプロファイルXMLが空です。");
            if (!root.Name.LocalName.Equals("WLANProfile", StringComparison.Ordinal) ||
                !IsSupportedNamespace(root.Name.NamespaceName))
            {
                throw new InvalidDataException("Windows Wi-FiプロファイルXMLではありません。");
            }

            var profileNamespace = root.Name.Namespace;
            var name = root.Element(profileNamespace + "name")?.Value;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidDataException("Wi-Fiプロファイル名が不正です。");
            }

            if (name!.Length > 255 || name.IndexOf('\0') >= 0)
            {
                throw new InvalidDataException("Wi-Fiプロファイル名が不正です。");
            }

            var keyMaterial = root
                .Descendants(profileNamespace + "keyMaterial")
                .FirstOrDefault();
            var protectedElement = root
                .Descendants(profileNamespace + "protected")
                .FirstOrDefault();
            var hasKeyMaterial = !string.IsNullOrEmpty(keyMaterial?.Value);
            var hasProtectedKey = hasKeyMaterial &&
                !string.Equals(
                    protectedElement?.Value?.Trim(),
                    bool.FalseString,
                    StringComparison.OrdinalIgnoreCase);

            return new WifiProfileXmlMetadata(
                name,
                hasKeyMaterial && !hasProtectedKey,
                hasProtectedKey);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException(
                "Wi-FiプロファイルXMLを解析できません。",
                exception);
        }
    }

    public static byte[] EncodeAndValidate(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new ArgumentException(
                "Wi-FiプロファイルXMLを指定してください。",
                nameof(xml));
        }

        var bytes = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetBytes(xml);
        Validate(bytes);
        return bytes;
    }

    private static bool IsSupportedNamespace(string value) =>
        value.Equals(WlanProfileNamespaceHttp, StringComparison.Ordinal) ||
        value.Equals(WlanProfileNamespaceHttps, StringComparison.Ordinal);
}

public sealed record WifiProfileXmlMetadata(
    string Name,
    bool ContainsPlaintextKey,
    bool ContainsProtectedKey);
