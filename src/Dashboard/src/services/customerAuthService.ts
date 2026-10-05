type CustomerAuthResponse = {
  authenticated: boolean;
  customerId: string;
  username?: string | null;
  fullName: string;
  loginId: string;
  activeCount: number;
  limit: number;
  balance: number;
  freeMoney: number;
  freeTimeMinutes: number;
  vipTier: string;
  isLocked: boolean;
};

export type CustomerSessionState = {
  authenticated: boolean;
  customerId: string;
  loginId: string;
  username?: string | null;
  fullName: string;
  balance: number;
  freeMoney: number;
  freeTimeMinutes: number;
  vipTier: string;
  isLocked: boolean;
  session: {
    id: string;
    state: string;
    startAt: string;
    endAt?: string | null;
    gameId?: string | null;
    stationName: string;
  } | null;
};

async function readJson<T>(response: Response): Promise<T> {
  if (response.ok) return await response.json() as T;
  const payload = await response.json().catch(() => null) as { message?: string } | null;
  throw new Error(payload?.message || 'عملیات مشتری انجام نشد');
}

export async function authenticateCustomer(usernameOrCode: string, password: string, clientKey: string) {
  return await readJson<CustomerAuthResponse>(await fetch('/api/customer-auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ usernameOrCode, password, clientKey }),
  }));
}

export async function readCustomerState(customerId: string, loginId: string, clientKey: string) {
  const params = new URLSearchParams({ customerId, loginId, clientKey });
  return await readJson<CustomerSessionState>(
    await fetch('/api/customer-auth/state?' + params.toString()),
  );
}

export async function releaseCustomerLogin(customerId: string, clientKey: string) {
  return await readJson<{ released: boolean; activeCount: number }>(
    await fetch('/api/customers/' + customerId + '/login-release', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ clientKey }),
    }),
  );
}
