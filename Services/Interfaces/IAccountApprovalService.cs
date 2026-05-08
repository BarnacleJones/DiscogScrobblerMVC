using DiscogScrobblerMVC.Data.Entities;
using DiscogScrobblerMVC.Models;

namespace DiscogScrobblerMVC.Services.Interfaces;

public interface IAccountApprovalService
{
    Task<IReadOnlyList<PendingRegistrationViewModel>> GetPendingRegistrationsAsync(CancellationToken cancellationToken = default);

    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);

    Task<AccountApprovalResult> ApproveAsync(string targetUserId, ApplicationUser adminUser, CancellationToken cancellationToken = default);

    Task<AccountApprovalResult> DenyAsync(string targetUserId, ApplicationUser adminUser, CancellationToken cancellationToken = default);
}
