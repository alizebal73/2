import type {
  AccountRecord,
  ClientRecord,
  CustomerRecord,
  GameRecord,
  ProductRecord,
  SessionInvoice,
  StartSessionInput,
  SettingGroup,
  TariffRecord,
  UserRecord,
  AuditLogRecord,
  ManagementInvoiceRecord,
  ReservationRecord,
  StationManagementRecord,
  VipPackageRecord,
  ExpenseRecord as TypedExpenseRecord,
  WalletLedgerEntry,
} from '../types';

const customers: CustomerRecord[] = [
  { id: 'c1', code: '1050', nationalId: '0012345678', name: 'رضا محمدی', alias: 'Reza-Headshot', mobile: '09123456789', vip: 'gold', wallet: 450000, debt: 0, giftCredit: 120000, discountLevel: 18, packageName: 'Gold VIP', username: 'reza_hs', lastSeen: '۵ دقیقه پیش', status: 'active', hoursUsedToday: 2.5, dailyHourCap: 4, transactionHistory: ['PS5-03 · ۲.۵ ساعت · ۲۵۰٬۰۰۰ تومان', 'پکیج گلد · ۱٬۸۰۰٬۰۰۰ تومان', 'بوفه · نوشابه ×۳ · ۹۰٬۰۰۰ تومان'] },
  { id: 'c2', code: '2020', nationalId: '0087654321', name: 'سروش نیک‌پور', alias: 'Soroush', mobile: '09120000002', vip: 'silver', wallet: 120000, debt: 35000, giftCredit: 5000, discountLevel: 10, packageName: 'Silver VIP', username: 'soroush.n', lastSeen: '۲۱ دقیقه پیش', status: 'warning', hoursUsedToday: 3.5, dailyHourCap: 5, transactionHistory: ['PC-09 · ۱ ساعت · ۹۵٬۰۰۰ تومان'] },
  { id: 'c3', code: '2021', nationalId: '0023456789', name: 'پارسا رضایی', alias: 'Parsa', mobile: '09120000003', vip: 'none', wallet: 0, debt: 20000, giftCredit: 0, discountLevel: 5, username: 'parsa.r', lastSeen: '۱ ساعت پیش', status: 'active', hoursUsedToday: 1, transactionHistory: ['بدهی ثبت‌شده · ۲۰٬۰۰۰ تومان'] },
  { id: 'c4', code: '1051', nationalId: '0076543210', name: 'مهدی جهان', alias: 'Mehdi', mobile: '09120000004', vip: 'gold', wallet: 470000, debt: 0, giftCredit: 25000, discountLevel: 22, packageName: 'Gold VIP', username: 'mehdi.j', lastSeen: 'حال حاضر', status: 'active', hoursUsedToday: 5, dailyHourCap: 4, transactionHistory: ['سقف روزانه تکمیل شد · مازاد نیم‌بها'] },
];

const activeCustomerLogins = new Map<string, Set<string>>();

const walletLedger: Record<string, WalletLedgerEntry[]> = {
  c1: [
    { id: 'wl-c1-1', customerId: 'c1', amount: 500000, direction: 'credit', type: 'charge', description: 'شارژ کیف پول', createdAt: new Date(Date.now() - 3 * 86400000).toISOString(), balanceAfter: 500000 },
    { id: 'wl-c1-2', customerId: 'c1', amount: 50000, direction: 'debit', type: 'settlement', description: 'تسویه جلسه PC-03', createdAt: new Date(Date.now() - 2 * 86400000).toISOString(), balanceAfter: 450000 },
  ],
  c2: [
    { id: 'wl-c2-1', customerId: 'c2', amount: 150000, direction: 'credit', type: 'charge', description: 'شارژ کیف پول', createdAt: new Date(Date.now() - 86400000).toISOString(), balanceAfter: 150000 },
    { id: 'wl-c2-2', customerId: 'c2', amount: 30000, direction: 'debit', type: 'settlement', description: 'تسویه جلسه PC-09', createdAt: new Date(Date.now() - 30 * 60000).toISOString(), balanceAfter: 120000 },
  ],
  c3: [
    { id: 'wl-c3-1', customerId: 'c3', amount: 20000, direction: 'credit', type: 'charge', description: 'شارژ کیف پول', createdAt: new Date(Date.now() - 2 * 86400000).toISOString(), balanceAfter: 20000 },
    { id: 'wl-c3-2', customerId: 'c3', amount: 20000, direction: 'debit', type: 'settlement', description: 'تسویه جلسه PC-11', createdAt: new Date(Date.now() - 90 * 60000).toISOString(), balanceAfter: 0 },
  ],
  c4: [
    { id: 'wl-c4-1', customerId: 'c4', amount: 470000, direction: 'credit', type: 'charge', description: 'شارژ کیف پول', createdAt: new Date(Date.now() - 4 * 86400000).toISOString(), balanceAfter: 470000 },
  ],
};

