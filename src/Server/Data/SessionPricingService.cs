using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record SessionPricingSnapshot(
    decimal HourlyRate,
    decimal VipDiscountPercent,
    bool VipPackageActive);

public sealed class SessionPricingService(GameNetDbContext database)
{
    public async Task<SessionPricingSnapshot> GetPricingAsync(
        Guid customerId,
        Guid stationId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var customer = await database.Customers
            .AsNoTracking()
            .Include(item => item.VipPackage)
            .FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);
        if (customer is null)
            throw new KeyNotFoundException("مشتری برای محاسبه تعرفه پیدا نشد.");

        var station = await database.Stations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == stationId, cancellationToken);
        if (station is null)
            throw new KeyNotFoundException("ایستگاه برای محاسبه تعرفه پیدا نشد.");

        var vipActive = customer.IsVip
            && customer.VipPackage is not null
            && customer.VipPackage.IsActive
            && (!customer.VipExpiresAt.HasValue || customer.VipExpiresAt.Value > at);

        var vipDiscount = vipActive
            ? Math.Min(100m, Math.Max(0m, customer.VipPackage!.DiscountPercent))
            : 0m;

        var hourlyRate = Math.Max(
            0m,
            Math.Round(station.RatePerHour * (1m - vipDiscount / 100m), 0, MidpointRounding.AwayFromZero));

        return new SessionPricingSnapshot(hourlyRate, vipDiscount, vipActive);
    }

    public static decimal CalculateTimeAmount(
        Session session,
        decimal hourlyRate,
        int freeTimeMinutes,
        DateTimeOffset now)
    {
        var elapsedMinutes = SessionTiming.GetBillableMinutes(session, now);
        if (freeTimeMinutes < 0 || freeTimeMinutes > Math.Ceiling(elapsedMinutes))
            throw new InvalidOperationException("دقیقه اعتبار رایگان مصرف‌شده با زمان جلسه سازگار نیست.");

        var billableMinutes = Math.Max(0d, elapsedMinutes - freeTimeMinutes);
        var rawAmount = hourlyRate * (decimal)billableMinutes / 60m;
        return Math.Max(0m, Math.Round(rawAmount, 0, MidpointRounding.AwayFromZero));
    }
}
