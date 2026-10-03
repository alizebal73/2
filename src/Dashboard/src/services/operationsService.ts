import type {
  AuditLogRecord,
  ExpenseRecord,
  ManagementInvoiceRecord,
  ReservationRecord,
  StationManagementRecord,
  VipPackageRecord,
} from '../types';
import { getServerProducts, createServerProduct, updateServerProduct, adjustServerStock } from './buffetService';
import { getVipPackages, assignVipPackage } from './vipPackageService';
import { getFinanceExpenses, createShiftExpense, getFinanceTransactions } from './financeService';
import { getCurrentShift } from './shiftService';
import { getAuditReport } from './reportService';

export type ServerStation = {
  id: string;
  name: string;
  zone: string;
  type: string;
  ratePerHour: number;
  state: string;
  stationTypeId: string;
  tariffId?: string | null;
  networkRoute: string;
  isActive: boolean;
};

export type ServerReservation = {
  id: string;
  customerId: string;
  customerCode: string;
  customerName: string;
  stationId: string;
  stationName: string;
  startAt: string;
  endAt: string;
  status: string;
  kind: string;
  priority: number;
  notes?: string | null;
};

async function readError(response: Response, fallback: string) {
  const payload = await response.json().catch(() => null) as { message?: string } | null;
  return payload?.message || fallback;
}

async function json<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, init);
  if (!response.ok) throw new Error(await readError(response, 'عملیات سرور انجام نشد'));
  return await response.json() as T;
}

export async function getManagedStations(): Promise<StationManagementRecord[]> {
  const rows = await json<ServerStation[]>('/api/stations');
  return rows.map(row => ({
    id: row.id,
    name: row.name,
    zone: row.zone === 'PC' ? 'pc' : row.zone.toLowerCase().includes('console') ? 'console' : 'table',
    type: row.type,
    ratePerHour: row.ratePerHour,
    status: row.isActive && row.state !== 'Offline' ? 'active' : 'off',
    note: row.networkRoute,
    stationTypeId: row.stationTypeId,
    tariffId: row.tariffId,
    networkRoute: row.networkRoute,
  }));
}

export async function saveManagedStation(
  form: StationManagementRecord,
  references: { stationTypeId: string; tariffId?: string | null },
): Promise<StationManagementRecord> {
  const payload = {
    name: form.name,
    zone: form.zone,
    type: form.type,
    stationTypeId: references.stationTypeId,
    tariffId: references.tariffId ?? null,
    ratePerHour: form.ratePerHour,
    networkRoute: form.networkRoute || form.note || 'internet1',
    isActive: form.status !== 'off',
  };
  const row = await json<ServerStation>(
    form.id ? '/api/stations/' + form.id : '/api/stations',
    {
      method: form.id ? 'PUT' : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    },
  );
  return (await getManagedStations()).find(item => item.id === row.id) ?? {
    id: row.id, name: row.name, zone: 'pc', type: row.type, ratePerHour: row.ratePerHour,
    status: row.isActive ? 'active' : 'off', note: row.networkRoute,
  };
}

export async function toggleStation(id: string, active: boolean, references: { stationTypeId: string; tariffId?: string | null }, current: StationManagementRecord) {
  return saveManagedStation({ ...current, status: active ? 'active' : 'off' }, references);
}

export async function getReservations(): Promise<ReservationRecord[]> {
  const rows = await json<ServerReservation[]>('/api/reservations?from=' + encodeURIComponent(new Date(Date.now() - 86400000).toISOString()) + '&to=' + encodeURIComponent(new Date(Date.now() + 7 * 86400000).toISOString()));
  return rows.map(row => ({
    id: row.id,
    stationId: row.stationId,
    stationName: row.stationName,
    customerCode: row.customerCode,
    customerName: row.customerName,
    reservedAt: row.startAt,
    durationMinutes: Math.max(1, Math.round((new Date(row.endAt).getTime() - new Date(row.startAt).getTime()) / 60000)),
    status: row.status.toLowerCase() as ReservationRecord['status'],
    note: row.notes ?? undefined,
  }));
}

