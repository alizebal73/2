import type { PayrollLedgerEntry, PayrollUserRecord } from '../types';

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
    ...init,
  });
  if (!response.ok) {
    let message = 'عملیات حقوق و حساب پرسنلی انجام نشد';
    try { message = (await response.json() as { message?: string }).message || message; } catch {}
    throw new Error(message);
  }
  return await response.json() as T;
}

export async function getPayrollUsers(): Promise<PayrollUserRecord[]> {
  return await request<PayrollUserRecord[]>('/api/payroll/users');
}

export async function getPayrollLedger(userId: string): Promise<PayrollLedgerEntry[]> {
  return await request<PayrollLedgerEntry[]>(`/api/payroll/users/${userId}/ledger`);
}

export async function updatePayrollProfile(userId: string, input: {
  phone?: string;
  payType: string;
  hourlyRate: number;
  monthlySalary: number;
  overtimeRate: number;
  employmentStartDate?: string | null;
  workSchedule?: string | null;
  notes?: string | null;
  isActive: boolean;
}): Promise<PayrollUserRecord> {
  return await request<PayrollUserRecord>(`/api/payroll/users/${userId}`, { method: 'PUT', body: JSON.stringify(input) });
}

export async function createPayrollEntry(userId: string, input: {
  kind: string;
  amount: number;
  reason: string;
  paymentMethod?: string;
  receiptNumber?: string;
  employeePayableDelta?: number;
  ownerReceivableDelta?: number;
}): Promise<{ entryId: string; status: string; approvalId?: string }> {
  return await request(`/api/payroll/users/${userId}/entries`, { method: 'POST', body: JSON.stringify(input) });
}
