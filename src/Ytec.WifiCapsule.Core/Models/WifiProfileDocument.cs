namespace Ytec.WifiCapsule.Core.Models;

public sealed class WifiProfileDocument : IDisposable
{
    private byte[]? _xml;

    public WifiProfileDocument(string name, byte[] xml)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Wi-Fiプロファイル名を指定してください。",
                nameof(name));
        }

        if (xml is null)
        {
            throw new ArgumentNullException(nameof(xml));
        }

        Name = name;
        _xml = (byte[])xml.Clone();
    }

    public string Name { get; }

    public int XmlLength => _xml?.Length ?? 0;

    public byte[] GetXmlCopy()
    {
        if (_xml is null)
        {
            throw new ObjectDisposedException(nameof(WifiProfileDocument));
        }

        return (byte[])_xml.Clone();
    }

    public void Dispose()
    {
        if (_xml is null)
        {
            return;
        }

        ZeroMemory(_xml);
        _xml = null;
        GC.SuppressFinalize(this);
    }

    private static void ZeroMemory(byte[] bytes)
    {
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = 0;
        }
    }
}
