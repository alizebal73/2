function isGuid(value: string) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}

async function readJson<T>(response: Response): Promise<T> {
  if (response.ok) return await response.json() as T;
  const payload = await response.json().catch(() => null) as { message?: string } | null;
  throw new Error(payload?.message || 'ورود مشتری انجام نشد');
}

export async function acquireCustomerLogin(customerId: string, clientKey: string) {
  if (!isGuid(customerId)) return null;
  return await readJson<{ acquired: boolean; loginId: string; activeCount: number; limit: number }>(
    await fetch('/api/customers/' + customerId + '/login-acquire', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ clientKey }),
    }),
  );
}

export async function releaseCustomerLogin(customerId: string, clientKey: string) {
  if (!isGuid(customerId)) return null;
  return await readJson<{ released: boolean; activeCount: number }>(
    await fetch('/api/customers/' + customerId + '/login-release', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ clientKey }),
    }),
  );
}
