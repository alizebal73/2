export type ReportSummary = {
  from: string;
  to: string;
  revenue: number;
  expense: number;
  operatingProfit: number;
  sessions: number;
  paidInvoices: number;
  customersServed: number;
};

export type StationPerformance = {
  stationId: string;
  stationName: string;
  zone: string;
  sessionCount: number;
  billableMinutes: number;
  revenue: number;
  averageSessionRevenue: number;
  occupancyEvents: number;
};

export type HeatmapCell = {
  dayOfWeek: number;
  hour: number;
  sessionCount: number;
  billableMinutes: number;
  revenue: number;
};

export type AuditExplorerRow = {
  id: string;
  createdAt: string;
  appUserId?: string | null;
  operatorName: string;
  action: string;
  entityName: string;
  entityId?: string | null;
  details?: string | null;
};

function query(from?: Date, to?: Date) {
  const params = new URLSearchParams();
  if (from) params.set('from', from.toISOString());
  if (to) params.set('to', to.toISOString());
  return params;
}

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'دریافت گزارش انجام نشد');
  }
  return await response.json() as T;
}

export function getReportSummary(from?: Date, to?: Date) {
  return getJson<ReportSummary>('/api/reports/summary?' + query(from, to).toString());
}

export function getStationReport(from?: Date, to?: Date) {
  return getJson<StationPerformance[]>('/api/reports/stations?' + query(from, to).toString());
}

export function getHeatmapReport(from?: Date, to?: Date) {
  return getJson<HeatmapCell[]>('/api/reports/heatmap?' + query(from, to).toString());
}

export function getAuditReport(input: {
  from?: Date;
  to?: Date;
  action?: string;
  entityName?: string;
  search?: string;
  limit?: number;
}) {
  const params = query(input.from, input.to);
  if (input.action) params.set('action', input.action);
  if (input.entityName) params.set('entityName', input.entityName);
  if (input.search) params.set('search', input.search);
  if (input.limit) params.set('limit', String(input.limit));
  return getJson<AuditExplorerRow[]>('/api/reports/audit?' + params.toString());
}

export async function exportFinanceReport(from?: Date, to?: Date) {
  const response = await fetch('/api/reports/export/finance.csv?' + query(from, to).toString());
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'خروجی گزارش انجام نشد');
  }
  const blob = await response.blob();
  const disposition = response.headers.get('Content-Disposition') || '';
  const match = disposition.match(/filename="?([^"]+)"?/i);
  const fileName = match?.[1] || 'gamenet-finance.csv';
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}
