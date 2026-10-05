using System.Text.Json;

namespace GameNetManager.Server.Data;

public sealed record ServerSettingDefinition(
    string Key,
    string Title,
    string Category,
    string Type,
    object DefaultValue);

public static class ServerSettingsCatalog
{
    public const string GlobalScope = "global";

    public static readonly IReadOnlyList<ServerSettingDefinition> Definitions =
        new List<ServerSettingDefinition>
        {
            new("viewMode", "حالت نمایش داشبورد", "داشبورد و نمایش", "string", "v-card"),
            new("zones", "زون‌بندی گرید", "داشبورد و نمایش", "boolean", true),
            new("liveCost", "نمایش هزینه لحظه‌ای", "داشبورد و نمایش", "boolean", true),
            new("progress", "نمایش نوار پیشرفت", "داشبورد و نمایش", "boolean", true),
            new("largeFont", "فونت بزرگ‌تر", "داشبورد و نمایش", "boolean", false),

            new("alarmEnd", "هشدار پایان وقت", "هشدارها و اعلان‌ها", "boolean", true),
            new("alarmFive", "هشدار ۵ دقیقه مانده", "هشدارها و اعلان‌ها", "boolean", true),
            new("repeatAlarm", "تکرار زنگ هر ۳۰ ثانیه", "هشدارها و اعلان‌ها", "boolean", true),
            new("sound", "صدای هشدار", "هشدارها و اعلان‌ها", "boolean", true),
            new("popup", "اعلان پاپ‌آپ", "هشدارها و اعلان‌ها", "boolean", true),

            new("sessionMode", "حالت پیش‌فرض جلسه", "جلسه و تسویه", "string", "settle"),
            new("autoRound", "رند خودکار به ۱۰۰۰ تومان", "جلسه و تسویه", "boolean", true),
            new("confirmDelete", "تأیید دو مرحله‌ای حذف", "جلسه و تسویه", "boolean", true),
            new("autoPrint", "چاپ خودکار فاکتور", "جلسه و تسویه", "boolean", false),
            new("operatorDiscount", "سقف تخفیف آزاد اپراتور", "جلسه و تسویه", "integer", 10),

            new("backupAuto", "بکاپ خودکار", "داده و پشتیبان‌گیری", "boolean", true),
            new("backupHour", "ساعت بکاپ", "داده و پشتیبان‌گیری", "string", "04:00"),
            new("backupKeep", "تعداد نسخه", "داده و پشتیبان‌گیری", "integer", 30),
            new("backupTarget", "مقصد بکاپ", "داده و پشتیبان‌گیری", "string", "Backups"),

            new("dns", "DNS پیش‌فرض", "شبکه و اتصال", "string", "178.22.122.100"),
            new("serverAddress", "آدرس سرور", "شبکه و اتصال", "string", "192.168.1.10:5080"),
            new("offlineMode", "حالت آفلاین کامل", "شبکه و اتصال", "boolean", true),
            new("wol", "Wake-on-LAN", "شبکه و اتصال", "boolean", true),

            new("payrollMode", "روش محاسبه حقوق", "کاربران و شیفت", "string", "hourly"),
            new("shortagePolicy", "رفتار اختلاف صندوق", "کاربران و شیفت", "string", "approval"),
            new("autoPayrollDeduction", "کسر خودکار از حقوق", "کاربران و شیفت", "boolean", false),

            new("theme", "تم", "ظاهر و محلی‌سازی", "string", "تیره"),
            new("accent", "رنگ تأکید", "ظاهر و محلی‌سازی", "string", "سبز"),
            new("calendar", "تقویم", "ظاهر و محلی‌سازی", "string", "شمسی"),
            new("currency", "واحد پول", "ظاهر و محلی‌سازی", "string", "تومان")
        };

    private static readonly IReadOnlyDictionary<string, ServerSettingDefinition> ByKey =
        Definitions.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, JsonElement> BuildValues(IEnumerable<AppSetting> stored)
    {
        var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in Definitions)
            values[definition.Key] = JsonSerializer.SerializeToElement(definition.DefaultValue);

        foreach (var setting in stored)
        {
            if (!ByKey.ContainsKey(setting.Key))
                continue;

            try
            {
                using var document = JsonDocument.Parse(setting.ValueJson);
                values[setting.Key] = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                // Corrupt rows are ignored here; the next valid save repairs them.
            }
        }

        return values;
    }

    public static bool TryValidate(
        string key,
        JsonElement value,
        out string error)
    {
        error = string.Empty;
        if (!ByKey.TryGetValue(key, out var definition))
        {
            error = $"تنظیم ناشناخته است: {key}";
            return false;
        }

        switch (definition.Type)
        {
            case "boolean" when value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False:
                return true;

            case "integer":
                if (!value.TryGetInt32(out var integer))
                {
                    error = $"مقدار «{definition.Title}» باید عدد صحیح باشد.";
                    return false;
                }

                if (key == "operatorDiscount" && integer is < 0 or > 100)
                {
                    error = "سقف تخفیف اپراتور باید بین ۰ تا ۱۰۰ درصد باشد.";
                    return false;
                }

                if (key == "backupKeep" && integer is < 1 or > 3650)
                {
                    error = "تعداد نسخهٔ بکاپ باید بین ۱ تا ۳۶۵۰ باشد.";
                    return false;
                }

                return true;

            case "string" when value.ValueKind == JsonValueKind.String:
                var text = value.GetString() ?? string.Empty;
                if (text.Length > 500)
                {
                    error = $"مقدار «{definition.Title}» بیش از حد طولانی است.";
                    return false;
                }

                if (key == "viewMode" && text is not ("v-card" or "v-compact" or "v-list"))
                {
                    error = "حالت نمایش داشبورد نامعتبر است.";
                    return false;
                }

                if (key == "sessionMode" && text is not ("settle" or "prepaid"))
                {
                    error = "حالت جلسه نامعتبر است.";
                    return false;
                }

                if (key == "payrollMode" && text is not ("hourly" or "monthly"))
                {
                    error = "روش محاسبهٔ حقوق نامعتبر است.";
                    return false;
                }

                if (key == "shortagePolicy" && text is not ("approval" or "payroll" or "expense"))
                {
                    error = "رفتار اختلاف صندوق نامعتبر است.";
                    return false;
                }

                if (key == "backupHour" && (!TimeOnly.TryParseExact(text, "HH:mm", out _)))
                {
                    error = "ساعت بکاپ باید با قالب HH:mm باشد.";
                    return false;
                }

                if (key == "accent" && text is not ("سبز" or "آبی" or "بنفش"))
                {
                    error = "رنگ تأکید نامعتبر است.";
                    return false;
                }

                if (key == "calendar" && text is not ("شمسی" or "میلادی"))
                {
                    error = "نوع تقویم نامعتبر است.";
                    return false;
                }

                if (key == "currency" && text is not "تومان")
                {
                    error = "واحد پول پشتیبانی‌شده در این نسخه تومان است.";
                    return false;
                }

                return true;

            default:
                error = $"نوع دادهٔ تنظیم «{definition.Title}» پشتیبانی نمی‌شود.";
                return false;
        }
    }

    public static ServerSettingDefinition Get(string key) => ByKey[key];
}
