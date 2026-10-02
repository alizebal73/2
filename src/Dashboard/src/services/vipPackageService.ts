import type { VipPackageRecord } from '../types';

type VipPackageDto = {
  id: string;
  name: string;
  tier: VipPackageRecord['tier'];
  price: number;
  durationDays: number;
  dailyMinutes: number;
  totalMinutes: number;
  discountPercent: number;
  overflowRule: string;
  description?: string;
  isActive: boolean;
};

export async function getVipPackages(): Promise<VipPackageRecord[]> {
  const response = await fetch('/api/vip-packages');
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'دریافت پکیج‌های VIP انجام نشد');
  }
  const rows = await response.json() as VipPackageDto[];
  return rows.map(row => ({
    id: row.id,
    name: row.name,
    tier: row.tier,
    price: row.price,
    dailyMinutes: row.dailyMinutes,
    totalMinutes: row.totalMinutes,
    discount: row.discountPercent,
    active: row.isActive,
  }));
}

export async function assignVipPackage(customerId: string, vipPackageId: string) {
  const response = await fetch('/api/customers/' + customerId + '/vip-package', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ vipPackageId }),
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'تخصیص پکیج VIP انجام نشد');
  }
  return response.json() as Promise<{
    customerId: string;
    packageId: string;
    packageName: string;
    vipTier: string;
    activatedAt: string;
    expiresAt: string;
    dailyMinutes: number;
    totalMinutes: number;
    discountPercent: number;
  }>;
}
