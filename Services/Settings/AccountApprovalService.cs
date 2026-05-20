using DiscogScrobblerMVC.Data;
using DiscogScrobblerMVC.Data.Entities;
using DiscogScrobblerMVC.Models;
using DiscogScrobblerMVC.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DiscogScrobblerMVC.Services.Settings;

public class AccountApprovalService : IAccountApprovalService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountApprovalService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<PendingRegistrationViewModel>> GetPendingRegistrationsAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Users
            .AsNoTracking()
            .Where(x => !x.AccountApproved)
            .OrderBy(x => x.Email)
            .Select(x => new PendingRegistrationViewModel
            {
                UserId = x.Id,
                Email = x.Email ?? x.UserName ?? x.Id,
            })
            .ToListAsync(cancellationToken);

    public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default) =>
        _db.Users.AsNoTracking().CountAsync(x => !x.AccountApproved, cancellationToken);

    public async Task<AccountApprovalResult> ApproveAsync(
        string targetUserId,
        ApplicationUser adminUser,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetUserId))
            return new AccountApprovalResult(false, "Missing user id.");

        if (adminUser.Id == targetUserId)
            return new AccountApprovalResult(false, "You cannot approve your own account this way.");

        var target = await _userManager.FindByIdAsync(targetUserId);
        if (target is null)
            return new AccountApprovalResult(false, "User not found.");

        if (target.AccountApproved)
            return new AccountApprovalResult(false, "That account is already approved.");

        target.AccountApproved = true;
        var update = await _userManager.UpdateAsync(target);
        if (!update.Succeeded)
        {
            var err = string.Join("; ", update.Errors.Select(x => x.Description));
            return new AccountApprovalResult(false, err);
        }

        return new AccountApprovalResult(true, null);
    }

    public async Task<AccountApprovalResult> DenyAsync(
        string targetUserId,
        ApplicationUser adminUser,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetUserId))
            return new AccountApprovalResult(false, "Missing user id.");

        if (adminUser.Id == targetUserId)
            return new AccountApprovalResult(false, "You cannot deny your own account.");

        var target = await _userManager.FindByIdAsync(targetUserId);
        if (target is null)
            return new AccountApprovalResult(false, "User not found.");

        if (target.AccountApproved)
            return new AccountApprovalResult(false, "That account is already approved; deny only applies to pending sign-ups.");

        var deleted = await _userManager.DeleteAsync(target);
        if (!deleted.Succeeded)
        {
            var err = string.Join("; ", deleted.Errors.Select(x => x.Description));
            return new AccountApprovalResult(false, err);
        }

        return new AccountApprovalResult(true, null);
    }
}
