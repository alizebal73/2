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
  | 'operations'
  | 'stations';

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
  remainingMinutes?: number;
  customerUsername?: string;
  customerFullName?: string;
  customerDebt?: number;
  customerNote?: string;
  agentId?: string | null;
  agentOnline?: boolean;
  agentLastSeenAt?: string | null;
  agentVersion?: string | null;
  agentLocked?: boolean;
  agentKioskEnabled?: boolean;
  agentLockOnDisconnect?: boolean;
  agentLifecycleState?: string | null;
  agentPendingUpdateVersion?: string | null;
  agentLastUpdateError?: string | null;
  agentLastHealthyAt?: string | null;
  sessionStartedAt?: string | null;
  sessionPausedAt?: string | null;
  sessionPausedMinutes?: number;
  sessionTimeAdjustmentMinutes?: number;
  sessionPrepaidAmount?: number;
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

export type PendingSettlementCharge = {
  id: string;
  amount: number;
  method: 'cash' | 'card' | 'wallet';
  createdAt: string;
};

export type PendingSettlementBuffetItem = {
  productId: string;
  productName: string;
  quantity: number;
  amount: number;
};

export type PendingSettlementAccount = {
  invoiceId: string;
  sessionId: string;
  customerId: string;
  customerName: string;
  customerCode?: string;
  username?: string;
  stationName: string;
  closedAt: string;
  waitingMinutes: number;
  timeAmount: number;
  buffetTotal: number;
  otherAmount: number;
  grossAmount: number;
  prepaidTotal: number;
  prepaidApplied: number;
  prepaidRemaining: number;
  creditOrBenefitReduction: number;
  amountDue: number;
  charges: PendingSettlementCharge[];
  buffetItems: PendingSettlementBuffetItem[];
};

export type DashboardSnapshotDto = {
  totalStations: number;
  stations: StationDto[];
  generatedAt: string;
};

export type AgentStatusDto = {
  agentId: string;
  deviceId: string;
  name: string;
  stationId?: string | null;
  stationName?: string | null;
  isOnline: boolean;
  isLocked: boolean;
  kioskEnabled: boolean;
  lockOnDisconnect: boolean;
  lastSeenAt?: string | null;
  connectedAt?: string | null;
  agentVersion?: string | null;
  osVersion?: string | null;
  cpuUsagePercent?: number | null;
  memoryAvailableBytes?: number | null;
  uptimeSeconds?: number | null;
  lifecycleState: string;
  pendingUpdateVersion?: string | null;
  lastUpdateError?: string | null;
  lastHealthyAt?: string | null;
  lifecycleStateChangedAt?: string | null;
};

export type AgentCommandStatusDto = {
  commandId: string;
  agentDeviceId: string;
  commandType: string;
  status: string;
  requestedAt: string;
  sentAt?: string | null;
  completedAt?: string | null;
  succeeded?: boolean | null;
  resultMessage?: string | null;
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
  vip: 'gold' | 'silver' | 'bronze' | 'custom' | 'none';
  wallet: number;
  debt: number;
  giftCredit: number;
  freeTimeMinutes?: number;
  discountLevel: number;
  packageName?: string;
  vipPackageId?: string;
  vipActivatedAt?: string;
  vipExpiresAt?: string;
  vipDailyMinutes?: number;
  vipTotalMinutes?: number;
  vipDiscountPercent?: number;
  username: string;
  lastSeen: string;
  status: 'active' | 'warning' | 'locked';
  hoursUsedToday?: number;
  dailyHourCap?: number;
  transactionHistory?: string[];
  concurrentLoginLimit?: number;
  notes?: string;
};

export type ProductRecord = {
  id: string;
  name: string;
  category: string;
  price: number;
  buyPrice: number;
  stock: number;
  warehouseStock: number;
  showcaseStock: number;
  minimumStock: number;
  unit: string;
  lowStock: boolean;
  maxStock: number;
  todaySold?: number;
  todayRevenue?: number;
};

