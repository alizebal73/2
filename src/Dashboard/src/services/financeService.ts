export type FinanceSummary = {
  from: string;
  to: string;
  revenue: number;
  expense: number;
  operatingProfit: number;
  source: 'server';
};

export type FinanceExpense = {
  id: string;
  shiftId: string;
  category: string;
  amount: number;
  description?: string;
  createdAt: string;
};

export async function getFinanceSummary(from?: Date, to?: Date): Promise<FinanceSummary> {
  const query = new URLSearchParams();
  if (from) query.set('from', from.toISOString());
  if (to) query.set('to', to.toISOString());
  const response = await fetch('/api/finance/summary?' + query.toString());
  if (!response.ok) throw new Error('دریافت خلاصه مالی انجام نشد');
  const row = await response.json() as { from: string; to: string; revenue: number; expense: number; operatingProfit: number; };
  return { ...row, source: 'server' };
}

export async function createShiftExpense(
  shiftId: string,
  input: { amount: number; category: string; description?: string; appUserId?: string },
): Promise<FinanceExpense> {
  const response = await fetch('/api/shifts/' + shiftId + '/expenses', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'ثبت هزینه انجام نشد');
  }
  return await response.json() as FinanceExpense;
}

export async function getFinanceExpenses(from?: Date, to?: Date): Promise<FinanceExpense[]> {
  const shiftsResponse = await fetch('/api/shifts/history');
  if (!shiftsResponse.ok) throw new Error('دریافت سابقه شیفت‌ها انجام نشد');
  const shifts = await shiftsResponse.json() as Array<{ id: string; openedAt: string; closedAt?: string }>;
  const start = from?.getTime() ?? Number.MIN_SAFE_INTEGER;
  const end = to?.getTime() ?? Number.MAX_SAFE_INTEGER;
  const relevant = shifts.filter(shift => {
    const opened = new Date(shift.openedAt).getTime();
    const closed = shift.closedAt ? new Date(shift.closedAt).getTime() : Date.now();
    return closed >= start && opened <= end;
  });
  const rows = await Promise.all(relevant.map(async shift => {
    const response = await fetch('/api/shifts/' + shift.id + '/expenses');
    if (!response.ok) throw new Error('دریافت هزینه‌های شیفت انجام نشد');
    return await response.json() as FinanceExpense[];
  }));
  return rows.flat().filter(row => {
    const created = new Date(row.createdAt).getTime();
    return created >= start && created <= end;
  });
}
