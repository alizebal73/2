export type UsersShiftReportFilters = {
  from?: Date;
  to?: Date;
  userSearch?: string;
  shiftState?: 'all' | 'open' | 'closed';
  page?: number;
  pageSize?: number;
};

export type UsersShiftReportRow = {
  userId: string;
  fullName: string;
  userName: string;
  role: string;
  isActive: boolean;
  payType: string;
  employeePayable: number;
  ownerReceivable: number;
  paidThisPeriod: number;
  bonusThisPeriod: number;
  deductionThisPeriod: number;
  shiftCount: number;
  closedShiftCount: number;
  shiftRevenue: number;
  shiftCashSales: number;
  shiftExpenses: number;
  shiftDifference: number;
  sessionCount: number;
  sessionRevenue: number;
  lastLoginAt?: string | null;
};

export type UsersShiftReportPage = {
  page: number;
  pageSize: number;
  total: number;
  summary: {
    userCount: number;
    activeUserCount: number;
    shiftCount: number;
    closedShiftCount: number;
    shiftRevenue: number;
    shiftCashSales: number;
    shiftExpenses: number;
    shiftDifference: number;
    sessionCount: number;
    sessionRevenue: number;
    payrollPaid: number;
    payrollEmployeePayable: number;
  };
  items: UsersShiftReportRow[];
};

async function readJson(response: Response): Promise<unknown> {
  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    const message = payload && typeof payload === 'object' && 'message' in payload
      ? String((payload as { message?: unknown }).message ?? '')
      : '';
    throw new Error(message || 'دریافت گزارش کاربران و شیفت انجام نشد');
  }
  return payload;
}

export async function getUsersShiftReport(
  filters: UsersShiftReportFilters = {},
): Promise<UsersShiftReportPage> {
  const params = new URLSearchParams();
  if (filters.from) params.set('from', filters.from.toISOString());
  if (filters.to) params.set('to', filters.to.toISOString());
  if (filters.userSearch?.trim()) params.set('userSearch', filters.userSearch.trim());
  if (filters.shiftState && filters.shiftState !== 'all') params.set('shiftState', filters.shiftState);
  params.set('page', String(filters.page ?? 1));
  params.set('pageSize', String(filters.pageSize ?? 50));

  return await readJson(
    await fetch('/api/reports/users-shifts?' + params.toString()),
  ) as UsersShiftReportPage;
}
