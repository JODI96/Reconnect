namespace Reconnect.Modules.Wallet.Public;

/// <summary>An amount of Swiss francs, stored exactly as whole Rappen (no floating point rounding).</summary>
public readonly record struct Chf(long Rappen)
{
    public static Chf Zero => new(0);

    public decimal Francs => Rappen / 100m;

    public static Chf FromFrancs(decimal francs) => new((long)decimal.Round(francs * 100m, MidpointRounding.AwayFromZero));

    public static Chf operator +(Chf a, Chf b) => new(a.Rappen + b.Rappen);
    public static Chf operator -(Chf a, Chf b) => new(a.Rappen - b.Rappen);

    public override string ToString() => $"CHF {Francs:N2}";
}

/// <summary>
/// Money for other modules. Every change is a booking in the ledger (kind + description + reference),
/// so balances can always be explained – the base for the later economy system.
/// </summary>
public interface IWallet
{
    /// <summary>Balance; opens the account (with starting capital) on first use.</summary>
    Task<Chf> GetBalanceAsync(Guid userId, CancellationToken ct);

    /// <summary>Takes money if the balance covers it (atomic, never below zero). False = not enough money.</summary>
    Task<bool> TryDebitAsync(Guid userId, Chf amount, string kind, string description, string reference, CancellationToken ct);

    Task CreditAsync(Guid userId, Chf amount, string kind, string description, string reference, CancellationToken ct);
}
