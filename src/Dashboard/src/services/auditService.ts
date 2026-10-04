import type { AuditLogRecord } from '../types';

export type AuditLogFilters = {
  from?: Date;
  to?: Date;
  operator?: string;
  action?: string;
  entityName?: string;
  search?: string;
  page?: number;
  pageSize?: number;
};

export type AuditLogPage = {
  page: number;
  pageSize: number;
  total: number;
  items: AuditLogRecord[];
};

type ServerAuditLogRecord = {
  id: string;
  createdAt: string;
  appUserId?: string | null;
  operator: string;
  action: string;
  entityName: string;
  entityId?: string | null;
  details?: string | null;
};

export async function getAuditLogs(filters: AuditLogFilters = {}): Promise<AuditLogPage> {
  const params = new URLSearchParams();
  if (filters.from) params.set('from', filters.from.toISOString());
  if (filters.to) params.set('to', filters.to.toISOString());
  if (filters.operator?.trim()) params.set('operator', filters.operator.trim());
  if (filters.action?.trim()) params.set('action', filters.action.trim());
  if (filters.entityName?.trim()) params.set('entityName', filters.entityName.trim());
  if (filters.search?.trim()) params.set('search', filters.search.trim());
  params.set('page', String(filters.page ?? 1));
  params.set('pageSize', String(filters.pageSize ?? 50));

  const response = await fetch('/api/audit?' + params.toString());
  const payload = await response.json().catch(() => null) as { message?: string } | null;
  if (!response.ok) {
    throw new Error(payload?.message || 'دریافت سوابق Audit انجام نشد');
  }

  const data = payload as unknown as {
    page: number;
    pageSize: number;
    total: number;
    items: ServerAuditLogRecord[];
  };

  return {
    page: data.page,
    pageSize: data.pageSize,
    total: data.total,
    items: data.items.map(item => ({
      id: item.id,
      createdAt: item.createdAt,
      operator: item.operator,
      action: item.action,
      target: item.entityId ? item.entityName + ' · ' + item.entityId : item.entityName,
      details: item.details ?? '',
    })),
  };
}
