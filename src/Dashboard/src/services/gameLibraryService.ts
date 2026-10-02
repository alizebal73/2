import type { GameRecord } from '../types';

async function readError(response: Response, fallback: string) {
  const payload = await response.json().catch(() => null) as { message?: string } | null;
  return payload?.message || fallback;
}

type ServerGame = {
  id: string; name: string; version?: string | null; category?: string | null; launcher?: string | null;
  installPath?: string | null; executablePath?: string | null; launchArguments?: string | null;
  connectionType?: string | null; targetSystem?: string | null; targetZone?: string | null;
  targetScope?: string | null; targetStations?: string | null; processNames?: string | null;
  coverPath?: string | null; trailerPath?: string | null; active: boolean;
  status: 'online' | 'offline' | 'program'; activeUsers: number;
};

function mapGame(row: ServerGame): GameRecord {
  return {
    id: row.id, name: row.name, version: row.version ?? '', category: row.category ?? 'سایر',
    status: row.status, activeUsers: row.activeUsers ?? 0, path: row.installPath ?? '',
    executable: row.executablePath ?? '', cover: row.coverPath ?? '', trailer: row.trailerPath ?? '',
    launchArgs: row.launchArguments ?? '', connectionType: row.connectionType ?? 'برنامه',
    launcher: row.launcher ?? '', processNames: row.processNames ?? '', active: row.active,
    targetSystem: row.targetSystem === 'vip' || row.targetSystem === 'standard' ? row.targetSystem : 'all',
    target: row.targetScope === 'zone' || row.targetScope === 'stations' ? row.targetScope : 'all',
    targetZone: row.targetZone ?? 'pc', targetStations: row.targetStations ?? ''
  };
}

function toPayload(game: GameRecord) {
  return {
    name: game.name.trim(), version: game.version || null, category: game.category || null,
    launcher: game.launcher || null, installPath: game.path || null, executablePath: game.executable || null,
    launchArguments: game.launchArgs || null, connectionType: game.connectionType || null,
    targetSystem: game.targetSystem, targetZone: game.targetZone || null, targetScope: game.target,
    targetStations: game.targetStations || null, processNames: game.processNames || null,
    coverPath: game.cover || null, trailerPath: game.trailer || null, active: game.active
  };
}

async function send(url: string, method: 'POST' | 'PUT', game: GameRecord) {
  const response = await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(toPayload(game)) });
  if (!response.ok) throw new Error(await readError(response, 'ذخیره بازی در سرور انجام نشد'));
  return mapGame(await response.json() as ServerGame);
}

export async function getServerGames(query = '') {
  const suffix = query.trim() ? '?q=' + encodeURIComponent(query.trim()) : '';
  const response = await fetch('/api/games' + suffix);
  if (!response.ok) throw new Error(await readError(response, 'دریافت بازی‌ها از سرور انجام نشد'));
  return (await response.json() as ServerGame[]).map(mapGame);
}

export function createServerGame(game: GameRecord) { return send('/api/games', 'POST', game); }
export function updateServerGame(game: GameRecord) { return send('/api/games/' + game.id, 'PUT', game); }

export async function deleteServerGame(id: string) {
  const response = await fetch('/api/games/' + id, { method: 'DELETE' });
  if (!response.ok) throw new Error(await readError(response, 'غیرفعال‌سازی بازی انجام نشد'));
  return mapGame(await response.json() as ServerGame);
}
