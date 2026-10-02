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
    }),
  });
  if (!response.ok) throw new Error(await readError(response, 'ثبت مشتری در سرور انجام نشد'));
  return mapCustomer(await response.json() as Parameters<typeof mapCustomer>[0]);
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
