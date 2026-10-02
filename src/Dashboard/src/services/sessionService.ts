export type SessionPaymentPart = {
  method: 'cash' | 'card' | 'wallet';
  amount: number;
};

export type StartServerSessionRequest = {
  customerId: string;
  stationId: string;
  tariffId?: string;
  appUserId?: string;
  hourlyRateOverride?: number;
  persons?: number;
};

export type StartServerSessionResult = {
  sessionId: string;
  stationId: string;
  customerId: string;
  startAt: string;
};

export type ServerSettlementResult = {
  invoiceId: string;
  sessionId: string;
  totalAmount: number;
  parts: SessionPaymentPart[];
  walletBalanceAfter: number;
  invoiceStatus: string;
  paidAt: string;
};

function isGuid(value: string) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}

export function isServerGuid(value: string | undefined): value is string {
  return Boolean(value && isGuid(value));
}

export async function startServerSession(
  input: StartServerSessionRequest,
): Promise<StartServerSessionResult | null> {
  if (!isGuid(input.customerId) || !isGuid(input.stationId)) return null;

  const body: StartServerSessionRequest = {
    customerId: input.customerId,
    stationId: input.stationId,
    ...(input.tariffId && isGuid(input.tariffId) ? { tariffId: input.tariffId } : {}),
    ...(input.appUserId && isGuid(input.appUserId) ? { appUserId: input.appUserId } : {}),
    ...(input.hourlyRateOverride !== undefined ? { hourlyRateOverride: input.hourlyRateOverride } : {}),
    ...(input.persons !== undefined ? { persons: input.persons } : {}),
  };

  const response = await fetch('/api/sessions', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'شروع جلسه روی سرور انجام نشد');
  }

  return await response.json() as StartServerSessionResult;
}

export async function settleServerSession(
  sessionId: string,
  totalAmount: number,
  parts: SessionPaymentPart[],
  appUserId?: string,
): Promise<ServerSettlementResult> {
  const response = await fetch('/api/sessions/' + sessionId + '/settle', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      totalAmount,
      parts,
      appUserId: appUserId && isGuid(appUserId) ? appUserId : null,
    }),
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'تسویه جلسه روی سرور انجام نشد');
  }

  return await response.json() as ServerSettlementResult;
}


export async function updateServerSessionDetails(
  sessionId: string,
  input: { hourlyRate?: number; persons?: number },
): Promise<void> {
  const response = await fetch('/api/sessions/' + sessionId + '/details', {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'تغییرات جلسه روی سرور ثبت نشد');
  }
}

export async function transferServerSession(
  sessionId: string,
  targetStationId: string,
): Promise<{ sessionId: string; stationId: string }> {
  const response = await fetch('/api/sessions/' + sessionId + '/transfer', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ targetStationId }),
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'انتقال جلسه روی سرور انجام نشد');
  }
  return await response.json() as { sessionId: string; stationId: string };
}