const products: ProductRecord[] = [
  { id: 'p1', name: 'انرژی درینک', category: 'نوشیدنی', price: 25000, buyPrice: 14000, stock: 42, minimumStock: 10, unit: 'عدد', lowStock: false, maxStock: 60 },
  { id: 'p2', name: 'پیتزا کوچک', category: 'غذا', price: 42000, buyPrice: 22000, stock: 18, minimumStock: 5, unit: 'عدد', lowStock: false, maxStock: 30 },
  { id: 'p3', name: 'چیپس', category: 'تنقلات', price: 18000, buyPrice: 9000, stock: 24, minimumStock: 6, unit: 'عدد', lowStock: false, maxStock: 40 },
  { id: 'p4', name: 'کاپ کیک', category: 'دسر', price: 14000, buyPrice: 7000, stock: 9, minimumStock: 3, unit: 'عدد', lowStock: false, maxStock: 20 },
];

const users: UserRecord[] = [
  { id: 'u1', name: 'رضا احمدی', role: 'owner', shift: 'صبح', sales: 18450000, permissions: ['مدیریت', 'گزارش', 'تنظیمات', 'کارمزد'], payType: 'monthly', monthlySalary: 0, paidSalaryTotal: 0, employeePayable: 0, ownerReceivable: 0, damageTotal: 0, advanceTotal: 0 },
  { id: 'u2', name: 'نرگس علیزاده', role: 'admin', shift: 'عصر', sales: 12240000, permissions: ['ایستگاه‌ها', 'مشتریان', 'بوفه', 'گزارش'], payType: 'monthly', monthlySalary: 18000000, paidSalaryTotal: 9000000, employeePayable: 9000000, ownerReceivable: 0, damageTotal: 0, advanceTotal: 0, bonusTotal: 500000, deductionTotal: 0 },
  { id: 'u3', name: 'حسین گل‌زاده', role: 'operator', shift: 'شب', sales: 9800000, permissions: ['ایستگاه‌ها', 'بوفه'], payType: 'hourly', hourlyRate: 85000, monthlySalary: 0, paidSalaryTotal: 4500000, employeePayable: 2100000, ownerReceivable: 350000, damageTotal: 600000, advanceTotal: 1250000, bonusTotal: 200000, deductionTotal: 100000 },
];

const tariffs: TariffRecord[] = [
  { id: 't1', title: 'رایانه - عادی', stationType: 'PC', tier: 'normal', pricePerHour: 95000, daily: 550000, vipDiscount: 0, nightRate: 115000, nightHours: '۲۲:۰۰ تا ۰۶:۰۰', active: true },
  { id: 't2', title: 'رایانه - VIP', stationType: 'PC', tier: 'vip', pricePerHour: 80000, daily: 500000, vipDiscount: 15, nightRate: 100000, nightHours: '۲۲:۰۰ تا ۰۶:۰۰', active: true },
  { id: 't3', title: 'PS5 - عادی', stationType: 'PS5', tier: 'normal', pricePerHour: 150000, daily: 780000, vipDiscount: 0, nightRate: 180000, nightHours: '۲۲:۰۰ تا ۰۶:۰۰', active: true },
  { id: 't4', title: 'PS4 - عادی', stationType: 'PS4', tier: 'normal', pricePerHour: 110000, daily: 650000, vipDiscount: 0, nightRate: 130000, nightHours: '۲۲:۰۰ تا ۰۶:۰۰', active: true },
  { id: 't5', title: 'فوتبال‌دستی - عادی', stationType: 'فوتبال‌دستی', tier: 'normal', pricePerHour: 60000, daily: 0, vipDiscount: 0, nightRate: 75000, nightHours: '۲۲:۰۰ تا ۰۶:۰۰', active: true },
];

