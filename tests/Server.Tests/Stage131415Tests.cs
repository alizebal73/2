using GameNetManager.Server.Data;
using GameNetManager.Shared.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class Stage131415Tests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public Stage131415Tests() => _connection.Open();

    [Fact]
    public async Task ReservationServiceRejectsOverlapAndSupportsWaitlistAssignment()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        Guid appUserId;
        Guid customerId;
        Guid stationId;

        await using (var db = new GameNetDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var user = new AppUser
            {
                FullName = "Stage14 Operator",
                UserName = "stage14",
                Email = "stage14@test.local",
                PasswordHash = "x",
                Role = "Owner"
            };
            var type = new StationType { Name = "Stage14 PC" };
            var customer = new Customer
            {
                FullName = "Stage14 Customer",
                Code = "S14-001",
                Username = "s14",
                ConcurrentLoginLimit = 1,
                VipTier = "none"
            };
            var station = new Station
            {
                Name = "S14-PC-01",
                Zone = "Stage14",
                Type = "PC",
                StationType = type,
                State = StationState.Available,
                IsActive = true,
                NetworkRoute = "internet2"
            };

            db.AppUsers.Add(user);
            db.StationTypes.Add(type);
            db.Customers.Add(customer);
            db.Stations.Add(station);
            await db.SaveChangesAsync();

            appUserId = user.Id;
            customerId = customer.Id;
            stationId = station.Id;
        }

        await using (var db = new GameNetDbContext(options))
        {
            var service = new ReservationService(db);
            var start = DateTimeOffset.UtcNow.AddHours(1);

            await service.CreateAsync(
                new ReservationCreateRequest(customerId, stationId, start, 120),
                appUserId,
                CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateAsync(
                    new ReservationCreateRequest(customerId, stationId, start.AddMinutes(30), 60),
                    appUserId,
                    CancellationToken.None));

            var waitlist = await service.CreateAsync(
                new ReservationCreateRequest(customerId, stationId, start.AddDays(1), 60, "waitlist", 10),
                appUserId,
                CancellationToken.None);

            var assigned = await service.TransitionAsync(
                waitlist.Id,
                new ReservationTransitionRequest("assign", stationId),
                appUserId,
                CancellationToken.None);

            Assert.Equal("Reservation", assigned.Kind);
            Assert.Equal("Confirmed", assigned.Status);
        }
    }

    [Fact]
    public async Task EventServiceEnforcesCapacityAndTracksParticipant()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        Guid appUserId;
        Guid firstCustomerId;
        Guid secondCustomerId;

        await using (var db = new GameNetDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var user = new AppUser
            {
                FullName = "Stage15 Event Operator",
                UserName = "stage15",
                Email = "stage15@test.local",
                PasswordHash = "x",
                Role = "Owner"
            };
            var first = new Customer
            {
                FullName = "Event First",
                Code = "EV-001",
                Username = "ev1",
                ConcurrentLoginLimit = 1,
                VipTier = "none"
            };
            var second = new Customer
            {
                FullName = "Event Second",
                Code = "EV-002",
                Username = "ev2",
                ConcurrentLoginLimit = 1,
                VipTier = "none"
            };

            db.AppUsers.Add(user);
            db.Customers.AddRange(first, second);
            await db.SaveChangesAsync();

            appUserId = user.Id;
            firstCustomerId = first.Id;
            secondCustomerId = second.Id;
        }

        await using (var db = new GameNetDbContext(options))
        {
            var service = new EventService(db);
            var eventRow = await service.CreateAsync(
                new EventCreateRequest(
                    "Stage14 Tournament",
                    "tournament",
                    DateTimeOffset.UtcNow,
                    180,
                    1),
                appUserId,
                CancellationToken.None);

            var participant = await service.AddParticipantAsync(
                eventRow.Id,
                new EventParticipantRequest(firstCustomerId, 1),
                appUserId,
                CancellationToken.None);

            Assert.Equal(1, participant.Seed);
            Assert.Single(await service.ListParticipantsAsync(eventRow.Id, CancellationToken.None));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AddParticipantAsync(
                    eventRow.Id,
                    new EventParticipantRequest(secondCustomerId, 2),
                    appUserId,
                    CancellationToken.None));

            var running = await service.TransitionAsync(
                eventRow.Id,
                "start",
                appUserId,
                CancellationToken.None);

            Assert.Equal("Running", running.Status);
        }
    }

    [Fact]
    public async Task ReportingServiceAggregatesServerFinancialTruth()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using (var db = new GameNetDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var user = new AppUser
            {
                FullName = "Report Operator",
                UserName = "report",
                Email = "report@test.local",
                PasswordHash = "x",
                Role = "Owner"
            };
            var customer = new Customer
            {
                FullName = "Report Customer",
                Code = "REP-001",
                Username = "rep1",
                ConcurrentLoginLimit = 1,
                VipTier = "none"
            };
            db.AppUsers.Add(user);
            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            db.Invoices.Add(new Invoice
            {
                CustomerId = customer.Id,
                AppUserId = user.Id,
                TotalAmount = 250000m,
                Status = InvoiceStatus.Paid,
                IssuedAt = DateTimeOffset.UtcNow
            });

            var shift = new Shift
            {
                AppUserId = user.Id,
                OpenAt = DateTimeOffset.UtcNow.AddHours(-2),
                CashOpening = 0
            };
            db.Shifts.Add(shift);
            await db.SaveChangesAsync();

            db.Expenses.Add(new Expense
            {
                ShiftId = shift.Id,
                Category = "Stage15",
                Amount = 50000m,
                Description = "test"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new GameNetDbContext(options))
        {
            var service = new ReportingService(db);
            var result = await service.GetSummaryAsync(
                DateTimeOffset.UtcNow.AddHours(-1),
                DateTimeOffset.UtcNow.AddHours(1),
                CancellationToken.None);

            Assert.Equal(250000m, result.Revenue);
            Assert.Equal(50000m, result.Expense);
            Assert.Equal(200000m, result.OperatingProfit);
            Assert.Equal(1, result.PaidInvoices);
        }
    }

    public void Dispose() => _connection.Dispose();
}
