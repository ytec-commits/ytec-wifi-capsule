namespace Ytec.WifiCapsule.Core.Models;

public sealed class WifiCapsuleDocument : IDisposable
{
    private bool _disposed;

    public WifiCapsuleDocument(
        DateTimeOffset createdAt,
        IReadOnlyList<WifiProfileDocument> profiles)
    {
        if (profiles is null)
        {
            throw new ArgumentNullException(nameof(profiles));
        }

        CreatedAt = createdAt;
        Profiles = profiles;
    }

    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyList<WifiProfileDocument> Profiles { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var profile in Profiles)
        {
            profile.Dispose();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