const games: GameRecord[] = [
  { id: 'g1', name: 'Counter-Strike 2', version: '2.0', category: 'FPS', status: 'online', activeUsers: 34, path: 'D:\\Games\\Steam\\steamapps\\common\\Counter-Strike Global Offensive', executable: 'cs2.exe', cover: '🎯', trailer: '', launchArgs: '-novid', connectionType: 'آنلاین', active: true, targetSystem: 'all', target: 'all', targetZone: 'pc', targetStations: '', launcher: '', processNames: '' },
  { id: 'g2', name: 'Valorant', version: '11.0', category: 'FPS', status: 'online', activeUsers: 21, path: 'D:\\Riot Games\\VALORANT', executable: 'VALORANT-Win64-Shipping.exe', cover: '⚡', trailer: '', launchArgs: '', connectionType: 'آنلاین', active: true, targetSystem: 'standard', target: 'all', targetZone: 'pc', targetStations: '', launcher: '', processNames: '' },
  { id: 'g3', name: 'FIFA 25', version: '2025', category: 'Sports', status: 'offline', activeUsers: 14, path: 'D:\\Games\\EA SPORTS FC 25', executable: 'FC25.exe', cover: '⚽', trailer: '', launchArgs: '', connectionType: 'آفلاین', active: true, targetSystem: 'all', target: 'zone', targetZone: 'console', targetStations: '', launcher: '', processNames: '' },
  { id: 'g4', name: 'Fortnite', version: '25.10', category: 'Battle Royale', status: 'online', activeUsers: 28, path: 'D:\\Epic Games\\Fortnite', executable: 'FortniteClient-Win64-Shipping.exe', cover: '🪂', trailer: '', launchArgs: '', connectionType: 'آنلاین', active: true, targetSystem: 'vip', target: 'all', targetZone: 'pc', targetStations: '', launcher: '', processNames: '' },
  { id: 'g5', name: 'Discord', version: '1.0', category: 'برنامه', status: 'program', activeUsers: 40, path: 'C:\\Users\\Public\\Desktop', executable: 'Discord.exe', cover: '💬', trailer: '', launchArgs: '', connectionType: 'آنلاین', active: true, targetSystem: 'all', target: 'all', targetZone: 'pc', targetStations: '', launcher: '', processNames: '' },
];

const accounts: AccountRecord[] = [
  { id: 'a1', title: 'Steam-01', platform: 'Steam', status: 'in-use', owner: 'مجموعه', expiresAt: '۳ روز دیگر', allowedGames: ['Counter-Strike 2', 'Dota 2'], assignedClient: 'PC ۱۲', guardStatus: '2FA', allowedGameIds: ['g1', 'g2'] },
  { id: 'a2', title: 'Battle-02', platform: 'Battle.net', status: 'free', owner: 'مجموعه', expiresAt: '۱۲ ساعت دیگر', allowedGames: ['Overwatch 2', 'Diablo IV'], assignedClient: '', guardStatus: 'محافظت‌شده', allowedGameIds: [] },
  { id: 'a3', title: 'Riot-03', platform: 'Riot', status: 'locked', owner: 'مجموعه', expiresAt: 'قفل شده', allowedGames: ['Valorant'], assignedClient: '', guardStatus: 'نیازمند بررسی', allowedGameIds: ['g2'] },
  { id: 'a4', title: 'Epic-04', platform: 'Epic', status: 'free', owner: 'مجموعه', expiresAt: '۱ هفته دیگر', allowedGames: ['Fortnite', 'Rocket League'], assignedClient: '', guardStatus: '2FA', allowedGameIds: [] },
  { id: 'a5', title: 'Steam-05', platform: 'Steam', status: 'free', owner: 'مجموعه', expiresAt: '۲ هفته دیگر', allowedGames: ['EA SPORTS FC 25'], assignedClient: '', guardStatus: 'محافظت‌شده', allowedGameIds: [] },
];

const accountLogs: string[] = ['Steam-01 به PC ۱۲ تخصیص یافت', 'Riot-03 پس از خطای ورود قفل شد', 'Epic-04 آزاد شد'];

