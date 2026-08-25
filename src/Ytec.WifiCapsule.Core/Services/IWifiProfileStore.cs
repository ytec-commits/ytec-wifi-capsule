using Ytec.WifiCapsule.Core.Models;

namespace Ytec.WifiCapsule.Core.Services;

public interface IWifiProfileStore
{
    IReadOnlyList<WifiAdapterInfo> GetAdapters();

    IReadOnlyList<WifiStoredProfile> GetProfiles(Guid adapterId);

    byte[] ExportProfile(Guid adapterId, string profileName);

    void ImportProfile(
        Guid adapterId,
        byte[] profileXml,
        bool overwriteExisting);
}
