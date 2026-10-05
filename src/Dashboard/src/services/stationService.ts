export type StationRecord = {
  id: string;
  name: string;
  zone: string;
  type: string;
  ratePerHour: number;
  network: number;
  state: string;
  isActive: boolean;
  stationTypeId: string;
  stationTypeName: string;
  tariffId?: string | null;
  tariffName?: string | null;
  agentDeviceId?: string | null;
  agentDeviceName?: string | null;
};

async function readJson<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error((body as { message?: string } | null)?.message || 'عملیات ایستگاه انجام نشد.');
  }
  return body as T;
}

export async function getStations(): Promise<StationRecord[]> {
  return readJson<StationRecord[]>(await fetch('/api/stations', { credentials: 'include' }));
}

export async function createStation(input: {
  name: string;
  zone: string;
  type: string;
  ratePerHour: number;
  network: number;
  tariffId?: string | null;
}): Promise<StationRecord> {
  return readJson<StationRecord>(await fetch('/api/stations', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  }));
}

export async function updateStation(id: string, input: {
  name: string;
  zone: string;
  type: string;
  ratePerHour: number;
  network: number;
  tariffId?: string | null;
  isActive: boolean;
}): Promise<StationRecord> {
  return readJson<StationRecord>(await fetch('/api/stations/' + encodeURIComponent(id), {
    method: 'PUT',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  }));
}

export async function provisionStations(input: {
  prefix: string;
  startNumber: number;
  count: number;
  zone: string;
  type: string;
  ratePerHour: number;
  network: number;
  tariffId?: string | null;
}): Promise<{ created: number; names: string[] }> {
  return readJson<{ created: number; names: string[] }>(await fetch('/api/stations/provision', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  }));
}