const clientSystems: ClientRecord[] = Array.from({ length: 40 }, (_, index) => {
  const number = index + 1;
  const online = number % 9 !== 0;
  return {
    id: `cl${number}`,
    name: `PC ${String(number).padStart(2, '۰')}`,
    type: 'PC',
    version: number % 7 === 0 ? '1.8.3' : '1.8.4',
    online,
    lastSync: online ? `${number % 5 + 1} دقیقه پیش` : '۲ ساعت پیش',
    ip: `192.168.1.${number + 20}`,
    dns1: '178.22.122.100',
    dns2: '185.51.200.2',
    systemNumber: number,
    serverAddress: '192.168.1.10:8080',
    shell: true,
    network: number % 2 === 0 ? 'internet2' : 'internet1',
    bootMode: number % 8 === 0 ? 'pxe' : 'ccboot',
    user: number % 4 === 0 ? 'رضا محمدی' : '',
    game: number % 4 === 0 ? 'Counter-Strike 2' : '',
    updatePending: number % 10 === 0,
    internetEnabled: true,
    locked: false,
  };
});

const settings: SettingGroup[] = [
  { id: 's1', title: 'نمایش لحظه‌ای هزینه', description: 'نمایش نرخ لحظه‌ای روی ایستگاه‌ها', enabled: true },
  { id: 's2', title: 'هشدار موجودی کم', description: 'هشدار برای کالاهای نزدیک به اتمام', enabled: true },
  { id: 's3', title: 'چاپ فاکتور خودکار', description: 'چاپ پس از تسویه به‌صورت خودکار', enabled: false },
  { id: 's4', title: 'پشتیبان‌گیری خودکار', description: 'ذخیرهٔ خودکار هر ۶ ساعت', enabled: true },
  { id: 's5', title: 'حالت شب', description: 'تم تاریک و کاهش نور', enabled: true },
  { id: 's6', title: 'قفل شبکه', description: 'دسترسی کاربران محدود و امن', enabled: false },
];

const sessions = new Map<string, { startedAt: number; input: StartSessionInput; buffetAmount: number }>();
const invoices: SessionInvoice[] = [];


type ExpenseRecord = TypedExpenseRecord;
type ReportRow = { id: string; station: string; timeAmount: number; buffet: number; packageAmount: number; amount: number; method: 'cash' | 'card' | 'wallet' | 'gift'; operator: string; type: 'time' | 'buffet' | 'package'; closedAt: string };
type ShiftRecord = { id: string; operator: string; openedAt: string; closedAt?: string; expectedCash?: number; countedCash?: number; difference?: number; sales?: number };