export async function getWaitlist() {
  const rows = await json<ServerReservation[]>('/api/waitlist');
  return rows.map(row => ({
    id: row.id,
    customerCode: row.customerCode,
    customerName: row.customerName,
    stationType: row.stationName,
    createdAt: row.startAt,
    status: row.status.toLowerCase() === 'pending' ? 'waiting' as const : 'assigned' as const,
  }));
}

export async function createReservation(input: {
  customerId: string;
  stationId: string;
  startAt: string;
  durationMinutes: number;
  kind?: 'reservation' | 'waitlist';
  priority?: number;
  notes?: string;
}) {
  return json<ServerReservation>('/api/reservations', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      customerId: input.customerId,
      stationId: input.stationId,
      startAt: input.startAt,
      durationMinutes: input.durationMinutes,
      kind: input.kind ?? 'reservation',
      priority: input.priority ?? 0,
      notes: input.notes ?? null,
    }),
  });
}

export async function transitionReservation(id: string, action: string, targetStationId?: string) {
  return json<ServerReservation>('/api/reservations/' + id + '/transition', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ action, targetStationId: targetStationId ?? null }),
  });
}

export async function getManagementExpenses(): Promise<ExpenseRecord[]> {
  const rows = await getFinanceExpenses();
  return rows.map(item => ({
    id: item.id,
    title: item.description ?? item.category,
    amount: item.amount,
    category: item.category,
    createdAt: item.createdAt,
    operator: 'سرور',
  }));
}

export async function addExpense(input: { title: string; amount: number; category: string }) {
  const shift = await getCurrentShift();
  if (!shift) throw new Error('برای ثبت هزینه ابتدا یک شیفت باز کنید.');
  return createShiftExpense(shift.id, {
    amount: input.amount,
    category: input.category,
    description: input.title,
  });
}

export async function getAuditLogs(): Promise<AuditLogRecord[]> {
  const rows = await getAuditReport({ from: new Date(Date.now() - 30 * 86400000), to: new Date(), limit: 500 });
  return rows.map(item => ({
    id: item.id,
    createdAt: item.createdAt,
    operator: item.operatorName,
    action: item.action,
    target: item.entityName + (item.entityId ? ':' + item.entityId : ''),
    details: item.details ?? '',
  }));
}

export async function getManagementInvoices(): Promise<ManagementInvoiceRecord[]> {
  const rows = await getFinanceTransactions();
  return rows.map(item => ({
    id: item.id,
    stationId: '',
    stationName: item.description,
    customerCode: '—',
    durationMinutes: 0,
    timeAmount: item.amount,
    buffetAmount: 0,
    totalAmount: item.amount,
    paymentMethod: item.method as ManagementInvoiceRecord['paymentMethod'],
    closedAt: item.closedAt,
    status: item.status.toLowerCase() === 'paid' ? 'paid' : item.status.toLowerCase() === 'cancelled' ? 'void' : 'pending',
    operator: 'سرور',
  }));
}

export async function getVipPackagesForOperations() {
  return getVipPackages();
}

export async function saveVipPackage(form: VipPackageRecord) {
  if (form.id) return updateVipPackage(form.id, form);
  return json<VipPackageRecord>('/api/vip-packages', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      name: form.name,
      tier: form.tier,
      price: form.price,
      durationDays: 30,
      dailyMinutes: form.dailyMinutes,
      totalMinutes: form.totalMinutes,
      discountPercent: form.discount,
      overflowRule: 'half-hourly',
    }),
  });
}

export async function updateVipPackage(id: string, form: VipPackageRecord) {
  return json<VipPackageRecord>('/api/vip-packages/' + id, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      name: form.name,
      tier: form.tier,
      price: form.price,
      durationDays: 30,
      dailyMinutes: form.dailyMinutes,
      totalMinutes: form.totalMinutes,
      discountPercent: form.discount,
      overflowRule: 'half-hourly',
      isActive: form.active,
    }),
  });
}

export async function deleteVipPackage(id: string) {
  await json('/api/vip-packages/' + id, { method: 'DELETE' });
}

export {
  getServerProducts,
  createServerProduct,
  updateServerProduct,
  adjustServerStock,
  assignVipPackage,
};