export type AppUserRecord = {
  id: string;
  fullName: string;
  userName: string;
  email: string;
  role: string;
  isActive: boolean;
  lastLoginAt?: string | null;
  permissions: string[];
};

export type ApprovalRecord = {
  id: string;
  action: string;
  entityName: string;
  entityId?: string | null;
  reason: string;
  status: string;
  requestedByUserId: string;
  requestedBy: string;
  decidedByUserId?: string | null;
  decidedBy?: string | null;
  decisionNote?: string | null;
  createdAt: string;
  decidedAt?: string | null;
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
  phone?: string;
  employmentStartDate?: string | null;
  workSchedule?: string | null;
  notes?: string | null;
  workStart?: string;
  workEnd?: string;
  bonusTotal?: number;
  deductionTotal?: number;
  paidSalaryTotal?: number;
  employeePayable?: number;
  ownerReceivable?: number;
  damageTotal?: number;
  advanceTotal?: number;
  lastPaymentAt?: string;
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
  login: string;
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
  serverReferenceId?: string;
};


export type CustomerVipUsage = {
  active: boolean;
  usedTodayMinutes: number;
  remainingTodayMinutes: number;
  usedTotalMinutes: number;
  remainingTotalMinutes: number;
};

export type CustomerHistoryItem = {
  id: string;
  type: string;
  description: string;
  amount: number;
  createdAt: string;
  referenceId?: string;
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


export type InventoryTransactionRecord = {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice?: number;
  unitCost?: number;
  referenceInvoiceId?: string | null;
  direction: 'In' | 'Out';
  stockArea?: 'Warehouse' | 'Showcase' | string;
  kind: 'Initial' | 'Adjustment' | 'Purchase' | 'Sale' | 'Waste' | 'Return' | 'ShowcaseTransfer' | string;
  notes?: string;
  createdAt: string;
};

export type BuffetTodaySaleRecord = {
  productId: string;
  productName: string;
  unit: string;
  quantity: number;
  revenue: number;
};

export type BuffetTodaySalesReport = {
  date: string;
  totalQuantity: number;
  totalRevenue: number;
  products: BuffetTodaySaleRecord[];
};

export type BuffetProfitReport = {
  from: string;
  to: string;
  totals: {
    salesRevenue: number;
    salesCost: number;
    returnRevenue: number;
    returnCost: number;
    purchaseCost: number;
    wasteCost: number;
    grossProfit: number;
  };
  products: Array<{
    productId: string;
    productName: string;
    salesQuantity: number;
    salesRevenue: number;
    salesCost: number;
    returnQuantity: number;
    returnRevenue: number;
    returnCost: number;
    purchaseQuantity: number;
    purchaseCost: number;
    wasteQuantity: number;
    wasteCost: number;
    grossProfit: number;
  }>;
};


export type PayrollUserRecord = {
  userId: string;
  fullName: string;
  payType: 'hourly' | 'monthly' | string;
  phone?: string;
  hourlyRate: number;
  monthlySalary: number;
  overtimeRate: number;
  employmentStartDate?: string | null;
  workSchedule?: string | null;
  notes?: string | null;
  isActive: boolean;
  employeePayable: number;
  ownerReceivable: number;
  accruedThisMonth: number;
  paidThisMonth: number;
  bonusTotal: number;
  deductionTotal: number;
  damageTotal: number;
  advanceTotal: number;
  lastPaymentAt?: string | null;
};

export type PayrollLedgerEntry = {
  id: string;
  userId: string;
  userName: string;
  kind: string;
  amount: number;
  employeePayableDelta: number;
  ownerReceivableDelta: number;
  reason: string;
  status: string;
  createdByUserId: string;
  createdAt: string;
  approvedByUserId?: string | null;
  approvedAt?: string | null;
  paymentMethod?: string | null;
  receiptNumber?: string | null;
};
