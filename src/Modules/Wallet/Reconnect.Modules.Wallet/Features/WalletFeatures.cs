using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reconnect.Contracts;
using Reconnect.Contracts.Wallet;
using Reconnect.Modules.Wallet.Domain;
using Reconnect.Modules.Wallet.Infrastructure;
using Reconnect.Modules.Wallet.Public;
using Reconnect.SharedKernel.Web;

namespace Reconnect.Modules.Wallet.Features;

/// <summary>
/// Accounts are opened lazily (first balance query, debit or credit) with the starting capital – that
/// covers new and existing users without the Wallet having to know the Identity module.
/// Balance changes are single SQL statements (atomic, the check constraint keeps it ≥ 0) plus a ledger entry,
/// in one transaction. Transactions run inside the execution strategy (Aspire enables retries on transient
/// database errors – a retry then repeats the whole transaction, not half of it).
/// </summary>
internal sealed class WalletService(WalletDbContext db, IOptions<WalletOptions> options, TimeProvider time) : IWallet
{
    public async Task<Chf> GetBalanceAsync(Guid userId, CancellationToken ct)
    {
        await EnsureAccountAsync(userId, ct);
        return new Chf(await db.Accounts.Where(a => a.UserId == userId).Select(a => a.BalanceRappen).SingleAsync(ct));
    }

    public async Task<bool> TryDebitAsync(Guid userId, Chf amount, string kind, string description, string reference, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount.Rappen);
        await EnsureAccountAsync(userId, ct);
        return await InTransactionAsync(async () =>
        {
            var changed = await db.Accounts
                .Where(a => a.UserId == userId && a.BalanceRappen >= amount.Rappen)
                .ExecuteUpdateAsync(a => a.SetProperty(x => x.BalanceRappen, x => x.BalanceRappen - amount.Rappen), ct);
            if (changed == 0)
            {
                return false;
            }
            await BookAsync(userId, -amount.Rappen, kind, description, reference, ct);
            return true;
        }, ct);
    }

    public async Task CreditAsync(Guid userId, Chf amount, string kind, string description, string reference, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount.Rappen);
        await EnsureAccountAsync(userId, ct);
        await InTransactionAsync(async () =>
        {
            await db.Accounts
                .Where(a => a.UserId == userId)
                .ExecuteUpdateAsync(a => a.SetProperty(x => x.BalanceRappen, x => x.BalanceRappen + amount.Rappen), ct);
            await BookAsync(userId, amount.Rappen, kind, description, reference, ct);
            return true;
        }, ct);
    }

    /// <summary>Opens the account with the starting capital exactly once, even with parallel requests.</summary>
    private async Task EnsureAccountAsync(Guid userId, CancellationToken ct)
    {
        if (await db.Accounts.AnyAsync(a => a.UserId == userId, ct))
        {
            return;
        }

        var start = Chf.FromFrancs(options.Value.StartingCapital);
        var now = time.GetUtcNow();
        await InTransactionAsync(async () =>
        {
            var opened = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO wallet.accounts (user_id, balance_rappen, created_at) VALUES ({userId}, {start.Rappen}, {now})
                ON CONFLICT (user_id) DO NOTHING
                """, ct);
            if (opened == 1)
            {
                await BookAsync(userId, start.Rappen, TransactionKinds.StartingCapital, "Startkapital", "account:open", ct);
            }
            return true;
        }, ct);
    }

    private Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();   // a retry starts from scratch
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await work();
            await transaction.CommitAsync(ct);
            return result;
        });

    private async Task BookAsync(Guid userId, long rappen, string kind, string description, string reference, CancellationToken ct)
    {
        db.Ledger.Add(new LedgerEntry
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AmountRappen = rappen,
            Kind = kind,
            Description = description.Length > LedgerEntry.DescriptionMaxLength ? description[..LedgerEntry.DescriptionMaxLength] : description,
            Reference = reference,
            CreatedAt = time.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }
}

internal static class WalletEndpoints
{
    public const int RecentTransactions = 20;

    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup(ApiRoutes.Wallet.Path).WithTags("Wallet").RequireAuthorization();
        group.MapGet("/", GetMine);
    }

    private static async Task<Ok<WalletDto>> GetMine(ClaimsPrincipal principal, IWallet wallet, WalletDbContext db, CancellationToken ct)
    {
        var userId = principal.GetUserId();
        var balance = await wallet.GetBalanceAsync(userId, ct);
        var recent = await db.Ledger
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Take(RecentTransactions)
            .Select(e => new WalletTransactionDto(e.Id, e.AmountRappen / 100m, e.Kind, e.Description, e.CreatedAt))
            .ToListAsync(ct);
        return TypedResults.Ok(new WalletDto(balance.Francs, Currency.Chf, recent));
    }
}
