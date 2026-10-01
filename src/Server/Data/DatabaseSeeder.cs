using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(GameNetDbContext database, CancellationToken cancellationToken = default)
    {
        if (await database.Stations.AnyAsync(cancellationToken))
        {
            return;
        }

        for (var number = 1; number <= 40; number++)
        {
            database.Stations.Add(new StationEntity
            {
                Name = $"PC {number:00}",
                Zone = "pc",
                Type = "کیس گیمینگ",
                RatePerHour = 80000,
                State = "free"
            });
        }

        AddStations(database, "PS5", 10, "console", "پلی‌استیشن ۵", 100000);
        AddStations(database, "PS4", 6, "console", "پلی‌استیشن ۴", 70000);
        AddStations(database, "میز", 5, "table", "فوتبال دستی", 60000);
        await database.SaveChangesAsync(cancellationToken);
    }

    private static void AddStations(GameNetDbContext database, string prefix, int count, string zone, string type, long rate)
    {
        for (var number = 1; number <= count; number++)
        {
            database.Stations.Add(new StationEntity
            {
                Name = $"{prefix} {number:00}",
                Zone = zone,
                Type = type,
                RatePerHour = rate,
                State = "free"
            });
        }
    }
}