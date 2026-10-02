using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class ConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ConcurrencyTests() => _connection.Open();

    [Fact]
    public async Task ConcurrentUpdatesToSameEntityAreRejected()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using (var seed = new GameNetDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Customers.Add(new Customer
            {
                FullName = "مشتری تعارض",
                Code = "CONFLICT-1",
                Username = "conflict-user",
                ConcurrentLoginLimit = 1,
                VipTier = "none"
            });
            await seed.SaveChangesAsync();
        }

        Customer first;
        Customer second;
        await using (var db1 = new GameNetDbContext(options))
        await using (var db2 = new GameNetDbContext(options))
        {
            first = await db1.Customers.SingleAsync(item => item.Code == "CONFLICT-1");
            second = await db2.Customers.SingleAsync(item => item.Code == "CONFLICT-1");

            first.Notes = "اپراتور اول";
            await db1.SaveChangesAsync();

            Assert.NotNull(first.UpdatedAt);
            second.Notes = "اپراتور دوم";

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                () => db2.SaveChangesAsync());

            var saved = await db1.Customers
                .AsNoTracking()
                .SingleAsync(item => item.Code == "CONFLICT-1");
            Assert.Equal("اپراتور اول", saved.Notes);
        }
    }

    public void Dispose() => _connection.Dispose();
}
