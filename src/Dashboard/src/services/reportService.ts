import type { AuditLogRecord, CustomerPerformance, OperatorPerformance } from '../types';

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'دریافت گزارش انجام نشد');
  }
  return await response.json() as T;
}

export type CustomerReport = {
  customerId: string;
  customerCode: string;
  customerName: string;
  sessionCount: number;
  billableMinutes: number;
  revenue: number;
  vipMinutesUsed: number;
  walletBalance: number;
  outstandingDebt: number;
};

export type OperatorReport = {
  appUserId: string;
  operatorName: string;
  shiftCount: number;
  shiftHours: number;
  paidInvoiceCount: number;
  revenue: number;
  expenses: number;
  difference: number;
};

function query(from?: Date, to?: Date) {
  const params = new URLSearchParams();
  if (from) params.set('from', from.toISOString());
  if (to) params.set('to', to.toISOString());
  return params;
}

export function getReportSummary(from?: Date, to?: Date) {
  return getJson<import('../types').ReportSummary>('/api/reports/summary?' + query(from, to).toString());
}

export function getStationReport(from?: Date, to?: Date) {
  return getJson<import('../types').StationPerformance[]>('/api/reports/stations?' + query(from, to).toString());
}

export function getHeatmapReport(from?: Date, to?: Date) {
  return getJson<import('../types').HeatmapCell[]>('/api/reports/heatmap?' + query(from, to).toString());
}

export function getCustomerReport(from?: Date, to?: Date) {
  return getJson<CustomerReport[]>('/api/reports/customers?' + query(from, to).toString());
}

export function getOperatorReport(from?: Date, to?: Date) {
  return getJson<OperatorReport[]>('/api/reports/operators?' + query(from, to).toString());
}

export function getAuditReport(input: {
  from?: Date;
  to?: Date;
  action?: string;
  entityName?: string;
  search?: string;
  limit?: number;
}) {
  const params = query(input.from, input.to);
  if (input.action) params.set('action', input.action);
  if (input.entityName) params.set('entityName', input.entityName);
  if (input.search) params.set('search', input.search);
  if (input.limit) params.set('limit', String(input.limit));
  return getJson<import('../types').AuditExplorerRow[]>('/api/reports/audit?' + params.toString());
}

export async function exportFinanceReport(from?: Date, to?: Date) {
  const response = await fetch('/api/reports/export/finance.csv?' + query(from, to).toString());
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'خروجی گزارش انجام نشد');
  }
  const blob = await response.blob();
  const disposition = response.headers.get('Content-Disposition') || '';
  const match = disposition.match(/filename="?([^"]+)"?/i);
  const fileName = match?.[1] || 'gamenet-finance.csv';
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}