const expenses: ExpenseRecord[] = [
  { id: 'e1', title: 'خرید نوشیدنی', category: 'خرید/تأمین', amount: 420000, createdAt: new Date(Date.now() - 86400000).toISOString(), operator: 'علی محمدی' },
  { id: 'e2', title: 'لوازم مصرفی', category: 'خرید/تأمین', amount: 180000, createdAt: new Date(Date.now() - 2 * 86400000).toISOString(), operator: 'سارا احمدی' },
];
const reportRows: ReportRow[] = [
  { id: 'r1', station: 'PC 04', timeAmount: 180000, buffet: 90000, packageAmount: 0, amount: 270000, method: 'cash', operator: 'علی محمدی', type: 'time', closedAt: new Date(Date.now() - 2 * 3600000).toISOString() },
  { id: 'r2', station: 'PS5 02', timeAmount: 260000, buffet: 35000, packageAmount: 0, amount: 295000, method: 'card', operator: 'سارا احمدی', type: 'time', closedAt: new Date(Date.now() - 4 * 3600000).toISOString() },
  { id: 'r3', station: 'PC 12', timeAmount: 320000, buffet: 0, packageAmount: 1800000, amount: 2120000, method: 'wallet', operator: 'علی محمدی', type: 'package', closedAt: new Date(Date.now() - 86400000).toISOString() },
  { id: 'r4', station: 'میز 03', timeAmount: 90000, buffet: 70000, packageAmount: 0, amount: 160000, method: 'cash', operator: 'رضا کاظمی', type: 'buffet', closedAt: new Date(Date.now() - 2 * 86400000).toISOString() },
];
let currentShift: ShiftRecord | null = { id: 'shift-demo', operator: 'علی محمدی', openedAt: new Date(Date.now() - 3 * 3600000).toISOString() };
const shifts: ShiftRecord[] = [
  { id: 'shift-1', operator: 'علی محمدی', openedAt: new Date(Date.now() - 86400000 - 7 * 3600000).toISOString(), closedAt: new Date(Date.now() - 86400000).toISOString(), expectedCash: 2100000, countedCash: 2095000, difference: -5000, sales: 3100000 },
  { id: 'shift-2', operator: 'سارا احمدی', openedAt: new Date(Date.now() - 2 * 86400000 - 8 * 3600000).toISOString(), closedAt: new Date(Date.now() - 2 * 86400000).toISOString(), expectedCash: 1850000, countedCash: 1850000, difference: 0, sales: 2850000 },
];
const permissionStore: Record<string, boolean> = {};
const managedStations: StationManagementRecord[] = Array.from({ length: 61 }, (_, index) => {
  const number = index + 1;
  const zone: 'pc' | 'console' | 'table' = number <= 40 ? 'pc' : number <= 56 ? 'console' : 'table';
  const type = zone === 'pc' ? 'PC' : number <= 50 ? 'PS5' : number <= 56 ? 'PS4' : 'فوتبال‌دستی';
  return { id: `station-${number}`, name: zone === 'pc' ? `PC ${String(number).padStart(2, '۰')}` : `${type} ${String(number).padStart(2, '۰')}`, zone, type, ratePerHour: type === 'PC' ? 95000 : type === 'PS5' ? 150000 : type === 'PS4' ? 110000 : 60000, status: number % 17 === 0 ? 'off' : 'active', ip: zone === 'pc' ? `192.168.1.${number + 20}` : '', note: '' };
});
const reservations: ReservationRecord[] = [
  { id: 'res-1', stationId: 'station-4', stationName: 'PC ۰۴', customerCode: '1050', customerName: 'رضا محمدی', reservedAt: new Date(Date.now() + 45 * 60000).toISOString(), durationMinutes: 120, status: 'confirmed', note: 'مسابقه دوستانه' },
  { id: 'res-2', stationId: 'station-43', stationName: 'PS5 ۰۳', customerCode: '2020', customerName: 'سروش نیک‌پور', reservedAt: new Date(Date.now() + 90 * 60000).toISOString(), durationMinutes: 90, status: 'pending' },
];
const vipPackages: VipPackageRecord[] = [
  { id: 'vip-1', name: 'Bronze روزانه', tier: 'bronze', price: 450000, dailyMinutes: 120, totalMinutes: 3000, discount: 5, active: true },
  { id: 'vip-2', name: 'Silver روزانه', tier: 'silver', price: 850000, dailyMinutes: 180, totalMinutes: 6000, discount: 10, active: true },
  { id: 'vip-3', name: 'Gold ۲۴ ساعته', tier: 'gold', price: 1600000, dailyMinutes: 1440, totalMinutes: 14400, discount: 15, active: true },
];
const waitlist: Array<{ id: string; customerCode: string; customerName: string; stationType: string; createdAt: string; status: 'waiting' | 'assigned' }> = [
  { id: 'wait-1', customerCode: '2021', customerName: 'پارسا رضایی', stationType: 'PC', createdAt: new Date(Date.now() - 18 * 60000).toISOString(), status: 'waiting' },
];
const auditLogs: AuditLogRecord[] = [
  { id: 'audit-1', createdAt: new Date(Date.now() - 12 * 60000).toISOString(), operator: 'علی محمدی', action: 'تسویه جلسه', target: 'PC ۰۴', details: '۲۷۰٬۰۰۰ تومان · نقدی' },
  { id: 'audit-2', createdAt: new Date(Date.now() - 35 * 60000).toISOString(), operator: 'سارا احمدی', action: 'تغییر تعرفه', target: 'PS5 عادی', details: '۱۵۰٬۰۰۰ تومان / ساعت' },
];

