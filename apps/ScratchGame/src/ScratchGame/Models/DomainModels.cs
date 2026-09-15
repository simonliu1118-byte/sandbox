namespace ScratchGame.Models;

public sealed record PrizeTierDefinition(
    string Id,
    long Amount,
    long Count,
    int SortOrder = 0);

public sealed record TicketDefinition(
    string Id,
    string DisplayName,
    long Price,
    string RuleId,
    long IssueSize,
    double PublishedWinRate,
    bool Enabled,
    bool Locked,
    string? SourcePackageId = null,
    int ActiveBatchNumber = 0,
    long MaxPrize = 0,
    DateTimeOffset? ActiveBatchStartedAt = null);

public sealed record UserProfile(
    string Id,
    string DisplayName,
    long TotalSpent,
    long TotalRedeemed,
    long WalletBalance = 100_000,
    long CompletedTicketCount = 0,
    long WinCount = 0,
    long MaxPrize = 0,
    long GrantCount = 0,
    long GrantTotalAmount = 0)
{
    public long Net => TotalRedeemed - TotalSpent;
    public double WinRate => CompletedTicketCount > 0
        ? (double)WinCount / CompletedTicketCount
        : 0;
}

public sealed record BatchInfo(
    string Id,
    string TicketId,
    int BatchNumber,
    BatchStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? ClosedAt = null);

public sealed record PendingTicket(
    string Id,
    string UserId,
    string TicketId,
    string BatchId,
    string ReservedTierId,
    long ReservedAmount,
    long Price,
    string PayloadJson,
    DateTimeOffset CreatedAt,
    string? ScratchStateJson = null);

public enum BatchStatus
{
    Active,
    Closed
}
