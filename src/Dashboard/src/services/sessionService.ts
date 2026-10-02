export type SessionPaymentPart = {
  method: 'cash' | 'card' | 'wallet' | 'gift';
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
  freeMoneyBalanceAfter: number;
  freeTimeMinutesAfter: number;
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
  freeTimeMinutes = 0,
  timeAmount?: number,
  discountAmount?: number,
  prepaidAmount?: number,
): Promise<ServerSettlementResult> {
  const response = await fetch('/api/sessions/' + sessionId + '/settle', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      totalAmount,
      parts,
      appUserId: appUserId && isGuid(appUserId) ? appUserId : null,
      freeTimeMinutes,
      timeAmount,
      discountAmount,
      prepaidAmount,
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


export type ActiveServerSession = {
  id: string;
  stationName: string;
  customerId: string;
  customerName: string;
  customerCode?: string;
  username?: string;
  startedAt: string;
  buffetTotal: number;
};

export async function getServerActiveSessions(): Promise<ActiveServerSession[]> {
  const response = await fetch('/api/sessions/active');
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'جلسه‌های فعال از سرور دریافت نشد');
  }
  return await response.json() as ActiveServerSession[];
}

export async function requestServerInvoiceReverseApproval(
  invoiceId: string,
  reason: string,
): Promise<{ id: string; status: string }> {
  const response = await fetch('/api/invoices/' + invoiceId + '/reverse/request', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ reason }),
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'درخواست تأیید برگشت فاکتور روی سرور ثبت نشد');
  }

  return await response.json() as { id: string; status: string };
}
