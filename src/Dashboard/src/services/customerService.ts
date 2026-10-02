import type { CustomerRecord } from '../types';

export async function getServerCustomers(): Promise<CustomerRecord[]> {
  const response = await fetch('/api/customers');
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'دریافت فهرست مشتریان از سرور انجام نشد');
  }

  const rows = await response.json() as Array<{
    id: string;
    code?: string;
    username?: string;
    name: string;
    mobile?: string;
    vip: 'gold' | 'none';
    wallet: number;
    debt: number;
    giftCredit: number;
    freeTimeMinutes: number;
    discountLevel: number;
    lastSeen: string;
    status: 'active' | 'warning' | 'locked';
    concurrentLoginLimit: number;
  }>;

  return rows.map(row => ({
    id: row.id,
    code: row.code,
    nationalId: '',
    name: row.name,
    alias: '',
    mobile: row.mobile ?? '',
    vip: row.vip,
    wallet: row.wallet,
    debt: row.debt,
    giftCredit: row.giftCredit,
    freeTimeMinutes: row.freeTimeMinutes,
    discountLevel: row.discountLevel,
    username: row.username ?? row.code ?? '',
    lastSeen: row.lastSeen,
    status: row.status,
    concurrentLoginLimit: row.concurrentLoginLimit,
    transactionHistory: [],
  }));
}
