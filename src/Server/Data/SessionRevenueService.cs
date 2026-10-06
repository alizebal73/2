using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed class SessionRevenueService(GameNetDbContext database)
{
    public async Task<IReadOnlyDictionary<Guid, decimal>> GetLedgerRevenueBySessionAsync(
        IReadOnlyCollection<Guid> sessionIds,
        CancellationToken cancellationToken)
    {
        if (sessionIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        return (await database.InvoiceItems
            .AsNoTracking()
            .Where(item => item.SessionId.HasValue
                && sessionIds.Contains(item.SessionId.Value)
                && item.Invoice.Status != InvoiceStatus.Cancelled)
            .GroupBy(item => item.SessionId!.Value)
            .Select(group => new
            {
                SessionId = group.Key,
                Amount = group.Sum(item => item.Amount)
            })
            .ToListAsync(cancellationToken))
            .ToDictionary(item => item.SessionId, item => item.Amount);
    }
}
