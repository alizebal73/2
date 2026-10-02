export type ClientIdentity = {
  deviceId: string;
  stationId?: string | null;
  stationName?: string | null;
  isOnline: boolean;
};

export async function getClientIdentity(): Promise<ClientIdentity> {
  const response = await fetch('/api/client/identity', {
    credentials: 'include',
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'Agent این رایانه شناسایی نشد.');
  }
  return await response.json() as ClientIdentity;
}
