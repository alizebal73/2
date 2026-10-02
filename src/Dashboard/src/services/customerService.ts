import type { CustomerRecord } from '../types';

export type CustomerWriteInput = {
  fullName: string;
  code?: string;
  username?: string;
  alias?: string;
  nationalId?: string;
  phone?: string;
  email?: string;
  vipTier: CustomerRecord['vip'];
  concurrentLoginLimit?: number;
  notes?: string;
  password?: string;
};

async function readError(response: Response, fallback: string) {
  const payload = await response.json().catch(() => null) as { message?: string } | null;
  return payload?.message || fallback;
}

function mapCustomer(row: {
  id: string;
  code?: string;
  username?: string;
  name: string;
  alias?: string;
  nationalId?: string;
  mobile?: string;
  vip?: string;
  wallet: number;
  debt: number;
  giftCredit: number;
  freeTimeMinutes: number;
  discountLevel: number;
  lastSeen: string;
  status: 'active' | 'warning' | 'locked';
  concurrentLoginLimit: number;
  vipPackageId?: string;
  vipPackageName?: string;
  vipActivatedAt?: string;
  vipExpiresAt?: string;
  vipDailyMinutes?: number;
  vipTotalMinutes?: number;
  vipDiscountPercent?: number;
  notes?: string;
}): CustomerRecord {
  const vip = row.vip === 'gold' || row.vip === 'silver' || row.vip === 'bronze' || row.vip === 'custom' ? row.vip : 'none';
  return {
    id: row.id,
    code: row.code,
    nationalId: row.nationalId ?? '',
    name: row.name,
    alias: row.alias ?? '',
    mobile: row.mobile ?? '',
    vip,
    wallet: row.wallet,
    debt: row.debt,
    giftCredit: row.giftCredit,
    freeTimeMinutes: row.freeTimeMinutes,
    discountLevel: 0,
    packageName: row.vipPackageName ?? (vip === 'none' ? undefined : vip === 'gold' ? 'Gold VIP' : vip === 'silver' ? 'Silver VIP' : vip === 'bronze' ? 'Bronze VIP' : 'VIP سفارشی'),
    vipPackageId: row.vipPackageId,
    vipActivatedAt: row.vipActivatedAt,
    vipExpiresAt: row.vipExpiresAt,
    vipDailyMinutes: row.vipDailyMinutes,
    vipTotalMinutes: row.vipTotalMinutes,
    vipDiscountPercent: row.vipDiscountPercent,
    username: row.username ?? row.code ?? '',
    lastSeen: row.lastSeen,
    status: row.status,
    concurrentLoginLimit: row.concurrentLoginLimit,
    notes: row.notes ?? '',
    transactionHistory: [],
  };
}

export async function getServerCustomers(): Promise<CustomerRecord[]> {
  const response = await fetch('/api/customers');
  if (!response.ok) throw new Error(await readError(response, 'دریافت فهرست مشتریان از سرور انجام نشد'));
  const rows = await response.json() as Array<Parameters<typeof mapCustomer>[0]>;
  return rows.map(mapCustomer);
}

export async function createServerCustomer(input: CustomerWriteInput): Promise<CustomerRecord> {
  const response = await fetch('/api/customers', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      fullName: input.fullName,
      code: input.code || null,
      username: input.username || null,
      alias: input.alias || null,
      nationalId: input.nationalId || null,
      phone: input.phone || null,
      email: input.email || null,
      vipTier: input.vipTier,
      concurrentLoginLimit: input.concurrentLoginLimit ?? 1,
      notes: input.notes || null,
      password: input.password || null,
    }),
  });
  if (!response.ok) throw new Error(await readError(response, 'ثبت مشتری در سرور انجام نشد'));
  return mapCustomer(await response.json() as Parameters<typeof mapCustomer>[0]);
}

export async function changeServerCustomerPassword(customerId: string, password: string) {
  const response = await fetch('/api/customers/' + customerId + '/password', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ password }),
  });
  if (!response.ok) throw new Error(await readError(response, 'تغییر رمز ورود مشتری انجام نشد'));
  return response.json() as Promise<{ changed: boolean }>;
}

export async function getCustomerDebts(customerId: string) {
  const response = await fetch('/api/customers/' + customerId + '/debts');
  if (!response.ok) throw new Error(await readError(response, 'دریافت بدهی‌های مشتری انجام نشد'));
  return response.json() as Promise<Array<{ id: string; amount: number; issuedAt: string; description: string }>>;
}

export async function settleCustomerDebt(customerId: string, invoiceId: string, method: 'cash' | 'card' | 'wallet') {
  const response = await fetch('/api/customers/' + customerId + '/debts/' + invoiceId + '/settle', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ method }),
  });
  if (!response.ok) throw new Error(await readError(response, 'تسویه بدهی مشتری انجام نشد'));
  return response.json() as Promise<{ invoiceId: string; customerId: string; amount: number; method: string; debtRemaining: number; walletBalanceAfter: number }>;
}

export async function getCustomerVipUsage(customerId: string) {
  const response = await fetch('/api/customers/' + customerId + '/vip-usage');
  if (!response.ok) throw new Error(await readError(response, 'دریافت مصرف VIP انجام نشد'));
  return response.json() as Promise<import('../types').CustomerVipUsage>;
}

export async function getCustomerHistory(customerId: string) {
  const response = await fetch('/api/customers/' + customerId + '/history');
  if (!response.ok) throw new Error(await readError(response, 'دریافت تاریخچه مشتری انجام نشد'));
  return response.json() as Promise<import('../types').CustomerHistoryItem[]>;
}

export async function createServerCustomerDebt(customerId: string, amount: number, description: string) {
  const response = await fetch('/api/customers/' + customerId + '/debt', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ amount, description }),
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'ثبت بدهی مشتری انجام نشد');
  }
  return response.json() as Promise<{ invoiceId: string; customerId: string; amount: number; description: string }>;
}

export async function updateServerCustomer(customerId: string, input: CustomerWriteInput): Promise<CustomerRecord> {
  const response = await fetch('/api/customers/' + customerId, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      fullName: input.fullName,
      code: input.code || null,
      username: input.username || null,
      alias: input.alias || null,
      nationalId: input.nationalId || null,
      phone: input.phone || null,
      email: input.email || null,
      vipTier: input.vipTier,
      concurrentLoginLimit: input.concurrentLoginLimit ?? 1,
      notes: input.notes || null,
    }),
  });
  if (!response.ok) throw new Error(await readError(response, 'ویرایش مشتری در سرور انجام نشد'));
  return mapCustomer(await response.json() as Parameters<typeof mapCustomer>[0]);
}
