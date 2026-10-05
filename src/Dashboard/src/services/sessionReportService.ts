export type SessionReportState = 'Active' | 'Ended' | 'Completed' | 'Cancelled' | '';

export type SessionReportFilters = {
  from?: Date;
  to?: Date;
  station?: string;
  zone?: string;
  operator?: string;
  state?: SessionReportState;
  customerSearch?: string;
  page?: number;
  pageSize?: number;
};

export type SessionReportRow = {
  id: string;
  startAt: string;
  endAt?: string | null;
  state: string;
  stationId: string;
  stationName: string;
  zone: string;
  stationType: string;
  customerId: string;
  customerName: string;
  customerCode?: string | null;
  customerUsername?: string | null;
  appUserId?: string | null;
  operator: string;
  gameId?: string | null;
  gameName?: string | null;
  persons: number;
  billableMinutes: number;
  totalAmount: number;
};

export type SessionStationSummary = {
  stationId: string;
  stationName: string;
  zone: string;
  sessionCount: number;
  billableMinutes: number;
  revenue: number;
};

export type SessionReportPage = {
  page: number;
  pageSize: number;
  total: number;
  summary: {
    sessionCount: number;
    billableMinutes: number;
    revenue: number;
    averageMinutes: number;
    stations: SessionStationSummary[];
  };
  items: SessionReportRow[];
};

export async function getSessionReport(filters: SessionReportFilters = {}): Promise<SessionReportPage> {
  const params = new URLSearchParams();
  if (filters.from) params.set('from', filters.from.toISOString());
  if (filters.to) params.set('to', filters.to.toISOString());
  if (filters.station?.trim()) params.set('station', filters.station.trim());
  if (filters.zone?.trim()) params.set('zone', filters.zone.trim());
  if (filters.operator?.trim()) params.set('operator', filters.operator.trim());
  if (filters.state?.trim()) params.set('state', filters.state.trim());
  if (filters.customerSearch?.trim()) params.set('customerSearch', filters.customerSearch.trim());
  params.set('page', String(filters.page ?? 1));
  params.set('pageSize', String(filters.pageSize ?? 50));

  const response = await fetch('/api/reports/sessions?' + params.toString());
  const payload = await response.json().catch(() => null) as SessionReportPage & { message?: string } | null;
  if (!response.ok) {
    throw new Error(payload?.message || 'دریافت گزارش جلسات انجام نشد');
  }

  if (!payload) {
    throw new Error('پاسخ گزارش جلسات از Server نامعتبر بود');
  }

  return payload;
}
