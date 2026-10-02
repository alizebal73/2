import type { AccountRecord } from '../types';

async function readError(response: Response, fallback: string) {
  const payload = await response.json().catch(() => null) as { message?: string } | null;
  return payload?.message || fallback;
}

type ServerAccount = {
  id: string; title: string; platform: string; launcher?: string | null; login?: string | null;
  status: 'free' | 'in-use' | 'locked'; owner: string; expiresAt?: string | null;
  allowedGameIds: string[]; allowedGames: Array<{ id: string; name: string }>;
  assignedClientId?: string | null; assignedClientName?: string | null;
  activeGameId?: string | null; activeGameName?: string | null; guardStatus: string; active: boolean;
};

function mapAccount(row: ServerAccount): AccountRecord {
  return {
    id: row.id, title: row.title,
    platform: row.platform === 'Battle.net' || row.platform === 'Riot' || row.platform === 'Epic' ? row.platform : 'Steam',
    status: row.status, owner: row.owner, expiresAt: row.expiresAt ? row.expiresAt.slice(0, 10) : '',
    allowedGames: row.allowedGames.map(item => item.name), allowedGameIds: row.allowedGameIds ?? row.allowedGames.map(item => item.id),
    assignedClient: row.assignedClientName ?? '', assignedClientId: row.assignedClientId ?? '',
    assignedGameId: row.activeGameId ?? '', assignedGame: row.activeGameName ?? '',
    guardStatus: row.guardStatus === '2FA' || row.guardStatus === 'نیازمند بررسی' ? row.guardStatus : 'محافظت‌شده',
    launcher: row.launcher ?? '', login: row.login ?? ''
  };
}

function toPayload(account: AccountRecord) {
  return {
    title: account.title.trim(), platform: account.platform, launcher: account.launcher || null,
    login: account.login || null, password: account.password?.trim() || null, owner: account.owner || 'مجموعه',
    expiresAt: account.expiresAt.trim() ? account.expiresAt.trim() + 'T23:59:59Z' : null,
    guardStatus: account.guardStatus, allowedGameIds: account.allowedGameIds, active: true
  };
}

export async function getServerAccounts(query = '') {
  const suffix = query.trim() ? '?q=' + encodeURIComponent(query.trim()) : '';
  const response = await fetch('/api/game-accounts' + suffix);
  if (!response.ok) throw new Error(await readError(response, 'دریافت Account Pool از سرور انجام نشد'));
  return (await response.json() as ServerAccount[]).map(mapAccount);
}

export async function createServerAccount(account: AccountRecord) {
  const response = await fetch('/api/game-accounts', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(toPayload(account)) });
  if (!response.ok) throw new Error(await readError(response, 'ثبت اکانت در سرور انجام نشد'));
  return mapAccount(await response.json() as ServerAccount);
}

export async function updateServerAccount(account: AccountRecord) {
  const response = await fetch('/api/game-accounts/' + account.id, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(toPayload(account)) });
  if (!response.ok) throw new Error(await readError(response, 'ویرایش اکانت در سرور انجام نشد'));
  return mapAccount(await response.json() as ServerAccount);
}

async function action(id: string, name: 'lock' | 'unlock' | 'lease' | 'release', body?: unknown) {
  const response = await fetch('/api/game-accounts/' + id + '/' + name, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body)
  });
  if (!response.ok) throw new Error(await readError(response, 'عملیات Account Pool انجام نشد'));
  return mapAccount(await response.json() as ServerAccount);
}

export const lockServerAccount = (id: string) => action(id, 'lock');
export const unlockServerAccount = (id: string) => action(id, 'unlock');
export const releaseServerAccount = (id: string) => action(id, 'release');
export const leaseServerAccount = (id: string, gameId: string, agentDeviceId: string) => action(id, 'lease', { gameId, agentDeviceId });

export async function getServerAccountLogs() {
  const response = await fetch('/api/game-accounts/logs');
  if (!response.ok) throw new Error(await readError(response, 'دریافت لاگ Account Pool انجام نشد'));
  return await response.json() as Array<{ id: string; createdAt: string; action: string; details?: string | null; operator?: string | null }>;
}

export async function getServerAccountClients() {
  const response = await fetch('/api/agent/devices');
  if (!response.ok) throw new Error(await readError(response, 'دریافت کلاینت‌ها انجام نشد'));
  return await response.json() as Array<{ id: string; name: string; isOnline: boolean; lifecycleState: string }>;
}