export const mockService = {
  getCustomerLoginState: async (customerId: string, limit = 1) => ({ active: activeCustomerLogins.get(customerId)?.size ?? 0, limit }),
  acquireCustomerLogin: async (customerId: string, deviceId: string, limit = 1) => {
    const active = activeCustomerLogins.get(customerId) ?? new Set<string>();
    if (active.size >= limit && !active.has(deviceId)) return false;
    active.add(deviceId); activeCustomerLogins.set(customerId, active); return true;
  },
  releaseCustomerLogin: async (customerId: string, deviceId: string) => {
    const active = activeCustomerLogins.get(customerId);
    if (!active) return;
    active.delete(deviceId);
    if (!active.size) activeCustomerLogins.delete(customerId);
  },
  getStations: async (stations: import('../types').StationDto[]) => stations,
  getManagedStations: async () => [...managedStations],
  saveManagedStation: async (record: StationManagementRecord) => {
    const index = managedStations.findIndex(item => item.id === record.id);
    if (index < 0) managedStations.push(record); else managedStations[index] = record;
    auditLogs.unshift({ id: crypto.randomUUID(), createdAt: new Date().toISOString(), operator: 'علی محمدی', action: 'ویرایش ایستگاه', target: record.name, details: record.status });
    return record;
  },
  deleteManagedStation: async (id: string) => { const index = managedStations.findIndex(item => item.id === id); if (index >= 0) managedStations.splice(index, 1); },
  toggleStationOutOfService: async (id: string, reason = 'خارج از سرویس') => {
    const station = managedStations.find(item => item.id === id);
    if (!station) return null;
    station.status = station.status === 'off' ? 'active' : 'off';
    station.note = station.status === 'off' ? reason : '';
    auditLogs.unshift({ id: crypto.randomUUID(), createdAt: new Date().toISOString(), operator: 'علی محمدی', action: station.status === 'off' ? 'خارج از سرویس' : 'فعال‌سازی ایستگاه', target: station.name, details: station.note || 'فعال شد' });
    return station;
  },
  getReservations: async () => [...reservations],
  saveReservation: async (record: ReservationRecord) => {
    const index = reservations.findIndex(item => item.id === record.id);
    if (index < 0) reservations.push(record); else reservations[index] = record;
    auditLogs.unshift({ id: crypto.randomUUID(), createdAt: new Date().toISOString(), operator: 'علی محمدی', action: 'رزرو ایستگاه', target: record.stationName, details: record.customerCode });
    return record;
  },
  cancelReservation: async (id: string) => { const item = reservations.find(x => x.id === id); if (item) item.status = 'cancelled'; },
  getVipPackages: async () => [...vipPackages],
  deleteVipPackage: async (id: string) => { const index = vipPackages.findIndex(item => item.id === id); if (index >= 0) vipPackages.splice(index, 1); },
  saveVipPackage: async (record: VipPackageRecord) => { const index = vipPackages.findIndex(item => item.id === record.id); if (index < 0) vipPackages.push(record); else vipPackages[index] = record; return record; },
  getAuditLogs: async () => [...auditLogs],
  addAuditLog: async (entry: Omit<AuditLogRecord, 'id' | 'createdAt'>) => { const row = { ...entry, id: crypto.randomUUID(), createdAt: new Date().toISOString() }; auditLogs.unshift(row); return row; },
  getManagementInvoices: async () => invoices.map(item => ({ ...item, status: 'paid', operator: 'علی محمدی' })) as ManagementInvoiceRecord[],
  getManagementExpenses: async () => [...expenses],
  startSession: async (input: StartSessionInput) => {
    sessions.set(input.stationId, { startedAt: Date.now(), input, buffetAmount: 0 });
    return { ...input, startedAt: new Date().toISOString() };
  },
  settleSession: async (stationId: string, paymentMethod: SessionInvoice['paymentMethod']) => {
    const session = sessions.get(stationId);
    if (!session) throw new Error('جلسه فعالی برای این ایستگاه پیدا نشد');
    const durationMinutes = Math.max(1, Math.ceil((Date.now() - session.startedAt) / 60000));
    const timeAmount = Math.ceil((session.input.hourlyRate * durationMinutes) / 60);
    const invoice: SessionInvoice = {
      id: crypto.randomUUID(),
      stationId,
      stationName: session.input.stationName,
      customerCode: session.input.customerCode,
      durationMinutes,
      timeAmount,
      buffetAmount: session.buffetAmount,
      totalAmount: timeAmount + session.buffetAmount,
      paymentMethod,
      closedAt: new Date().toISOString(),
    };
    invoices.push(invoice);
    sessions.delete(stationId);
    return invoice;
  },
  extendSession: async (stationId: string, minutes: number) => {
    const session = sessions.get(stationId);
    if (!session) throw new Error('جلسه فعالی برای این ایستگاه پیدا نشد');
    return { stationId, minutes, extendedAt: new Date().toISOString() };
  },
  getInvoices: async () => [...invoices],
  getWalletLedger: async (customerId: string) => [...(walletLedger[customerId] ?? [])].sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
  recordWalletTransaction: async (
    customerId: string,
    input: { amount: number; type: 'credit' | 'debit'; description: string }
  ) => {
    const customer = customers.find(item => item.id === customerId);
    if (!customer || input.amount <= 0) throw new Error('مشتری یا مبلغ تراکنش معتبر نیست');
    if (input.type === 'debit' && customer.wallet < input.amount) throw new Error('موجودی کیف پول کافی نیست');
    customer.wallet = input.type === 'credit' ? customer.wallet + input.amount : customer.wallet - input.amount;
    const current = walletLedger[customerId] ?? [];
    const entry: WalletLedgerEntry = {
      id: crypto.randomUUID(),
      customerId,
      amount: input.amount,
      direction: input.type === 'credit' ? 'credit' : 'debit',
      type: input.type === 'credit' ? 'charge' : 'debit',
      description: input.description,
      createdAt: new Date().toISOString(),
      balanceAfter: customer.wallet,
    };
    walletLedger[customerId] = [entry, ...current];
    return entry;
  },
  getCustomers: async () => customers,
  getProducts: async () => products,
  saveProduct: async (product: ProductRecord) => { const index = products.findIndex(item => item.id === product.id); if (index < 0) products.push(product); else products[index] = product; return product; },
  addStock: async (productId: string, quantity: number) => { const product = products.find(item => item.id === productId); if (!product) throw new Error('کالا پیدا نشد'); product.stock += Math.max(0, quantity); return product; },
  getWaitlist: async () => [...waitlist],
  addWaitlist: async (entry: Omit<(typeof waitlist)[number], 'id' | 'createdAt' | 'status'>) => { const row = { ...entry, id: crypto.randomUUID(), createdAt: new Date().toISOString(), status: 'waiting' as const }; waitlist.push(row); return row; },
  assignWaitlist: async (id: string, stationName: string) => { const row = waitlist.find(item => item.id === id); if (!row) return null; row.status = 'assigned'; auditLogs.unshift({ id: crypto.randomUUID(), createdAt: new Date().toISOString(), operator: 'علی محمدی', action: 'تخصیص صف انتظار', target: stationName, details: row.customerCode }); return row; },
  transferSession: async (fromStation: string, toStation: string) => { auditLogs.unshift({ id: crypto.randomUUID(), createdAt: new Date().toISOString(), operator: 'علی محمدی', action: 'انتقال جلسه', target: toStation, details: `${fromStation} → ${toStation}` }); return { fromStation, toStation, transferredAt: new Date().toISOString() }; },
  getUsers: async () => users,
  getTariffs: async () => tariffs,
  saveTariff: async (tariff: TariffRecord) => {
    const index = tariffs.findIndex(item => item.id === tariff.id);
    if (index < 0) tariffs.push(tariff);
    else tariffs[index] = tariff;
    return tariff;
  },
  deleteTariff: async (id: string) => {
    const index = tariffs.findIndex(item => item.id === id);
    if (index >= 0) tariffs.splice(index, 1);
  },
  getGames: async () => games,
  saveGame: async (game: GameRecord) => {
    const index = games.findIndex(item => item.id === game.id);
    if (index < 0) games.push(game);
    else games[index] = game;
    return game;
  },
  deleteGame: async (id: string) => {
    const index = games.findIndex(item => item.id === id);
    if (index >= 0) games.splice(index, 1);
  },
  applyGamesToClients: async (gameIds: string[]) => ({ gameIds, queuedAt: new Date().toISOString() }),
  getAccounts: async () => accounts,
  saveAccount: async (account: AccountRecord) => {
    const index = accounts.findIndex(item => item.id === account.id);
    if (index < 0) accounts.push(account);
    else accounts[index] = account;
    accountLogs.unshift(`${account.title} ذخیره شد`);
    return account;
  },
  unlockAccount: async (id: string) => {
    const account = accounts.find(item => item.id === id);
    if (account) { account.status = 'free'; account.assignedClient = ''; accountLogs.unshift(`${account.title} رفع قفل شد`); }
  },
  getAccountLogs: async () => [...accountLogs],
  allocatePoolAccount: async (gameName: string, clientName: string) => {
    const account = accounts.find(item => item.status === 'free' && item.allowedGames.includes(gameName));
    if (!account) return null;
    account.status = 'in-use'; account.assignedClient = clientName;
    accountLogs.unshift(`${account.title} برای ${clientName} در بازی ${gameName} تخصیص یافت`);
    return account;
  },
  releasePoolAccount: async (clientName: string) => {
    for (const account of accounts) {
      if (account.status === 'in-use' && account.assignedClient === clientName) {
        account.status = 'free'; account.assignedClient = '';
        accountLogs.unshift(`${account.title} از ${clientName} آزاد شد`);
      }
    }
  },
  getClients: async () => clientSystems,
  getClientSystems: async () => clientSystems,
  getSettings: async () => settings,
  getReportRows: async () => [...reportRows],
  addReportRow: async (row: ReportRow) => { reportRows.unshift(row); return row; },
  getExpenses: async () => [...expenses],
  addExpense: async (expense: Omit<ExpenseRecord, 'id' | 'createdAt'>) => {
    const row = { ...expense, id: crypto.randomUUID(), createdAt: new Date().toISOString() };
    expenses.unshift(row);
    return row;
  },
  getCurrentShift: async () => currentShift ? {
    ...currentShift,
    expectedCash: reportRows.filter(row => row.operator === currentShift!.operator && row.method === 'cash').reduce((sum, row) => sum + row.amount, 0),
    sales: reportRows.filter(row => row.operator === currentShift!.operator).reduce((sum, row) => sum + row.amount, 0),
  } : null,
  startShift: async (operator = 'علی محمدی') => {
    if (currentShift) throw new Error('شیفت فعلی هنوز باز است');
    currentShift = { id: crypto.randomUUID(), operator, openedAt: new Date().toISOString(), expectedCash: 0, countedCash: 0, difference: 0, sales: 0 };
    return { ...currentShift };
  },
  closeShift: async (countedCash: number, manualCashAdjustment = 0, note = '') => {
    if (!currentShift) throw new Error('شیفت بازی برای بستن وجود ندارد');
    const expectedCash = reportRows.filter(row => row.operator === currentShift!.operator && row.method === 'cash').reduce((sum, row) => sum + row.amount, 0);
    const adjustedExpectedCash = expectedCash + manualCashAdjustment;
    const closed = { ...currentShift, closedAt: new Date().toISOString(), expectedCash: adjustedExpectedCash, rawExpectedCash: expectedCash, countedCash, manualCashAdjustment, note, difference: countedCash - adjustedExpectedCash, sales: reportRows.filter(row => row.operator === currentShift!.operator).reduce((sum, row) => sum + row.amount, 0) };
    shifts.unshift(closed);
    currentShift = null;
    return closed;
  },
  getShifts: async () => [...shifts],
  saveUser: async (user: UserRecord) => {
    const index = users.findIndex(item => item.id === user.id);
    if (index < 0) users.push(user); else users[index] = user;
    return user;
  },
  getPermissions: async () => ({ ...permissionStore }),
  savePermissions: async (values: Record<string, boolean>) => {
    Object.keys(permissionStore).forEach(key => delete permissionStore[key]);
    Object.assign(permissionStore, values);
    return { ...permissionStore };
  },
  createBackup: async () => ({
    customers, products, users, tariffs, games, accounts, clients: clientSystems, settings,
    expenses, reportRows, shifts, currentShift, permissions: permissionStore, createdAt: new Date().toISOString()
  }),
  restoreBackup: async (payload: any) => {
    const replace = (target: any[], source: any[]) => { if (Array.isArray(source)) target.splice(0, target.length, ...source); };
    replace(customers, payload.customers); replace(products, payload.products); replace(users, payload.users); replace(tariffs, payload.tariffs);
    replace(games, payload.games); replace(accounts, payload.accounts); replace(clientSystems, payload.clients); replace(settings, payload.settings);
    replace(expenses, payload.expenses); replace(reportRows, payload.reportRows); replace(shifts, payload.shifts);
    Object.keys(permissionStore).forEach(key => delete permissionStore[key]);
    Object.assign(permissionStore, payload.permissions || {});
  }

};
