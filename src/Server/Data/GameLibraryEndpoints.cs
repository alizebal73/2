using GameNetManager.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public static class GameLibraryEndpointMapping
{
    public static void MapGameLibraryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/games", async (
            HttpContext context,
            GameNetDbContext database,
            string? q,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "game.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            var query = database.Games.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var search = q.Trim();
                query = query.Where(item =>
                    item.Name.Contains(search) ||
                    (item.Genre ?? "").Contains(search) ||
                    (item.Launcher ?? "").Contains(search));
            }

            var rows = await query.OrderBy(item => item.Name).ToListAsync(cancellationToken);
            return Results.Ok(rows.Select(ToGameDto).ToList());
        }).WithName("GetGames");

        app.MapGet("/api/games/{id:guid}", async (
            Guid id,
            HttpContext context,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "game.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            var game = await database.Games.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return game is null
                ? Results.NotFound(new { code = "game_not_found", message = "بازی پیدا نشد." })
                : Results.Ok(ToGameDto(game));
        }).WithName("GetGame");

        app.MapPost("/api/games", async (
            GameLibraryWriteRequest request,
            HttpContext context,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "game.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            var validation = ValidateGameRequest(request);
            if (validation is not null) return validation;

            var name = request.Name.Trim();
            if (await database.Games.AnyAsync(item => item.Name.ToLower() == name.ToLower(), cancellationToken))
                return Results.Conflict(new { code = "game_name_exists", message = "این بازی قبلاً ثبت شده است." });

            var game = new Game { Name = name };
            ApplyGameWrite(game, request);
            database.Games.Add(game);
            database.AuditLogs.Add(new AuditLog
            {
                Action = "GameCreated",
                EntityName = "Game",
                EntityId = game.Id.ToString(),
                AppUserId = auth.User!.Id,
                Details = "ایجاد بازی · " + game.Name
            });
            await database.SaveChangesAsync(cancellationToken);

            return Results.Created("/api/games/" + game.Id, ToGameDto(game));
        }).WithName("CreateGame");

        app.MapPut("/api/games/{id:guid}", async (
            Guid id,
            GameLibraryWriteRequest request,
            HttpContext context,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "game.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            var validation = ValidateGameRequest(request);
            if (validation is not null) return validation;

            var game = await database.Games.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (game is null) return Results.NotFound(new { code = "game_not_found", message = "بازی پیدا نشد." });

            var name = request.Name.Trim();
            if (await database.Games.AnyAsync(item => item.Id != id && item.Name.ToLower() == name.ToLower(), cancellationToken))
                return Results.Conflict(new { code = "game_name_exists", message = "این نام بازی قبلاً ثبت شده است." });

            ApplyGameWrite(game, request);
            database.AuditLogs.Add(new AuditLog
            {
                Action = "GameUpdated",
                EntityName = "Game",
                EntityId = game.Id.ToString(),
                AppUserId = auth.User!.Id,
                Details = "ویرایش بازی · " + game.Name
            });
            await database.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToGameDto(game));
        }).WithName("UpdateGame");

        app.MapDelete("/api/games/{id:guid}", async (
            Guid id,
            HttpContext context,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "game.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            var game = await database.Games.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (game is null) return Results.NotFound(new { code = "game_not_found", message = "بازی پیدا نشد." });

            game.IsActive = false;
            database.AuditLogs.Add(new AuditLog
            {
                Action = "GameDeactivated",
                EntityName = "Game",
                EntityId = game.Id.ToString(),
                AppUserId = auth.User!.Id,
                Details = "غیرفعال‌سازی بازی · " + game.Name
            });
            await database.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToGameDto(game));
        }).WithName("DeleteGame");

        app.MapGet("/api/game-accounts", async (
            HttpContext context,
            GameNetDbContext database,
            string? q,
            string? platform,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "account.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            var query = database.GameAccountPoolEntries
                .AsNoTracking()
                .Include(item => item.AllowedGames).ThenInclude(item => item.Game)
                .Include(item => item.Leases).ThenInclude(item => item.Game)
                .Include(item => item.Leases).ThenInclude(item => item.AgentDevice)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(platform))
            {
                var value = platform.Trim();
                query = query.Where(item => item.Platform == value);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var search = q.Trim();
                query = query.Where(item =>
                    item.AccountName.Contains(search) ||
                    item.Platform.Contains(search) ||
                    (item.Login ?? "").Contains(search));
            }

            var accounts = await query
                .OrderBy(item => item.Platform)
                .ThenBy(item => item.AccountName)
                .ToListAsync(cancellationToken);

            return Results.Ok(accounts.Select(ToAccountDto).ToList());
        }).WithName("GetGameAccounts");

        app.MapPost("/api/game-accounts", async (
            GameAccountPoolWriteRequest request,
            HttpContext context,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "account.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            var validation = ValidateAccountRequest(request);
            if (validation is not null) return validation;

            var title = request.Title.Trim();
            if (await database.GameAccountPoolEntries.AnyAsync(item => item.AccountName.ToLower() == title.ToLower(), cancellationToken))
                return Results.Conflict(new { code = "account_name_exists", message = "این اکانت قبلاً در استخر ثبت شده است." });

            var allowedGameIds = request.AllowedGameIds?.Distinct().ToArray() ?? [];
            var games = await database.Games
                .Where(item => allowedGameIds.Contains(item.Id) && item.IsActive)
                .ToListAsync(cancellationToken);

            if (games.Count != allowedGameIds.Length)
                return Results.BadRequest(new { code = "invalid_games", message = "یکی از بازی‌های انتخاب‌شده معتبر یا فعال نیست." });

            var account = new GameAccountPoolEntry
            {
                AccountName = title,
                Platform = request.Platform.Trim(),
                Launcher = Clean(request.Launcher),
                Login = Clean(request.Login),
                PasswordHash = string.IsNullOrWhiteSpace(request.Password) ? null : PasswordSecurity.Hash(request.Password),
                Owner = string.IsNullOrWhiteSpace(request.Owner) ? "مجموعه" : request.Owner.Trim(),
                ExpiresAt = request.ExpiresAt,
                GuardStatus = string.IsNullOrWhiteSpace(request.GuardStatus) ? "محافظت‌شده" : request.GuardStatus.Trim(),
                IsActive = request.Active,
                Status = GameAccountPoolStatus.Free
            };

            foreach (var game in games)
                account.AllowedGames.Add(new GameAccountAllowedGame { GameId = game.Id, Game = game });

            database.GameAccountPoolEntries.Add(account);
            database.AuditLogs.Add(new AuditLog
            {
                Action = "GameAccountCreated",
                EntityName = "GameAccountPoolEntry",
                EntityId = account.Id.ToString(),
                AppUserId = auth.User!.Id,
                Details = "ایجاد اکانت استخر · " + account.AccountName
            });
            await database.SaveChangesAsync(cancellationToken);

            return Results.Created("/api/game-accounts/" + account.Id, ToAccountDto(account));
        }).WithName("CreateGameAccount");

        app.MapPut("/api/game-accounts/{id:guid}", async (
            Guid id,
            GameAccountPoolWriteRequest request,
            HttpContext context,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "account.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            var validation = ValidateAccountRequest(request);
            if (validation is not null) return validation;

            var account = await database.GameAccountPoolEntries
                .Include(item => item.AllowedGames)
                .Include(item => item.Leases)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (account is null)
                return Results.NotFound(new { code = "account_not_found", message = "اکانت پیدا نشد." });

            var title = request.Title.Trim();
            if (await database.GameAccountPoolEntries.AnyAsync(item => item.Id != id && item.AccountName.ToLower() == title.ToLower(), cancellationToken))
                return Results.Conflict(new { code = "account_name_exists", message = "این نام اکانت قبلاً ثبت شده است." });

            var allowedGameIds = request.AllowedGameIds?.Distinct().ToArray() ?? [];
            var games = await database.Games
                .Where(item => allowedGameIds.Contains(item.Id) && item.IsActive)
                .ToListAsync(cancellationToken);

            if (games.Count != allowedGameIds.Length)
                return Results.BadRequest(new { code = "invalid_games", message = "یکی از بازی‌های انتخاب‌شده معتبر یا فعال نیست." });

            var activeLease = account.Leases.FirstOrDefault(item => item.ReleasedAt == null && item.Status == GameAccountLeaseStatus.Active);
            if (activeLease is not null && !allowedGameIds.Contains(activeLease.GameId))
                return Results.Conflict(new { code = "leased_game_required", message = "بازی فعال اکانت را نمی‌توان از فهرست مجاز حذف کرد." });

            account.AccountName = title;
            account.Platform = request.Platform.Trim();
            account.Launcher = Clean(request.Launcher);
            account.Login = Clean(request.Login);
            if (!string.IsNullOrWhiteSpace(request.Password))
                account.PasswordHash = PasswordSecurity.Hash(request.Password);
            account.Owner = string.IsNullOrWhiteSpace(request.Owner) ? "مجموعه" : request.Owner.Trim();
            account.ExpiresAt = request.ExpiresAt;
            account.GuardStatus = string.IsNullOrWhiteSpace(request.GuardStatus) ? "محافظت‌شده" : request.GuardStatus.Trim();
            account.IsActive = request.Active;

            account.AllowedGames.Clear();
            foreach (var game in games)
                account.AllowedGames.Add(new GameAccountAllowedGame { GameAccountPoolEntryId = account.Id, GameId = game.Id });

            database.AuditLogs.Add(new AuditLog
            {
                Action = "GameAccountUpdated",
                EntityName = "GameAccountPoolEntry",
                EntityId = account.Id.ToString(),
                AppUserId = auth.User!.Id,
                Details = "ویرایش اکانت استخر · " + account.AccountName
            });
            await database.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToAccountDto(account));
        }).WithName("UpdateGameAccount");

        app.MapPost("/api/game-accounts/{id:guid}/lock", async (
            Guid id,
            HttpContext context,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "account.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            var account = await database.GameAccountPoolEntries.Include(item => item.Leases)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (account is null) return Results.NotFound(new { code = "account_not_found", message = "اکانت پیدا نشد." });
            if (account.Leases.Any(item => item.ReleasedAt == null && item.Status == GameAccountLeaseStatus.Active))
                return Results.Conflict(new { code = "account_in_use", message = "اکانت در حال استفاده است و ابتدا باید آزاد شود." });

            account.Status = GameAccountPoolStatus.Locked;
            database.AuditLogs.Add(new AuditLog
            {
                Action = "GameAccountLocked",
                EntityName = "GameAccountPoolEntry",
                EntityId = id.ToString(),
                AppUserId = auth.User!.Id,
                Details = "قفل اکانت · " + account.AccountName
            });
            await database.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToAccountDto(account));
        }).WithName("LockGameAccount");

        app.MapPost("/api/game-accounts/{id:guid}/unlock", async (
            Guid id,
            HttpContext context,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "account.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            var account = await database.GameAccountPoolEntries.Include(item => item.Leases)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (account is null) return Results.NotFound(new { code = "account_not_found", message = "اکانت پیدا نشد." });
            if (account.Leases.Any(item => item.ReleasedAt == null && item.Status == GameAccountLeaseStatus.Active))
                return Results.Conflict(new { code = "account_in_use", message = "اکانت در حال استفاده است و نمی‌تواند رفع قفل شود." });

            account.Status = GameAccountPoolStatus.Free;
            account.IsActive = true;
            database.AuditLogs.Add(new AuditLog
            {
                Action = "GameAccountUnlocked",
                EntityName = "GameAccountPoolEntry",
                EntityId = id.ToString(),
                AppUserId = auth.User!.Id,
                Details = "رفع قفل اکانت · " + account.AccountName
            });
            await database.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToAccountDto(account));
        }).WithName("UnlockGameAccount");

        app.MapPost("/api/game-accounts/{id:guid}/lease", async (
            Guid id,
            GameAccountLeaseRequest request,
            HttpContext context,
            GameNetDbContext database,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "account.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            if (!AuthorizationService.HasPermission(auth.User!, "client.control"))
                return Results.Json(
                    new { code = "permission_denied", message = "برای تخصیص اکانت به کلاینت، دسترسی کنترل کلاینت لازم است." },
                    statusCode: StatusCodes.Status403Forbidden);

            var account = await database.GameAccountPoolEntries
                .AsNoTracking()
                .Include(item => item.AllowedGames)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (account is null) return Results.NotFound(new { code = "account_not_found", message = "اکانت پیدا نشد." });
            if (!account.IsActive || account.Status != GameAccountPoolStatus.Free)
                return Results.Conflict(new { code = "account_not_free", message = "اکانت برای تخصیص آزاد نیست." });
            if (!account.AllowedGames.Any(item => item.GameId == request.GameId))
                return Results.BadRequest(new { code = "game_not_allowed", message = "این بازی برای این اکانت مجاز نیست." });

            var game = await database.Games.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.GameId && item.IsActive, cancellationToken);
            if (game is null) return Results.BadRequest(new { code = "game_not_found", message = "بازی انتخاب‌شده فعال نیست." });

            var device = await database.AgentDevices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.AgentDeviceId && item.IsActive, cancellationToken);
            if (device is null) return Results.BadRequest(new { code = "agent_not_found", message = "کلاینت انتخاب‌شده پیدا نشد." });

            var offlineAfter = Math.Clamp(configuration.GetValue("Agent:OfflineAfterSeconds", 30), 6, 300);
            if (!device.LastSeenAt.HasValue || DateTimeOffset.UtcNow - device.LastSeenAt.Value > TimeSpan.FromSeconds(offlineAfter))
                return Results.Conflict(new { code = "agent_offline", message = "کلاینت انتخاب‌شده آفلاین است." });

            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var claimed = await database.GameAccountPoolEntries
                .Where(item => item.Id == id && item.IsActive && item.Status == GameAccountPoolStatus.Free)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, GameAccountPoolStatus.InUse), cancellationToken);

            if (claimed != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Results.Conflict(new { code = "account_race", message = "اکانت همزمان توسط عملیات دیگری تخصیص داده شد." });
            }

            var lease = new GameAccountLease
            {
                GameAccountPoolEntryId = id,
                GameId = request.GameId,
                AgentDeviceId = request.AgentDeviceId,
                CustomerId = request.CustomerId,
                Status = GameAccountLeaseStatus.Active,
                LeasedAt = DateTimeOffset.UtcNow
            };
            database.GameAccountLeases.Add(lease);
            database.AuditLogs.Add(new AuditLog
            {
                Action = "GameAccountLeased",
                EntityName = "GameAccountLease",
                EntityId = lease.Id.ToString(),
                AppUserId = auth.User!.Id,
                Details = "تخصیص " + account.AccountName + " · " + game.Name + " · " + device.Name
            });
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var refreshed = await database.GameAccountPoolEntries
                .AsNoTracking()
                .Include(item => item.AllowedGames).ThenInclude(item => item.Game)
                .Include(item => item.Leases).ThenInclude(item => item.Game)
                .Include(item => item.Leases).ThenInclude(item => item.AgentDevice)
                .FirstAsync(item => item.Id == id, cancellationToken);

            return Results.Ok(ToAccountDto(refreshed));
        }).WithName("LeaseGameAccount");

        app.MapPost("/api/game-accounts/{id:guid}/release", async (
            Guid id,
            HttpContext context,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "account.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            var account = await database.GameAccountPoolEntries.Include(item => item.Leases)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (account is null) return Results.NotFound(new { code = "account_not_found", message = "اکانت پیدا نشد." });

            var lease = account.Leases.FirstOrDefault(item => item.ReleasedAt == null && item.Status == GameAccountLeaseStatus.Active);
            if (lease is null) return Results.Conflict(new { code = "account_not_leased", message = "اکانت در حال استفاده نیست." });

            lease.Status = GameAccountLeaseStatus.Released;
            lease.ReleasedAt = DateTimeOffset.UtcNow;
            lease.ReleaseReason = "آزادسازی توسط اپراتور";
            account.Status = GameAccountPoolStatus.Free;

            database.AuditLogs.Add(new AuditLog
            {
                Action = "GameAccountReleased",
                EntityName = "GameAccountLease",
                EntityId = lease.Id.ToString(),
                AppUserId = auth.User!.Id,
                Details = "آزادسازی اکانت · " + account.AccountName
            });
            await database.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToAccountDto(account));
        }).WithName("ReleaseGameAccount");

        app.MapGet("/api/game-accounts/logs", async (
            HttpContext context,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "account.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            var rows = await database.AuditLogs
                .AsNoTracking()
                .Include(item => item.AppUser)
                .Where(item => item.EntityName == "GameAccountPoolEntry" || item.EntityName == "GameAccountLease")
                .OrderByDescending(item => item.CreatedAt)
                .Take(100)
                .Select(item => new GameAccountPoolAuditDto(
                    item.Id,
                    item.CreatedAt,
                    item.Action,
                    item.Details,
                    item.AppUser != null ? item.AppUser.FullName : null))
                .ToListAsync(cancellationToken);

            return Results.Ok(rows);
        }).WithName("GetGameAccountLogs");
    }

    private static IResult? ValidateGameRequest(GameLibraryWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { code = "missing_game_name", message = "نام بازی الزامی است." });
        if (request.Name.Trim().Length > 120)
            return Results.BadRequest(new { code = "game_name_too_long", message = "نام بازی بیش از حد مجاز است." });

        if ((request.Version?.Length ?? 0) > 60 ||
            (request.Launcher?.Length ?? 0) > 40 ||
            (request.InstallPath?.Length ?? 0) > 500 ||
            (request.ExecutablePath?.Length ?? 0) > 260 ||
            (request.LaunchArguments?.Length ?? 0) > 1000 ||
            (request.ConnectionType?.Length ?? 0) > 30 ||
            (request.TargetSystem?.Length ?? 0) > 30 ||
            (request.TargetZone?.Length ?? 0) > 30 ||
            (request.TargetScope?.Length ?? 0) > 20 ||
            (request.TargetStations?.Length ?? 0) > 1000 ||
            (request.ProcessNames?.Length ?? 0) > 500 ||
            (request.CoverPath?.Length ?? 0) > 500 ||
            (request.TrailerPath?.Length ?? 0) > 500)
            return Results.BadRequest(new { code = "game_field_too_long", message = "یکی از مقادیر بازی بیش از حد مجاز است." });

        return null;
    }

    private static IResult? ValidateAccountRequest(GameAccountPoolWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(new { code = "missing_account_name", message = "نام اکانت الزامی است." });
        if (string.IsNullOrWhiteSpace(request.Platform))
            return Results.BadRequest(new { code = "missing_platform", message = "پلتفرم اکانت الزامی است." });
        if (request.Title.Trim().Length > 120 ||
            request.Platform.Trim().Length > 40 ||
            (request.Launcher?.Length ?? 0) > 60 ||
            (request.Login?.Length ?? 0) > 120 ||
            (request.Owner?.Length ?? 0) > 120 ||
            (request.GuardStatus?.Length ?? 0) > 40)
            return Results.BadRequest(new { code = "account_field_too_long", message = "یکی از مقادیر اکانت بیش از حد مجاز است." });

        return null;
    }

    private static void ApplyGameWrite(Game game, GameLibraryWriteRequest request)
    {
        game.Name = request.Name.Trim();
        game.Genre = string.IsNullOrWhiteSpace(request.Category) ? "سایر" : request.Category.Trim();
        game.Version = Clean(request.Version);
        game.Launcher = Clean(request.Launcher);
        game.InstallPath = Clean(request.InstallPath);
        game.ExecutablePath = Clean(request.ExecutablePath);
        game.LaunchArguments = Clean(request.LaunchArguments);
        game.ConnectionType = Clean(request.ConnectionType) ?? "برنامه";
        game.TargetSystem = Clean(request.TargetSystem) ?? "all";
        game.TargetZone = Clean(request.TargetZone) ?? "pc";
        game.TargetScope = Clean(request.TargetScope) ?? "all";
        game.TargetStations = Clean(request.TargetStations);
        game.ProcessNames = Clean(request.ProcessNames);
        game.CoverPath = Clean(request.CoverPath);
        game.TrailerPath = Clean(request.TrailerPath);
        game.IsActive = request.Active;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static GameLibraryDto ToGameDto(Game game)
    {
        var status = !game.IsActive
            ? "offline"
            : game.ConnectionType?.Trim().ToLowerInvariant() switch
            {
                "آنلاین" or "online" => "online",
                "آفلاین" or "offline" => "offline",
                _ => "program"
            };

        return new GameLibraryDto(
            game.Id,
            game.Name,
            game.Version,
            game.Genre ?? "سایر",
            game.Launcher,
            game.InstallPath,
            game.ExecutablePath,
            game.LaunchArguments,
            game.ConnectionType,
            game.TargetSystem,
            game.TargetZone,
            game.TargetScope,
            game.TargetStations,
            game.ProcessNames,
            game.CoverPath,
            game.TrailerPath,
            game.IsActive,
            status,
            0);
    }

    private static GameAccountPoolDto ToAccountDto(GameAccountPoolEntry account)
    {
        var lease = account.Leases.FirstOrDefault(item =>
            item.ReleasedAt == null && item.Status == GameAccountLeaseStatus.Active);

        var allowed = account.AllowedGames
            .Where(item => item.Game is not null)
            .Select(item => new GameLibraryOptionDto(item.GameId, item.Game.Name))
            .OrderBy(item => item.Name)
            .ToArray();

        return new GameAccountPoolDto(
            account.Id,
            account.AccountName,
            account.Platform,
            account.Launcher,
            account.Login,
            account.Status switch
            {
                GameAccountPoolStatus.InUse => "in-use",
                GameAccountPoolStatus.Locked => "locked",
                _ => "free"
            },
            account.Owner,
            account.ExpiresAt,
            allowed.Select(item => item.Id.ToString()).ToArray(),
            allowed,
            lease?.AgentDeviceId,
            lease?.AgentDevice?.Name,
            lease?.GameId,
            lease?.Game?.Name,
            account.GuardStatus,
            account.IsActive);
    }
}
