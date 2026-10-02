namespace GameNetManager.Server.Data;

public static class SessionTiming
{
    public static double GetBillableMinutes(Session session, DateTimeOffset now)
    {
        var elapsed = Math.Max(0d, (now - session.StartAt).TotalMinutes);
        var activePauseMinutes = session.PausedAt.HasValue
            ? Math.Max(0d, (now - session.PausedAt.Value).TotalMinutes)
            : 0d;

        return Math.Max(
            0d,
            elapsed
            - session.PausedMinutes
            - activePauseMinutes
            + session.TimeAdjustmentMinutes);
    }

    public static DateTimeOffset? GetProjectedEnd(Session session, DateTimeOffset now)
    {
        if (session.State != SessionState.Active)
            return session.EndAt;

        var remainingAdjustment = session.TimeAdjustmentMinutes;
        if (remainingAdjustment == 0)
            return null;

        var pauseMinutes = session.PausedMinutes
            + (session.PausedAt.HasValue
                ? (int)Math.Ceiling(Math.Max(0d, (now - session.PausedAt.Value).TotalMinutes))
                : 0);

        var baseEnd = session.StartAt
            .AddMinutes(session.TimeAdjustmentMinutes + pauseMinutes + 1);

        return baseEnd;
    }
}
