import type { AppUserRecord, ApprovalRecord } from '../types';

async function readError(response: Response, fallback: string) {
  try {
    const payload = await response.json() as { message?: string };
    return payload.message || fallback;
  } catch {
    return fallback;
  }
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
    ...init,
  });
  if (!response.ok) throw new Error(await readError(response, 'عملیات احراز هویت انجام نشد'));
  return await response.json() as T;
}

export async function login(userName: string, password: string): Promise<AppUserRecord> {
  return await request<AppUserRecord>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ userName, password }),
  });
}

export async function getCurrentUser(): Promise<AppUserRecord | null> {
  const response = await fetch('/api/auth/me', { credentials: 'include' });
  if (response.status === 401) return null;
  if (!response.ok) throw new Error(await readError(response, 'دریافت کاربر فعلی انجام نشد'));
  return await response.json() as AppUserRecord;
}

export async function logout(): Promise<void> {
  await request('/api/auth/logout', { method: 'POST' });
}

export async function getUsers(): Promise<AppUserRecord[]> {
  return await request<AppUserRecord[]>('/api/users');
}

export async function createUser(input: {
  fullName: string;
  userName: string;
  email: string;
  password: string;
  role: string;
  isActive?: boolean;
}): Promise<AppUserRecord> {
  return await request<AppUserRecord>('/api/users', { method: 'POST', body: JSON.stringify(input) });
}

export async function updateUser(id: string, input: {
  fullName: string;
  userName: string;
  email: string;
  password?: string;
  role: string;
  isActive: boolean;
}): Promise<AppUserRecord> {
  return await request<AppUserRecord>(`/api/users/${id}`, { method: 'PUT', body: JSON.stringify(input) });
}

export async function getPermissions(): Promise<Array<{ id: string; name: string; description?: string }>> {
  return await request('/api/permissions');
}

export async function setUserPermissions(userId: string, permissionNames: string[]): Promise<void> {
  await request(`/api/users/${userId}/permissions`, {
    method: 'PUT',
    body: JSON.stringify({ permissionNames }),
  });
}

export async function getApprovals(): Promise<ApprovalRecord[]> {
  return await request<ApprovalRecord[]>('/api/approvals');
}

export async function createApproval(input: {
  action: string;
  entityName: string;
  entityId?: string;
  reason: string;
}): Promise<{ id: string; status: string }> {
  return await request('/api/approvals', { method: 'POST', body: JSON.stringify(input) });
}

export async function decideApproval(id: string, approved: boolean, note?: string): Promise<void> {
  await request(`/api/approvals/${id}/${approved ? 'approve' : 'reject'}`, {
    method: 'POST',
    body: JSON.stringify({ note }),
  });
}


export function hasPermission(user: AppUserRecord, permission: string): boolean {
  const role = user.role.toLowerCase();
  return role === 'admin' || role === 'owner' || user.permissions.includes(permission);
}
