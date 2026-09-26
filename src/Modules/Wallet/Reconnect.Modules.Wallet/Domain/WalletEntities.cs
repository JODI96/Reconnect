namespace Reconnect.Modules.Wallet.Domain;

/// <summary>One account per user. <see cref="BalanceRappen"/> is the sum of the user's ledger entries.</summary>
internal sealed class Account
{
    public Guid UserId { get; set; }
    public long BalanceRappen { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>A booking: positive = money in, negative = money out. Never changed or deleted.</summary>
internal sealed class LedgerEntry
{
    public const int KindMaxLength = 40;
    public const int DescriptionMaxLength = 200;
    public const int ReferenceMaxLength = 100;

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public long AmountRappen { get; set; }
    public string Kind { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>What the booking belongs to, e.g. "office:{unitId}" – for support and the later economy.</summary>
    public string Reference { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>"Wallet" configuration.</summary>
internal sealed class WalletOptions
{
    public const string SectionName = "Wallet";

    /// <summary>Every new account starts with this much (CHF).</summary>
    public decimal StartingCapital { get; set; } = 10_000m;
}
