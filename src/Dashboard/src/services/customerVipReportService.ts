export type CustomerVipReportFilters = {
  from?: Date;
  to?: Date;
  search?: string;
  vip?: 'all' | 'vip' | 'active' | 'expired' | 'none';
  debt?: 'all' | 'debtor' | 'clear';
  package?: string;
  page?: number;
  pageSize?: number;
};

export type CustomerVipReportRow = {
  customerId: string;
  code: string;
  username: string;
  name: string;
  vipTier: string;
  packageName?: string | null;
  vipActivatedAt?: string | null;
  vipExpiresAt?: string | null;
  vipDailyMinutes: number;
  vipTotalMinutes: number;
  vipDiscountPercent: number;
  usedTodayMinutes: number;
  usedTotalMinutes: number;
  remainingTodayMinutes: number;
  remainingTotalMinutes: number;
  walletBalance: number;
  debt: number;
  sessionCount: number;
  sessionRevenue: number;
  lastSessionAt?: string | null;
  status: string;
  notes?: string | null;
};

export type CustomerVipReportPage = {
  page: number;
  pageSize: number;
  total: number;
  summary: {
    customerCount: number;
    vipCount: number;
    activeVipCount: number;
    debtorCount: number;
    walletTotal: number;
    debtTotal: number;
    sessionCount: number;
    sessionRevenue: number;
  };
  items: CustomerVipReportRow[];
};

async function readJson(response: Response): Promise<unknown> {
  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    const message = payload && typeof payload === 'object' && 'message' in payload
      ? String((payload as { message?: unknown }).message ?? '')
      : '';
    throw new Error(message || 'دریافت گزارش مشتری و VIP انجام نشد');
  }
  return payload;
}

export async function getCustomerVipReport(
  filters: CustomerVipReportFilters = {},
): Promise<CustomerVipReportPage> {
  const params = new URLSearchParams();
  if (filters.from) params.set('from', filters.from.toISOString());
  if (filters.to) params.set('to', filters.to.toISOString());
  if (filters.search?.trim()) params.set('search', filters.search.trim());
  if (filters.vip && filters.vip !== 'all') params.set('vip', filters.vip);
  if (filters.debt && filters.debt !== 'all') params.set('debt', filters.debt);
  if (filters.package?.trim()) params.set('package', filters.package.trim());
  params.set('page', String(filters.page ?? 1));
  params.set('pageSize', String(filters.pageSize ?? 50));

  return await readJson(
    await fetch('/api/reports/customers?' + params.toString()),
  ) as CustomerVipReportPage;
}
