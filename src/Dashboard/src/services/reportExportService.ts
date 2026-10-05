export type ReportExportKey = 'sessions' | 'customers' | 'users-shift' | 'audit';

export async function downloadReportCsv(
  report: ReportExportKey,
  params: Record<string, string | number | undefined>,
): Promise<void> {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') query.set(key, String(value));
  }
  query.set('report', report);

  const response = await fetch('/api/reports/export?' + query.toString(), {
    credentials: 'include',
  });

  if (!response.ok) {
    let message = 'خروجی گزارش انجام نشد';
    try {
      const payload = await response.json() as { message?: string };
      message = payload.message || message;
    } catch {
      // Keep the Persian fallback when the server did not return JSON.
    }
    throw new Error(message);
  }

  const blob = await response.blob();
  const disposition = response.headers.get('Content-Disposition') || '';
  const filename = /filename="?([^";]+)"?/i.exec(disposition)?.[1] || `gamenet-${report}-report.csv`;
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  link.download = filename;
  link.click();
  URL.revokeObjectURL(link.href);
}
