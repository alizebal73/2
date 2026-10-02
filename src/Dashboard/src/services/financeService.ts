import { mockService } from './mockService';

export type FinanceSummary = {
  from: string;
  to: string;
  revenue: number;
  expense: number;
  operatingProfit: number;
  source: 'server' | 'mock';
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
  try {
    const query = new URLSearchParams();
    if (from) query.set('from', from.toISOString());
    if (to) query.set('to', to.toISOString());
    const response = await fetch('/api/finance/summary?' + query.toString());
    if (!response.ok) throw new Error('دریافت خلاصه مالی انجام نشد');
    const row = await response.json() as { from: string; to: string; revenue: number; expense: number; operatingProfit: number; };
    return { ...row, source: 'server' };
  } catch {
    const [rows, expenses] = await Promise.all([mockService.getReportRows(), mockService.getExpenses()]);
    const now = Date.now();
    const start = from?.getTime() ?? now - 6 * 86400000;
    const end = to?.getTime() ?? now;
    const visibleRows = rows.filter((row: any) => {
      const time = new Date(row.closedAt).getTime();
      return time >= start && time <= end;
    });
    const visibleExpenses = expenses.filter(item => {
      const time = new Date(item.createdAt).getTime();
      return time >= start && time <= end;
    });
    const revenue = visibleRows.reduce((sum: number, row: any) => sum + row.amount, 0);
    const expense = visibleExpenses.reduce((sum: number, row: any) => sum + row.amount, 0);
    return {
      from: new Date(start).toISOString(),
      to: new Date(end).toISOString(),
      revenue,
      expense,
      operatingProfit: revenue - expense,
      source: 'mock',
    };
  }
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
