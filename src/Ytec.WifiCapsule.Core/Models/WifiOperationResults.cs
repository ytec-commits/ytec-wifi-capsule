namespace Ytec.WifiCapsule.Core.Models;

public sealed record WifiBackupResult(
    int ProfileCount,
    string OutputPath,
    long FileSize);

public sealed record WifiRestoreResult(
    int SelectedProfiles,
    int RestoredProfiles,
    int SkippedProfiles,
    int FailedProfiles,
    IReadOnlyList<string> Errors);
