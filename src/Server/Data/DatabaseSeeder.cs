using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(GameNetDbContext database, CancellationToken cancellationToken = default)
    {
        if (await database.Stations.AnyAsync(cancellationToken))
        {
            await EnsureCustomerProfilesAsync(database, cancellationToken);
            return;
        }

        var consoleType = new StationType { Name = "Console", Description = "PlayStation and console stations" };
        var pcType = new StationType { Name = "PC", Description = "Gaming PC stations" };
        var tableType = new StationType { Name = "Table", Description = "Table and multiplayer stations" };

        database.StationTypes.AddRange(consoleType, pcType, tableType);

        var hourlyTariff = new Tariff
        {
            Name = "Normal",
            Description = "Standard hourly rate",
            HourlyRate = 95000m,
            DailyRate = 550000m,
            IsActive = true
        };
        database.Tariffs.Add(hourlyTariff);

        var vipPackage = new VipPackage
        {
            Name = "Gold",
            Price = 1200000m,
            DurationDays = 30,
            Description = "VIP access for 30 days",
            IsActive = true
        };
        database.VipPackages.Add(vipPackage);

        var product = new Product
        {
            Name = "Energy Drink",
            Category = "Beverage",
            UnitPrice = 25000m,
            CostPrice = 15000m,
            StockQuantity = 40,
            IsActive = true
        };
        database.Products.Add(product);

        var adminUser = new AppUser
        {
            FullName = "System Administrator",
            UserName = "admin",
            Email = "admin@gamenet.local",
            PasswordHash = "hash",
            Role = "Admin",
            IsActive = true
        };
        database.AppUsers.Add(adminUser);

        var customer = new Customer
        {
            Code = "1050",
            Username = "reza_hs",
            FullName = "رضا محمدی",
            Phone = "09123456789",
            Email = "reza@gamenet.local",
            IsVip = true,
            Balance = 450000m,
            Notes = "Gold VIP"
        };
        database.Customers.Add(customer);

        await database.SaveChangesAsync(cancellationToken);
        await EnsureCustomerProfilesAsync(database, cancellationToken);

        CreateStations(database, consoleType, hourlyTariff, "PS5", 10, "Zone A", "Console");
        CreateStations(database, consoleType, hourlyTariff, "PS4", 6, "Zone A", "Console");
        CreateStations(database, pcType, hourlyTariff, "PC", 40, "Zone B", "PC");
        CreateStations(database, tableType, hourlyTariff, "Table", 5, "Zone C", "Table");

        database.AuditLogs.Add(new AuditLog
        {
            Action = "Seed",
            EntityName = "GameNet",
            EntityId = "bootstrap",
            Details = "Initial reference data was created.",
            AppUserId = adminUser.Id
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    private static void CreateStations(GameNetDbContext database, StationType stationType, Tariff tariff, string prefix, int count, string zone, string type)
    {
        for (var number = 1; number <= count; number++)
        {
            database.Stations.Add(new Station
            {
                Name = $"{prefix}-{number:00}",
                Zone = zone,
                Type = type,
                StationTypeId = stationType.Id,
                TariffId = tariff.Id,
                RatePerHour = tariff.HourlyRate,
                State = StationState.Available,
                IsActive = true
            });
        }
    }


    private static async Task EnsureCustomerProfilesAsync(GameNetDbContext database, CancellationToken cancellationToken)
    {
        var profiles = new[]
        {
            new { Code = "1050", Username = "reza_hs", FullName = "رضا محمدی", Phone = "09123456789", Email = "reza@gamenet.local", IsVip = true, Balance = 450000m },
            new { Code = "2020", Username = "soroush.n", FullName = "سروش نیک‌پور", Phone = "09120000002", Email = "soroush@gamenet.local", IsVip = true, Balance = 120000m },
            new { Code = "2021", Username = "parsa.r", FullName = "پارسا رضایی", Phone = "09120000003", Email = "parsa@gamenet.local", IsVip = false, Balance = 0m },
            new { Code = "1051", Username = "mehdi.j", FullName = "مهدی جهان", Phone = "09120000004", Email = "mehdi@gamenet.local", IsVip = true, Balance = 470000m },
        };

        foreach (var profile in profiles)
        {
            var customer = await database.Customers.FirstOrDefaultAsync(item => item.Code == profile.Code || item.Phone == profile.Phone, cancellationToken);
            if (customer is null)
            {
                database.Customers.Add(new Customer
                {
                    Code = profile.Code,
                    Username = profile.Username,
                    FullName = profile.FullName,
                    Phone = profile.Phone,
                    Email = profile.Email,
                    IsVip = profile.IsVip,
                    Balance = profile.Balance,
                    Notes = profile.IsVip ? "VIP" : null,
                });
                continue;
            }

            customer.Code ??= profile.Code;
            customer.Username ??= profile.Username;
            if (string.IsNullOrWhiteSpace(customer.FullName)) customer.FullName = profile.FullName;
            customer.Phone ??= profile.Phone;
            customer.Email ??= profile.Email;
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}
