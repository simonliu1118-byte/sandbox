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
    string? SourcePackageId = null);

public sealed record UserProfile(
    string Id,
    string DisplayName,
    long TotalSpent,
    long TotalRedeemed)
{
    public long Net => TotalRedeemed - TotalSpent;
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

public sealed record TicketHistoryEntry(
    string Id,
    string UserId,
    string TicketId,
    int BatchNumber,
    long Price,
    long PrizeAmount,
    DateTimeOffset CompletedAt);

public enum BatchStatus
{
    Active,
    Closed
}
