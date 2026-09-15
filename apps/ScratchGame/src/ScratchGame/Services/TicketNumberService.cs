using Microsoft.Data.Sqlite;
using ScratchGame.Data;
using ScratchGame.Models;

namespace ScratchGame.Services;

public sealed record TicketPresentationMetadata(
    long StyleNumber,
    long TicketsPerBook,
    int PriceDisplay,
    long IssueSize)
{
    public long BookCount => IssueSize / TicketsPerBook;
}

public sealed class TicketNumberService(AppDatabase database)
{
    public async Task<TicketPresentationMetadata> GetMetadataAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.issue_size,
                   COALESCE(m.style_number, 0),
                   COALESCE(m.tickets_per_book, t.issue_size),
                   COALESCE(m.price_display, 0)
            FROM ticket_definitions t
            LEFT JOIN ticket_metadata m ON m.ticket_id = t.id
            WHERE t.id = $ticketId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$ticketId", ticketId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("找不到彩券定義。");

        var issueSize = reader.GetInt64(0);
        var style = reader.GetInt64(1);
        var ticketsPerBook = reader.GetInt64(2);
        var priceDisplay = reader.GetInt32(3);
        if (ticketsPerBook <= 0 || issueSize % ticketsPerBook != 0)
            throw new InvalidOperationException("彩券的每本張數設定不合法。");

        return new TicketPresentationMetadata(style, ticketsPerBook, priceDisplay, issueSize);
    }

    public async Task<string> GetOrCreateDisplayNumberAsync(
        PendingTicket pending,
        CancellationToken cancellationToken = default)
    {
        var metadata = await GetMetadataAsync(pending.TicketId, cancellationToken);

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var cleanup = connection.CreateCommand();
        cleanup.Transaction = transaction;
        cleanup.CommandText = """
            DELETE FROM batch_serial_claims
            WHERE batch_id = $batchId
              AND consumed = 0
              AND pending_ticket_id IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM pending_tickets p
                  WHERE p.id = batch_serial_claims.pending_ticket_id
              );
            """;
        cleanup.Parameters.AddWithValue("$batchId", pending.BatchId);
        await cleanup.ExecuteNonQueryAsync(cancellationToken);

        long serialIndex;
        var existing = connection.CreateCommand();
        existing.Transaction = transaction;
        existing.CommandText = """
            SELECT serial_index
            FROM batch_serial_claims
            WHERE pending_ticket_id = $pendingId
            LIMIT 1;
            """;
        existing.Parameters.AddWithValue("$pendingId", pending.Id);
        var existingValue = await existing.ExecuteScalarAsync(cancellationToken);
        if (existingValue is not null)
        {
            serialIndex = Convert.ToInt64(existingValue);
        }
        else
        {
            var used = new HashSet<long>();
            var usedCommand = connection.CreateCommand();
            usedCommand.Transaction = transaction;
            usedCommand.CommandText = "SELECT serial_index FROM batch_serial_claims WHERE batch_id = $batchId;";
            usedCommand.Parameters.AddWithValue("$batchId", pending.BatchId);
            await using (var reader = await usedCommand.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    used.Add(reader.GetInt64(0));
            }

            var available = new List<long>();
            for (long index = 1; index <= metadata.IssueSize; index++)
            {
                if (!used.Contains(index))
                    available.Add(index);
            }
            if (available.Count == 0)
                throw new InvalidOperationException("此批彩券已沒有可用票號。");

            serialIndex = available[Random.Shared.Next(available.Count)];
            var claim = connection.CreateCommand();
            claim.Transaction = transaction;
            claim.CommandText = """
                INSERT INTO batch_serial_claims(batch_id, serial_index, pending_ticket_id, consumed)
                VALUES($batchId, $serialIndex, $pendingId, 0);
                """;
            claim.Parameters.AddWithValue("$batchId", pending.BatchId);
            claim.Parameters.AddWithValue("$serialIndex", serialIndex);
            claim.Parameters.AddWithValue("$pendingId", pending.Id);
            await claim.ExecuteNonQueryAsync(cancellationToken);
        }

        var batchInfo = connection.CreateCommand();
        batchInfo.Transaction = transaction;
        batchInfo.CommandText = "SELECT batch_number FROM batches WHERE id = $batchId LIMIT 1;";
        batchInfo.Parameters.AddWithValue("$batchId", pending.BatchId);
        var batchNumber = Convert.ToInt64(await batchInfo.ExecuteScalarAsync(cancellationToken));
        if (batchNumber <= 0)
            throw new InvalidOperationException("找不到彩券批次編號。");

        transaction.Commit();
        return Format(metadata, serialIndex, batchNumber);
    }

    public async Task ReleaseAsync(
        string pendingTicketId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM batch_serial_claims
            WHERE pending_ticket_id = $pendingId AND consumed = 0;
            """;
        command.Parameters.AddWithValue("$pendingId", pendingTicketId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkConsumedAsync(
        string pendingTicketId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE batch_serial_claims
            SET consumed = 1
            WHERE pending_ticket_id = $pendingId;
            """;
        command.Parameters.AddWithValue("$pendingId", pendingTicketId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Format(TicketPresentationMetadata metadata, long serialIndex, long batchNumber)
    {
        var localBookNumber = ((serialIndex - 1) / metadata.TicketsPerBook) + 1;
        var bookNumber = ((batchNumber - 1) * metadata.BookCount) + localBookNumber;
        var numberInBook = ((serialIndex - 1) % metadata.TicketsPerBook) + 1;

        var styleWidth = Math.Max(3, DigitCount(Math.Max(0, metadata.StyleNumber)));
        var bookWidth = Math.Max(3, DigitCount(batchNumber * metadata.BookCount));
        var ticketWidth = Math.Max(3, DigitCount(metadata.TicketsPerBook));

        return $"{metadata.StyleNumber.ToString(new string('0', styleWidth))}-" +
               $"{bookNumber.ToString(new string('0', bookWidth))}-" +
               numberInBook.ToString(new string('0', ticketWidth));
    }

    private static int DigitCount(long value)
        => value <= 0 ? 1 : (int)Math.Floor(Math.Log10(value)) + 1;
}
