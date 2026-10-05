export type ClientRequestKind = 'message' | 'charge' | 'move' | 'unlock' | 'buffet';

async function readJson<T>(response: Response): Promise<T> {
  if (response.ok) return await response.json() as T;
  const payload = await response.json().catch(() => null) as { message?: string } | null;
  throw new Error(payload?.message || 'عملیات Client انجام نشد.');
}

export async function createClientRequest(input: {
  customerId: string;
  loginId: string;
  kind: ClientRequestKind;
  message?: string;
  productId?: string;
  quantity?: number;
}) {
  return await readJson<{ created: boolean; message: string }>(
    await fetch('/api/client/request', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input),
    }),
  );
}

export async function lockClient(customerId: string, loginId: string) {
  return await readJson<{ commandId: string; status: string; message: string }>(
    await fetch('/api/client/command/lock', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ customerId, loginId }),
    }),
  );
}

export async function logoutAndLockClient(customerId: string, loginId: string) {
  return await readJson<{ commandId: string; status: string; message: string }>(
    await fetch('/api/client/command/logout-lock', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ customerId, loginId }),
    }),
  );
}

export async function launchClientGame(input: {
  customerId: string;
  loginId: string;
  sessionId: string;
  gameId: string;
}) {
  return await readJson<{ commandId: string; status: string; message: string }>(
    await fetch('/api/client/game/launch', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input),
    }),
  );
}

export async function stopClientGame(input: {
  customerId: string;
  loginId: string;
  sessionId: string;
  gameId: string;
}) {
  return await readJson<{ commandId: string; status: string; message: string }>(
    await fetch('/api/client/game/stop', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input),
    }),
  );
}
