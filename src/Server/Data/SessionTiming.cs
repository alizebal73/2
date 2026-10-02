namespace GameNetManager.Server.Data;

public static class SessionTiming
{
    public static double GetBillableMinutes(Session session, DateTimeOffset now)
    {
        var effectiveNow = session.EndAt.HasValue && session.EndAt.Value < now
            ? session.EndAt.Value
            : now;

        var elapsed = Math.Max(0d, (effectiveNow - session.StartAt).TotalMinutes);
        var activePauseMinutes = session.PausedAt.HasValue
            ? Math.Max(0d, (effectiveNow - session.PausedAt.Value).TotalMinutes)
            : 0d;

        return Math.Max(
            0d,
            elapsed
            - session.PausedMinutes
            - activePauseMinutes
            + session.TimeAdjustmentMinutes);
    }
}
