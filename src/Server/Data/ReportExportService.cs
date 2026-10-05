using System.Globalization;
using System.Text;

namespace GameNetManager.Server.Data;

public static class ReportExportService
{
    public static string Sessions(SessionReportPageDto report)
        => Csv(
            ["تاریخ شروع","تاریخ پایان","ایستگاه","منطقه","مشتری","اپراتور","وضعیت","مدت دقیقه","مبلغ"],
            report.Items.Select(item => (IReadOnlyList<object?>)[
                item.StartAt.ToString("O", CultureInfo.InvariantCulture),
                item.EndAt?.ToString("O", CultureInfo.InvariantCulture) ?? "",
                item.StationName,
                item.Zone,
                item.CustomerName,
                item.Operator,
                item.State,
                item.BillableMinutes,
                item.TotalAmount
            ]));

    public static string Customers(CustomerVipReportPageDto report)
        => Csv(
            ["کد","نام مشتری","نام کاربری","VIP","پکیج","کیف پول","بدهی","تعداد جلسات","درآمد جلسات","وضعیت"],
            report.Items.Select(item => (IReadOnlyList<object?>)[
                item.Code,
                item.Name,
                item.Username,
                item.VipTier,
                item.PackageName ?? "",
                item.WalletBalance,
                item.Debt,
                item.SessionCount,
                item.SessionRevenue,
                item.Status
            ]));

    public static string UsersShift(UsersShiftReportPageDto report)
        => Csv(
            ["کاربر","نام کاربری","نقش","شیفت","شیفت بسته","فروش شیفت","فروش نقدی","هزینه","اختلاف","جلسات","درآمد جلسات","حقوق پرداخت‌شده","مطالبات پرسنل"],
            report.Items.Select(item => (IReadOnlyList<object?>)[
                item.FullName,
                item.UserName,
                item.Role,
                item.ShiftCount,
                item.ClosedShiftCount,
                item.ShiftRevenue,
                item.ShiftCashSales,
                item.ShiftExpenses,
                item.ShiftDifference,
                item.SessionCount,
                item.SessionRevenue,
                item.PaidThisPeriod,
                item.EmployeePayable
            ]));

    public static string Audit(AuditLogPageDto report)
        => Csv(
            ["تاریخ","کاربر","عملیات","هدف","جزئیات"],
            report.Items.Select(item => (IReadOnlyList<object?>)[
                item.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
                item.Operator,
                item.Action,
                item.EntityName + (string.IsNullOrWhiteSpace(item.EntityId) ? "" : " · " + item.EntityId),
                item.Details ?? ""
            ]));

    private static string Csv(
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
            builder.AppendLine(string.Join(",", row.Select(Escape)));
        return builder.ToString();
    }

    private static string Escape(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
