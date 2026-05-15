namespace DiscogScrobblerMVC.Services.Interfaces;

public enum DiscogsQueuedWorkKind
{
    FullSync,
    ForceRefreshUserCollection,
}

public readonly record struct DiscogsQueuedWork(DiscogsQueuedWorkKind Kind, string ApplicationUserId);
