import type { DashboardSnapshotDto, StationDto, StationState, ZoneKey } from '../types';

type RawStation = Omit<StationDto, 'state' | 'zone'> & {
  state: string;
  zone: string;
};

const stateMap: Record<string, StationState> = {
  available: 'free',
  free: 'free',
  occupied: 'busy',
  inuse: 'busy',
  in_use: 'busy',
  busy: 'busy',
  paused: 'paused',
  reserved: 'reserved',
  resv: 'reserved',
  maintenance: 'off',
  outofservice: 'off',
  out_of_service: 'off',
  offline: 'off',
};

function normalizeState(state: string): StationState {
  const key = state.replace(/[\s-]/g, '').toLowerCase();
  return stateMap[key] ?? stateMap[state.toLowerCase()] ?? 'off';
}

function normalizeZone(zone: string, type: string): ZoneKey {
  const value = `${zone} ${type}`.toLowerCase();
  if (/console|ps[45]/.test(value)) return 'console';
  if (/table|foosball/.test(value)) return 'table';
  if (/pc|computer|zone\s*b/.test(value)) return 'pc';
  return 'pc';
}

export function normalizeDashboardSnapshot(snapshot: DashboardSnapshotDto): DashboardSnapshotDto {
  const stations = (snapshot.stations as RawStation[]).map((station) => ({
    ...station,
    zone: normalizeZone(station.zone, station.type),
    state: normalizeState(station.state),
  }));

  return { ...snapshot, stations };
}
