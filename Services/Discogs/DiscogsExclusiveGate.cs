namespace DiscogScrobblerMVC.Services;

/// <summary>
/// Serializes Discogs detail/image work across hosted services. The daily job and on-demand sync
/// each use their own scope and <c>DbContext</c>; without this they can load the same trackless
/// releases and both sync them (duplicate log lines and duplicate <see cref="Data.Entities.Track"/> rows).
/// </summary>
public sealed class DiscogsExclusiveGate : IDisposable
{
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public Task WaitAsync(CancellationToken cancellationToken) => _mutex.WaitAsync(cancellationToken);

    public void Release() => _mutex.Release();

    public void Dispose() => _mutex.Dispose();
}
