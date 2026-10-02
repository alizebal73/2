import { mockService } from './mockService';
import type { WalletLedgerEntry } from '../types';

function isGuid(value: string) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}

export async function getWalletLedger(customerId: string): Promise<WalletLedgerEntry[]> {
  if (!isGuid(customerId)) return mockService.getWalletLedger(customerId);
  const response = await fetch('/api/customers/' + customerId + '/wallet-ledger');
  if (!response.ok) throw new Error('دریافت دفتر کیف پول انجام نشد');
  const rows = await response.json() as Array<{
    id: string;
    customerId: string;
    amount: number;
    type: string;
    description: string;
    createdAt: string;
    balanceAfter: number;
    referenceTransactionId?: string | null;
  }>;
  return rows.map(row => ({
    ...row,
    direction: row.type.toLowerCase() === 'credit' ? 'credit' : 'debit',
    type: row.type.toLowerCase() === 'credit' ? 'charge' : row.type.toLowerCase() === 'refund' ? 'refund' : 'debit',
    referenceTransactionId: row.referenceTransactionId ?? undefined,
  }));
}

export async function recordWalletTransaction(
  customerId: string,
  input: { amount: number; type: 'credit' | 'debit'; description: string },
): Promise<WalletLedgerEntry> {
  if (!isGuid(customerId)) return mockService.recordWalletTransaction(customerId, input);
  const response = await fetch('/api/customers/' + customerId + '/wallet-transactions', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'ثبت تراکنش کیف پول انجام نشد');
  }
  const row = await response.json() as {
    id: string;
    customerId: string;
    amount: number;
    type: string;
    description: string;
    createdAt: string;
    balanceAfter: number;
    referenceTransactionId?: string | null;
  };
  return {
    ...row,
    direction: row.type.toLowerCase() === 'credit' ? 'credit' : 'debit',
    type: row.type.toLowerCase() === 'credit' ? 'charge' : 'debit',
    referenceTransactionId: row.referenceTransactionId ?? undefined,
  };
}


export async function refundWalletTransaction(
  customerId: string,
  input: { amount: number; reason: string; sourceTransactionId?: string },
): Promise<WalletLedgerEntry> {
  if (!isGuid(customerId)) {
    return mockService.recordWalletTransaction(customerId, {
      amount: input.amount,
      type: 'debit',
      description: 'بازگشت وجه · ' + input.reason,
    });
  }

  const response = await fetch('/api/customers/' + customerId + '/wallet-refunds', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ amount: input.amount, reason: input.reason, sourceTransactionId: input.sourceTransactionId ?? null }),
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'ثبت بازگشت وجه انجام نشد');
  }

  const row = await response.json() as {
    id: string;
    customerId: string;
    amount: number;
    type: string;
    description: string;
    createdAt: string;
    balanceAfter: number;
  };

  return {
    ...row,
    direction: 'debit',
    type: 'refund',
  };
}
