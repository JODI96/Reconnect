using System;
using System.Collections.Generic;

namespace Reconnect.Contracts.Wallet
{
    /// <summary>In-game money. Amounts are Swiss francs with two decimals (the backend stores Rappen).</summary>
    public static class Currency
    {
        public const string Chf = "CHF";
    }

    /// <summary>Why money moved. More kinds come with the economy system.</summary>
    public static class TransactionKinds
    {
        public const string StartingCapital = "starting-capital";
        public const string OfficePurchase = "office-purchase";
        public const string OfficeSale = "office-sale";
    }

    /// <param name="Amount">Positive = received, negative = paid.</param>
    public sealed record WalletTransactionDto(Guid Id, decimal Amount, string Kind, string Description, DateTimeOffset At);

    public sealed record WalletDto(decimal Balance, string Currency, IReadOnlyList<WalletTransactionDto> RecentTransactions);
}
