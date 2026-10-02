export type PageKey =
  | 'dashboard'
  | 'customers'
  | 'buffet'
  | 'reports'
  | 'users'
  | 'settings'
  | 'tariffs'
  | 'games'
  | 'accounts'
  | 'client-shell'
  | 'operations';

export type StationState = 'free' | 'busy' | 'paused' | 'reserved' | 'off';
export type ZoneKey = 'all' | 'pc' | 'console' | 'table';

export type StationDto = {
  id: string;
  name: string;
  zone: ZoneKey | string;
  type: string;
  ratePerHour: number;
  state: StationState | string;
  startedAt?: string;
  persons?: number;
  customerCode?: string;
  buffetTotal?: number;
  reservationAt?: string;
  sessionMinutes?: number;
  sessionRate?: number;
  amountSoFar?: number;
  pausedAt?: string;
  pausedMinutes?: number;
  sessionCredit?: number;
  prepaidEndsAt?: string;
  serverSessionId?: string;
  network?: 1 | 2;
  outOfServiceReason?: string;
};

export type StartSessionInput = {
  stationId: string;
  stationName: string;
  customerCode: string;
  hourlyRate: number;
  persons: number;
  paymentMode: 'settle-later' | 'prepaid';
};

export type SessionInvoice = {
  id: string;
  stationId: string;
  stationName: string;
  customerCode: string;
  durationMinutes: number;
  timeAmount: number;
  buffetAmount: number;
  totalAmount: number;
  paymentMethod: 'cash' | 'card' | 'wallet' | 'debt';
  closedAt: string;
};

export type DashboardSnapshotDto = {
  totalStations: number;
  stations: StationDto[];
  generatedAt: string;
};

export type ServerInfoDto = {
  name: string;
  environment: string;
  utcNow: string;
};

export type CustomerRecord = {
  id: string;
  code?: string;
  nationalId?: string;
  name: string;
  alias: string;
  mobile: string;
  vip: 'gold' | 'silver' | 'none';
  wallet: number;
  debt: number;
  giftCredit: number;
  freeTimeMinutes?: number;
  discountLevel: number;
  packageName?: string;
  username: string;
  lastSeen: string;
  status: 'active' | 'warning' | 'locked';
  hoursUsedToday?: number;
  dailyHourCap?: number;
  transactionHistory?: string[];
  concurrentLoginLimit?: number;
};

export type ProductRecord = {
  id: string;
  name: string;
  category: string;
  price: number;
  buyPrice: number;
  stock: number;
  maxStock: number;
};

export type UserRecord = {
  id: string;
  name: string;
  role: 'owner' | 'admin' | 'operator';
  shift: string;
  sales: number;
  permissions: string[];
  payType?: 'hourly' | 'monthly';
  hourlyRate?: number;
  monthlySalary?: number;
  overtimeRate?: number;
  workStart?: string;
  workEnd?: string;
  bonusTotal?: number;
  deductionTotal?: number;
};

export type PricingScheduleRule = {
  id: string;
  weekdays: number[];
  startMinute: number;
  endMinute: number;
  pricePerHour: number;
  minimumCharge?: number;
  priority?: number;
  active?: boolean;
};

export type TariffRecord = {
  id: string;
  title: string;
  stationType: 'PC' | 'PS5' | 'PS4' | 'فوتبال‌دستی';
  tier: 'normal' | 'vip';
  pricePerHour: number;
  daily: number;
  vipDiscount: number;
  nightRate: number;
  nightHours: string;
  active: boolean;
  minimumCharge?: number;
  roundingStep?: number;
  schedule?: PricingScheduleRule[];
};

export type GameRecord = {
  id: string;
  name: string;
  version: string;
  category: string;
  status: 'online' | 'offline' | 'program';
  activeUsers: number;
  path: string;
  executable: string;
  cover: string;
  trailer: string;
  launchArgs: string;
  connectionType: string;
  active: boolean;
  targetSystem: 'all' | 'vip' | 'standard';
  target: 'all' | 'zone' | 'stations';
  targetZone: string;
  targetStations: string;
};

export type AccountRecord = {
  id: string;
  title: string;
  platform: 'Steam' | 'Battle.net' | 'Riot' | 'Epic';
  status: 'free' | 'in-use' | 'locked';
  owner: string;
  expiresAt: string;
  allowedGames: string[];
  assignedClient: string;
  guardStatus: '2FA' | 'محافظت‌شده' | 'نیازمند بررسی';
};

export type ClientRecord = {
  id: string;
  name: string;
  type: string;
  version: string;
  online: boolean;
  lastSync: string;
  ip: string;
  dns1: string;
  dns2: string;
  systemNumber: number;
  serverAddress: string;
  shell: boolean;
  network: 'internet1' | 'internet2' | 'lan';
  bootMode: 'normal' | 'ccboot' | 'pxe';
  user: string;
  game: string;
  updatePending: boolean;
  internetEnabled: boolean;
  locked: boolean;
};

export type SettingGroup = {
  id: string;
  title: string;
  description: string;
  enabled: boolean;
};


export type StationManagementRecord = {
  id: string;
  name: string;
  zone: ZoneKey;
  type: string;
  ratePerHour: number;
  status: 'active' | 'reserved' | 'off';
  ip?: string;
  note?: string;
};

export type ReservationRecord = {
  id: string;
  stationId: string;
  stationName: string;
  customerCode: string;
  customerName: string;
  reservedAt: string;
  durationMinutes: number;
  status: 'pending' | 'confirmed' | 'cancelled' | 'completed';
  note?: string;
};

export type VipPackageRecord = {
  id: string;
  name: string;
  tier: 'bronze' | 'silver' | 'gold' | 'custom';
  price: number;
  dailyMinutes: number;
  totalMinutes: number;
  discount: number;
  active: boolean;
};

export type AuditLogRecord = {
  id: string;
  createdAt: string;
  operator: string;
  action: string;
  target: string;
  details: string;
};

export type ManagementInvoiceRecord = SessionInvoice & {
  status: 'paid' | 'pending' | 'void';
  operator: string;
};

export type ExpenseRecord = {
  id: string;
  title: string;
  amount: number;
  category: string;
  createdAt: string;
  operator: string;
};

export type SessionTimelineEvent = {
  id: string;
  stationId: string;
  createdAt: string;
  kind: 'start' | 'pause' | 'resume' | 'charge' | 'extend' | 'reduce' | 'buffet' | 'settle' | 'note';
  title: string;
  detail: string;
  amount?: number;
};


export type WalletLedgerEntry = {
  id: string;
  customerId: string;
  amount: number;
  direction: 'credit' | 'debit';
  type: 'charge' | 'debit' | 'settlement' | 'refund' | 'adjustment';
  description: string;
  createdAt: string;
  balanceAfter: number;
  referenceTransactionId?: string;
};

export type PageLockRule = { enabled: boolean; pinHash: string; label: string };
export type PageLockMap = Partial<Record<PageKey, PageLockRule>>;
