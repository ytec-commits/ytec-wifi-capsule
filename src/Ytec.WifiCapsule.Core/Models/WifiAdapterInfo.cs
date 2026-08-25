namespace Ytec.WifiCapsule.Core.Models;

public sealed record WifiAdapterInfo(
    Guid Id,
    string Name,
    string Description,
    bool IsConnected);
