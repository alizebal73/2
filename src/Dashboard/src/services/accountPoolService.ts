import type { AccountRecord } from '../types';

type AccountDto = {
  id: string;
  title: string;
  platform: string;
  login?: string | null;
  owner: string;
  expiresAt?: string | null;
  allowedGames: string[];
  status: string;
  assignedClient?: string | null;
  assignedAgentDeviceId?: string | null;
};

type LeaseDto = {
  leaseId: string;
  accountId: string;
  gameId: string;
  accountTitle: string;
  platform: string;
  login?: string | null;
  assignedClient?: string | null;
  leasedAt: string;
  state: string;
};

async function readError(response: Response, fallback: string) {
  const payload = await response.json().catch(() => null) as { message?: string } | null;
  return payload?.message || fallback;
}

function platform(value: string): AccountRecord['platform'] {
  return value === 'Battle.net' || value === 'Riot' || value === 'Epic' ? value : 'Steam';
}

function status(value: string): AccountRecord['status'] {
  return value === 'InUse' || value === 'in-use' ? 'in-use' : value === 'Locked' || value === 'locked' ? 'locked' : 'free';
}

function mapAccount(row: AccountDto): AccountRecord {
  return {
    id: row.id,
    title: row.title,
    platform: platform(row.platform),
    status: status(row.status),
    owner: row.owner,
    expiresAt: row.expiresAt ? new Date(row.expiresAt).toLocaleDateString('fa-IR') : '',
    allowedGames: row.allowedGames,
    assignedClient: row.assignedClient || '',
    guardStatus: 'محافظت‌شده',
  };
}

export async function getServerAccountPool(): Promise<AccountRecord[]> {
  const response = await fetch('/api/account-pool');
  if (!response.ok) throw new Error(await readError(response, 'دریافت استخر اکانت‌ها از سرور انجام نشد'));
  return (await response.json() as AccountDto[]).map(mapAccount);
}

export async function saveServerAccount(account: AccountRecord, allowedGameIds: string[] = []) {
  const payload = {
    title: account.title,
    platform: account.platform,
    login: null,
    secret: null,
    owner: account.owner,
    expiresAt: account.expiresAt || null,
    allowedGameIds,
    status: account.status === 'in-use' ? 'InUse' : account.status === 'locked' ? 'Locked' : 'Free',
  };
  const response = await fetch('/api/account-pool/' + account.id, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  if (!response.ok) throw new Error(await readError(response, 'ویرایش اکانت در سرور انجام نشد'));
  return getServerAccountPool();
}

export async function createServerAccount(input: {
  title: string;
  platform: AccountRecord['platform'];
  owner: string;
  allowedGameIds: string[];
  secret?: string;
}) {
  const response = await fetch('/api/account-pool', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      title: input.title,
      platform: input.platform,
      login: null,
      secret: input.secret || null,
      owner: input.owner || 'مجموعه',
      expiresAt: null,
      allowedGameIds: input.allowedGameIds,
      status: 'Free',
    }),
  });
  if (!response.ok) throw new Error(await readError(response, 'ثبت اکانت در سرور انجام نشد'));
  return getServerAccountPool();
}

export async function unlockServerAccount(id: string) {
  const response = await fetch('/api/account-pool/' + id + '/unlock', { method: 'POST' });
  if (!response.ok) throw new Error(await readError(response, 'رفع قفل اکانت انجام نشد'));
  return getServerAccountPool();
}

export async function getServerLeases(): Promise<LeaseDto[]> {
  const response = await fetch('/api/account-pool/leases');
  if (!response.ok) throw new Error(await readError(response, 'دریافت Leaseهای فعال انجام نشد'));
  return response.json() as Promise<LeaseDto[]>;
}

export async function allocateServerAccount(gameId: string, agentDeviceId?: string) {
  const response = await fetch('/api/account-pool/allocate', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ gameId, agentDeviceId: agentDeviceId || null }),
  });
  if (!response.ok) throw new Error(await readError(response, 'تخصیص اکانت انجام نشد'));
  return response.json() as Promise<LeaseDto>;
}

export async function releaseServerLease(leaseId: string, reason?: string) {
  const response = await fetch('/api/account-pool/leases/' + leaseId + '/release', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason: reason || null }),
  });
  if (!response.ok) throw new Error(await readError(response, 'آزادسازی اکانت انجام نشد'));
}
