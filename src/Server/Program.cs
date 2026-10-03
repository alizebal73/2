    SaveAccountPoolEntryRequest request,
    HttpContext context,
    GameNetDbContext database,
    GameCredentialProtectionService credentialProtection,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "account.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;
    if (string.IsNullOrWhiteSpace(request.Title))
        return Results.BadRequest(new { code = "account_title_required", message = "نام اکانت الزامی است." });

    if (!Enum.TryParse<AccountPoolStatus>(request.Status, true, out var requestedStatus)
        || requestedStatus == AccountPoolStatus.InUse)
        return Results.BadRequest(new { code = "invalid_account_status", message = "اکانت فقط می‌تواند آزاد یا قفل باشد؛ وضعیت در حال استفاده فقط از طریق Lease ایجاد می‌شود." });

    var gameIds = request.AllowedGameIds.Distinct().ToArray();
    if (gameIds.Length > 0)
    {
        var count = await database.Games.CountAsync(item => gameIds.Contains(item.Id) && item.IsActive, cancellationToken);
        if (count != gameIds.Length)
            return Results.BadRequest(new { code = "invalid_games", message = "یکی از بازی‌های انتخاب‌شده معتبر نیست." });
    }

    var account = new AccountPoolEntry
    {
        Title = request.Title.Trim(),
        Platform = request.Platform.Trim(),
        Login = request.Login?.Trim(),
        SecretHash = string.IsNullOrWhiteSpace(request.Secret) ? null : PasswordSecurity.Hash(request.Secret),
        SecretCiphertext = string.IsNullOrWhiteSpace(request.Secret) ? null : credentialProtection.Protect(request.Secret),
        Owner = string.IsNullOrWhiteSpace(request.Owner) ? "مجموعه" : request.Owner.Trim(),
        ExpiresAt = request.ExpiresAt?.UtcDateTime,
        AllowedGameIdsCsv = string.Join(",", gameIds),
        Status = requestedStatus,
        IsActive = true
    };

    database.AccountPoolEntries.Add(account);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "AccountPoolCreated",
        EntityName = "AccountPoolEntry",
        EntityId = account.Id.ToString(),
        AppUserId = auth.User!.Id,
        Details = "ثبت اکانت استخر · " + account.Title
    });
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { id = account.Id });
}).WithName("CreateAccountPoolEntry");

app.MapPut("/api/account-pool/{accountId:guid}", async (
    Guid accountId,
    SaveAccountPoolEntryRequest request,
    HttpContext context,
    GameNetDbContext database,
    GameCredentialProtectionService credentialProtection,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "account.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var account = await database.AccountPoolEntries.FirstOrDefaultAsync(item => item.Id == accountId && item.IsActive, cancellationToken);
    if (account is null) return Results.NotFound(new { code = "account_not_found", message = "اکانت استخر پیدا نشد." });

    if (!Enum.TryParse<AccountPoolStatus>(request.Status, true, out var requestedStatus))
        return Results.BadRequest(new { code = "invalid_account_status", message = "وضعیت اکانت معتبر نیست." });

    if (requestedStatus == AccountPoolStatus.InUse)
    {
        if (account.Status != AccountPoolStatus.InUse)
            return Results.Conflict(new { code = "account_in_use_requires_lease", message = "اکانت فقط از طریق تخصیص Lease می‌تواند در وضعیت استفاده قرار بگیرد." });
    }
    else if (account.Status == AccountPoolStatus.InUse)
    {
        return Results.Conflict(new { code = "account_in_use", message = "اکانت در حال استفاده است و ابتدا باید Lease آن آزاد شود." });
    }

    var gameIds = request.AllowedGameIds.Distinct().ToArray();
    var count = await database.Games.CountAsync(item => gameIds.Contains(item.Id) && item.IsActive, cancellationToken);
    if (count != gameIds.Length)
        return Results.BadRequest(new { code = "invalid_games", message = "یکی از بازی‌های انتخاب‌شده معتبر نیست." });

    account.Title = request.Title.Trim();
    account.Platform = request.Platform.Trim();
    account.Login = request.Login?.Trim();
    if (!string.IsNullOrWhiteSpace(request.Secret))
    {
        account.SecretHash = PasswordSecurity.Hash(request.Secret);
        account.SecretCiphertext = credentialProtection.Protect(request.Secret);
    }
    account.Owner = string.IsNullOrWhiteSpace(request.Owner) ? "مجموعه" : request.Owner.Trim();
    account.ExpiresAt = request.ExpiresAt?.UtcDateTime;
    account.AllowedGameIdsCsv = string.Join(",", gameIds);
    account.Status = requestedStatus;

    database.AuditLogs.Add(new AuditLog
    {
        Action = "AccountPoolUpdated",
        EntityName = "AccountPoolEntry",
        EntityId = account.Id.ToString(),
        AppUserId = auth.User!.Id,
        Details = "ویرایش اکانت استخر · " + account.Title
    });
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { updated = true });
}).WithName("UpdateAccountPoolEntry");

app.MapPost("/api/account-pool/{accountId:guid}/unlock", async (
    Guid accountId,