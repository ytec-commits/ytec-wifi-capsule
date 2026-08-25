namespace Ytec.WifiCapsule.Core.Models;

public sealed record WifiStoredProfile(
    string Name,
    bool IsGroupPolicy,
    bool IsCurrentUser);
