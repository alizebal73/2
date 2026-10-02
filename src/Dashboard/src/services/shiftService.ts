export type ShiftSnapshot = {
  id: string;
  appUserId: string;
  operator: string;
  openedAt: string;
  closedAt?: string;
  cashOpening: number;
  cashClosing?: number;
  cashSales: number;
  expenses: number;
  externalCash: number;
  expectedCash: number;
  difference: number;
  note?: string;
};

async function readJson<T>(response: Response): Promise<T> {
  if (response.ok) return await response.json() as T;
  const payload = await response.json().catch(() => null) as { message?: string } | null;
  throw new Error(payload?.message || 'عملیات شیفت انجام نشد');
}

export async function getCurrentShift(): Promise<ShiftSnapshot | null> {
  const response = await fetch('/api/shifts/current');
  return await readJson<ShiftSnapshot | null>(response);
}

export async function getShiftHistory(): Promise<ShiftSnapshot[]> {
  const response = await fetch('/api/shifts/history');
  return await readJson<ShiftSnapshot[]>(response);
}

export async function startServerShift(input: {
  operatorName?: string;
  appUserId?: string;
  cashOpening: number;
  note?: string;
}): Promise<ShiftSnapshot> {
  const response = await fetch('/api/shifts/start', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  });
  return await readJson<ShiftSnapshot>(response);
}

export async function closeServerShift(
  shiftId: string,
  input: { cashClosing: number; externalCash: number; note?: string },
): Promise<ShiftSnapshot> {
  const response = await fetch('/api/shifts/' + shiftId + '/close', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  });
  return await readJson<ShiftSnapshot>(response);
}
