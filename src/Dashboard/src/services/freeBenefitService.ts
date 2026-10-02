export type FreeBenefitTransaction = {
  id: string;
  type: string;
  moneyAmount: number;
  minutes: number;
  description: string;
  createdAt: string;
};

export type FreeBenefitsSnapshot = {
  freeMoney: number;
  freeTimeMinutes: number;
  transactions: FreeBenefitTransaction[];
};

function isGuid(value: string) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}

async function readJson<T>(response: Response): Promise<T> {
  if (response.ok) return await response.json() as T;
  const payload = await response.json().catch(() => null) as { message?: string } | null;
  throw new Error(payload?.message || 'عملیات اعتبار رایگان انجام نشد');
}

export async function getFreeBenefits(customerId: string): Promise<FreeBenefitsSnapshot | null> {
  if (!isGuid(customerId)) return null;
  return await readJson<FreeBenefitsSnapshot>(await fetch('/api/customers/' + customerId + '/free-benefits'));
}

export async function changeFreeBenefits(
  customerId: string,
  input: { moneyAmount?: number; minutes?: number; mode: 'credit' | 'debit'; description: string },
): Promise<FreeBenefitsSnapshot | null> {
  if (!isGuid(customerId)) return null;
  return await readJson<FreeBenefitsSnapshot>(await fetch('/api/customers/' + customerId + '/free-benefits', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      moneyAmount: input.moneyAmount ?? 0,
      minutes: input.minutes ?? 0,
      mode: input.mode,
      description: input.description,
    }),
  }));
}
