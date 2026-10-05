using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace GameNetManager.Server.Data;

public static class AuthorizationService
{
    public const string SessionCookieName = "gamenet_session";
    public const int SessionHours = 12;

    public static readonly IReadOnlyDictionary<string, string> PermissionCatalog =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["session.start"] = "شروع و پایان جلسه",
            ["session.manage"] = "توقف، ادامه، تمدید و تغییر جلسه",
            ["session.settle"] = "تسویه جلسه",
            ["customer.manage"] = "مدیریت مشتریان",
            ["customer.wallet"] = "شارژ و عملیات کیف پول",
            ["customer.debt"] = "ثبت و تسویه بدهی",
            ["buffet.sell"] = "فروش بوفه",
            ["buffet.inventory"] = "مدیریت انبار",
            ["finance.view"] = "مشاهده گزارش مالی",
            ["finance.manage"] = "ثبت و مدیریت هزینه",
            ["shift.manage"] = "باز و بسته کردن شیفت",
            ["tariff.view"] = "مشاهده تعرفه‌ها",
            ["tariff.manage"] = "مدیریت تعرفه",
            ["game.manage"] = "مدیریت بازی",
            ["account.manage"] = "مدیریت Account Pool",
            ["client.control"] = "کنترل کلاینت",
            ["client.power"] = "راه‌اندازی مجدد و خاموش کردن کلاینت",
            ["user.manage"] = "مدیریت کاربران و دسترسی",
            ["approval.decide"] = "تأیید/رد عملیات حساس",
            ["payroll.view"] = "مشاهده اطلاعات حقوق و حساب پرسنلی",
            ["payroll.manage"] = "ثبت و مدیریت حقوق و حساب پرسنلی",
            ["audit.view"] = "مشاهده Audit",
            ["report.sessions.view"] = "مشاهده گزارش جلسات و ایستگاه‌ها",
            ["report.sessions.scope.all"] = "مشاهده تمام جلسات و ایستگاه‌ها",
            ["report.customers.view"] = "مشاهده گزارش مشتری و VIP",
            ["report.customers.scope.all"] = "مشاهده تمام مشتریان و VIP",
            ["report.users-shift.view"] = "مشاهده گزارش کاربران و شیفت",
            ["report.users-shift.scope.all"] = "مشاهده تمام کاربران و شیفت‌ها",
            ["report.audit.view"] = "مشاهده گزارش Audit",
            ["report.audit.scope.all"] = "مشاهده تمام رویدادهای Audit",
            ["reports.export"] = "خروجی گرفتن از گزارش‌های مجاز"
        };

    public static async Task<AppUser?> ResolveUserAsync(
        HttpContext context,
        GameNetDbContext database,
        CancellationToken cancellationToken)
    {
        var token = context.Request.Cookies[SessionCookieName];
        if (string.IsNullOrWhiteSpace(token))
        {
            var header = context.Request.Headers.Authorization.ToString();
            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                token = header["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(token))
            return null;

        var tokenHash = PasswordSecurity.HashToken(token);
        var sessions = await database.AppUserSessions
            .Include(item => item.AppUser)
            .ThenInclude(item => item.Permissions)
            .ThenInclude(item => item.Permission)
            .Where(item => item.TokenHash == tokenHash)
            .ToListAsync(cancellationToken);

        var session = sessions.FirstOrDefault(item =>
            item.RevokedAt == null
            && item.ExpiresAt > DateTimeOffset.UtcNow
            && item.AppUser.IsActive);

        return session?.AppUser;
    }

    public static async Task<(AppUser? User, IResult? Error)> RequireAuthenticatedAsync(
        HttpContext context,
        GameNetDbContext database,
        CancellationToken cancellationToken)
    {
        var user = await ResolveUserAsync(context, database, cancellationToken);
        return user is null
            ? (null, Results.Unauthorized())
            : (user, null);
    }

    public static bool HasPermission(AppUser user, string permission)
    {
        if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Role, "Owner", StringComparison.OrdinalIgnoreCase))
            return true;

        return user.Permissions.Any(item => string.Equals(item.Permission.Name, permission, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<(AppUser? User, IResult? Error)> RequireAnyPermissionAsync(
        HttpContext context,
        GameNetDbContext database,
        CancellationToken cancellationToken,
        params string[] permissions)
    {
        var user = await ResolveUserAsync(context, database, cancellationToken);
        if (user is null)
            return (null, Results.Unauthorized());

        if (!permissions.Any(permission => HasPermission(user, permission)))
            return (user, Results.Json(
                new { code = "permission_denied", message = "دسترسی لازم برای این عملیات را ندارید." },
                statusCode: StatusCodes.Status403Forbidden));

        return (user, null);
    }

    public static async Task<(AppUser? User, IResult? Error)> RequirePermissionAsync(
        HttpContext context,
        GameNetDbContext database,
        string permission,
        CancellationToken cancellationToken)
    {
        var user = await ResolveUserAsync(context, database, cancellationToken);
        if (user is null)
            return (null, Results.Unauthorized());

        if (!HasPermission(user, permission))
            return (user, Results.Json(
                new { code = "permission_denied", message = "دسترسی لازم برای این عملیات را ندارید." },
                statusCode: StatusCodes.Status403Forbidden));

        return (user, null);
    }

    public static async Task<(AppUser? User, IResult? Error)> RequireReportPermissionAsync(
        HttpContext context,
        GameNetDbContext database,
        string reportKey,
        CancellationToken cancellationToken,
        params string[] legacyPermissions)
    {
        var user = await ResolveUserAsync(context, database, cancellationToken);
        if (user is null)
            return (null, Results.Unauthorized());

        var permission = $"report.{reportKey}.view";
        var allowed = HasPermission(user, permission)
            || legacyPermissions.Any(item => HasPermission(user, item));

        if (!allowed)
            return (user, Results.Json(
                new { code = "permission_denied", message = "دسترسی لازم برای مشاهده این گزارش را ندارید." },
                statusCode: StatusCodes.Status403Forbidden));

        return (user, null);
    }

    public static bool HasReportAllScope(AppUser user, string reportKey)
        => IsGlobalUser(user)
            || HasPermission(user, $"report.{reportKey}.scope.all");

    public static bool HasReportExport(AppUser user)
        => IsGlobalUser(user) || HasPermission(user, "reports.export");

    private static bool IsGlobalUser(AppUser user)
        => string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Role, "Owner", StringComparison.OrdinalIgnoreCase);

    public static string CreateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
