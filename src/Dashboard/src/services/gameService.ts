import type { GameRecord } from '../types';

async function readError(response: Response, fallback: string) {
  const payload = await response.json().catch(() => null) as { message?: string } | null;
  return payload?.message || fallback;
}

type GameDto = {
  id: string;
  name: string;
  version: string;
  genre?: string | null;
  status: string;
  activeUsers: number;
  path: string;
  executable: string;
  cover: string;
  trailer: string;
  launchArgs: string;
  connectionType: string;
  active: boolean;
  targetSystem: string;
  target: string;
  targetZone: string;
  targetStations: string;
};

export function mapGame(row: GameDto): GameRecord {
  return {
    id: row.id,
    name: row.name,
    version: row.version,
    category: row.genre || 'عمومی',
    status: row.status === 'online' || row.status === 'program' ? row.status : 'offline',
    activeUsers: row.activeUsers,
    path: row.path,
    executable: row.executable,
    cover: row.cover,
    trailer: row.trailer,
    launchArgs: row.launchArgs,
    connectionType: row.connectionType,
    active: row.active,
    targetSystem: row.targetSystem === 'vip' || row.targetSystem === 'standard' ? row.targetSystem : 'all',
    target: row.target === 'zone' || row.target === 'stations' ? row.target : 'all',
    targetZone: row.targetZone,
    targetStations: row.targetStations,
  };
}

export async function getServerGames(): Promise<GameRecord[]> {
  const response = await fetch('/api/games');
  if (!response.ok) throw new Error(await readError(response, 'دریافت بازی‌ها از سرور انجام نشد'));
  return (await response.json() as GameDto[]).map(mapGame);
}

export async function saveServerGame(game: GameRecord): Promise<GameRecord> {
  const payload = {
    name: game.name,
    version: game.version || null,
    genre: game.category || null,
    status: game.status,
    path: game.path,
    executable: game.executable,
    cover: game.cover,
    trailer: game.trailer,
    launchArgs: game.launchArgs,
    connectionType: game.connectionType,
    active: game.active,
    targetSystem: game.targetSystem,
    target: game.target,
    targetZone: game.targetZone,
    targetStations: game.targetStations,
  };
  const response = await fetch(game.id ? '/api/games/' + game.id : '/api/games', {
    method: game.id ? 'PUT' : 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  if (!response.ok) throw new Error(await readError(response, 'ذخیره بازی در سرور انجام نشد'));
  const result = await response.json();
  return result?.id ? mapGame(result as GameDto) : game;
}

export async function archiveServerGame(id: string) {
  const response = await fetch('/api/games/' + id, { method: 'DELETE' });
  if (!response.ok) throw new Error(await readError(response, 'غیرفعال‌سازی بازی انجام نشد'));
}

