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
} from '../types';

const customers: CustomerRecord[] = [
  { id: 'c1', code: '1050', nationalId: '0012345678', name: 'رضا محمدی', alias: 'Reza-Headshot', mobile: '09123456789', vip: 'gold', wallet: 450000, debt: 0, giftCredit: 120000, discountLevel: 18, packageName: 'Gold VIP', username: 'reza_hs', lastSeen: '۵ دقیقه پیش', status: 'active', hoursUsedToday: 2.5, dailyHourCap: 4, transactionHistory: ['PS5-03 · ۲.۵ ساعت · ۲۵۰٬۰۰۰ تومان', 'پکیج گلد · ۱٬۸۰۰٬۰۰۰ تومان', 'بوفه · نوشابه ×۳ · ۹۰٬۰۰۰ تومان'] },
  { id: 'c2', code: '2020', nationalId: '0087654321', name: 'سروش نیک‌پور', alias: 'Soroush', mobile: '09120000002', vip: 'silver', wallet: 120000, debt: 35000, giftCredit: 5000, discountLevel: 10, packageName: 'Silver VIP', username: 'soroush.n', lastSeen: '۲۱ دقیقه پیش', status: 'warning', hoursUsedToday: 3.5, dailyHourCap: 5, transactionHistory: ['PC-09 · ۱ ساعت · ۹۵٬۰۰۰ تومان'] },
  { id: 'c3', code: '2021', nationalId: '0023456789', name: 'پارسا رضایی', alias: 'Parsa', mobile: '09120000003', vip: 'none', wallet: 0, debt: 20000, giftCredit: 0, discountLevel: 5, username: 'parsa.r', lastSeen: '۱ ساعت پیش', status: 'active', hoursUsedToday: 1, transactionHistory: ['بدهی ثبت‌شده · ۲۰٬۰۰۰ تومان'] },
  { id: 'c4', code: '1051', nationalId: '0076543210', name: 'مهدی جهان', alias: 'Mehdi', mobile: '09120000004', vip: 'gold', wallet: 470000, debt: 0, giftCredit: 25000, discountLevel: 22, packageName: 'Gold VIP', username: 'mehdi.j', lastSeen: 'حال حاضر', status: 'active', hoursUsedToday: 5, dailyHourCap: 4, transactionHistory: ['سقف روزانه تکمیل شد · مازاد نیم‌بها'] },
];

const products: ProductRecord[] = [
  { id: 'p1', name: 'انرژی درینک', category: 'نوشیدنی', price: 25000, buyPrice: 14000, stock: 42, maxStock: 60 },
  { id: 'p2', name: 'پیتزا کوچک', category: 'غذا', price: 42000, buyPrice: 22000, stock: 18, maxStock: 30 },
  { id: 'p3', name: 'چیپس', category: 'تنقلات', price: 18000, buyPrice: 9000, stock: 24, maxStock: 40 },
  { id: 'p4', name: 'کاپ کیک', category: 'دسر', price: 14000, buyPrice: 7000, stock: 9, maxStock: 20 },
];

const users: UserRecord[] = [
  { id: 'u1', name: 'رضا احمدی', role: 'owner', shift: 'صبح', sales: 18450000, permissions: ['مدیریت', 'گزارش', 'تنظیمات', 'کارمزد'] },
  { id: 'u2', name: 'نرگس علیزاده', role: 'admin', shift: 'عصر', sales: 12240000, permissions: ['ایستگاه‌ها', 'مشتریان', 'بوفه', 'گزارش'] },
  { id: 'u3', name: 'حسین گل‌زاده', role: 'operator', shift: 'شب', sales: 9800000, permissions: ['ایستگاه‌ها', 'بوفه'] },
];

const tariffs: TariffRecord[] = [
  { id: 't1', title: 'رایانه - عادی', stationType: 'PC', tier: 'normal', pricePerHour: 95000, daily: 550000, vipDiscount: 0, nightRate: 115000, nightHours: '۲۲:۰۰ تا ۰۶:۰۰', active: true },
  { id: 't2', title: 'رایانه - VIP', stationType: 'PC', tier: 'vip', pricePerHour: 80000, daily: 500000, vipDiscount: 15, nightRate: 100000, nightHours: '۲۲:۰۰ تا ۰۶:۰۰', active: true },
  { id: 't3', title: 'PS5 - عادی', stationType: 'PS5', tier: 'normal', pricePerHour: 150000, daily: 780000, vipDiscount: 0, nightRate: 180000, nightHours: '۲۲:۰۰ تا ۰۶:۰۰', active: true },
  { id: 't4', title: 'PS4 - عادی', stationType: 'PS4', tier: 'normal', pricePerHour: 110000, daily: 650000, vipDiscount: 0, nightRate: 130000, nightHours: '۲۲:۰۰ تا ۰۶:۰۰', active: true },
  { id: 't5', title: 'فوتبال‌دستی - عادی', stationType: 'فوتبال‌دستی', tier: 'normal', pricePerHour: 60000, daily: 0, vipDiscount: 0, nightRate: 75000, nightHours: '۲۲:۰۰ تا ۰۶:۰۰', active: true },
];

const games: GameRecord[] = [
  { id: 'g1', name: 'Counter-Strike 2', version: '2.0', category: 'FPS', status: 'online', activeUsers: 34, path: 'D:\\Games\\Steam\\steamapps\\common\\Counter-Strike Global Offensive', executable: 'cs2.exe', cover: '🎯', trailer: '', launchArgs: '-novid', connectionType: 'آنلاین', active: true, targetSystem: 'all', target: 'all', targetZone: 'pc', targetStations: '' },
  { id: 'g2', name: 'Valorant', version: '11.0', category: 'FPS', status: 'online', activeUsers: 21, path: 'D:\\Riot Games\\VALORANT', executable: 'VALORANT-Win64-Shipping.exe', cover: '⚡', trailer: '', launchArgs: '', connectionType: 'آنلاین', active: true, targetSystem: 'standard', target: 'all', targetZone: 'pc', targetStations: '' },
  { id: 'g3', name: 'FIFA 25', version: '2025', category: 'Sports', status: 'offline', activeUsers: 14, path: 'D:\\Games\\EA SPORTS FC 25', executable: 'FC25.exe', cover: '⚽', trailer: '', launchArgs: '', connectionType: 'آفلاین', active: true, targetSystem: 'all', target: 'zone', targetZone: 'console', targetStations: '' },
  { id: 'g4', name: 'Fortnite', version: '25.10', category: 'Battle Royale', status: 'online', activeUsers: 28, path: 'D:\\Epic Games\\Fortnite', executable: 'FortniteClient-Win64-Shipping.exe', cover: '🪂', trailer: '', launchArgs: '', connectionType: 'آنلاین', active: true, targetSystem: 'vip', target: 'all', targetZone: 'pc', targetStations: '' },
  { id: 'g5', name: 'Discord', version: '1.0', category: 'برنامه', status: 'program', activeUsers: 40, path: 'C:\\Users\\Public\\Desktop', executable: 'Discord.exe', cover: '💬', trailer: '', launchArgs: '', connectionType: 'آنلاین', active: true, targetSystem: 'all', target: 'all', targetZone: 'pc', targetStations: '' },
];

const accounts: AccountRecord[] = [
  { id: 'a1', title: 'Steam-01', platform: 'Steam', status: 'in-use', owner: 'مجموعه', expiresAt: '۳ روز دیگر', allowedGames: ['Counter-Strike 2', 'Dota 2'], assignedClient: 'PC ۱۲', guardStatus: '2FA' },
  { id: 'a2', title: 'Battle-02', platform: 'Battle.net', status: 'free', owner: 'مجموعه', expiresAt: '۱۲ ساعت دیگر', allowedGames: ['Overwatch 2', 'Diablo IV'], assignedClient: '', guardStatus: 'محافظت‌شده' },
  { id: 'a3', title: 'Riot-03', platform: 'Riot', status: 'locked', owner: 'مجموعه', expiresAt: 'قفل شده', allowedGames: ['Valorant'], assignedClient: '', guardStatus: 'نیازمند بررسی' },
  { id: 'a4', title: 'Epic-04', platform: 'Epic', status: 'free', owner: 'مجموعه', expiresAt: '۱ هفته دیگر', allowedGames: ['Fortnite', 'Rocket League'], assignedClient: '', guardStatus: '2FA' },
  { id: 'a5', title: 'Steam-05', platform: 'Steam', status: 'free', owner: 'مجموعه', expiresAt: '۲ هفته دیگر', allowedGames: ['EA SPORTS FC 25'], assignedClient: '', guardStatus: 'محافظت‌شده' },
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


type ExpenseRecord = { id: string; title: string; amount: number; createdAt: string; operator: string };
type ReportRow = { id: string; station: string; timeAmount: number; buffet: number; packageAmount: number; amount: number; method: 'cash' | 'card' | 'wallet'; operator: string; type: 'time' | 'buffet' | 'package'; closedAt: string };
type ShiftRecord = { id: string; operator: string; openedAt: string; closedAt?: string; expectedCash?: number; countedCash?: number; difference?: number; sales?: number };

const expenses: ExpenseRecord[] = [
  { id: 'e1', title: 'خرید نوشیدنی', amount: 420000, createdAt: new Date(Date.now() - 86400000).toISOString(), operator: 'علی محمدی' },
  { id: 'e2', title: 'لوازم مصرفی', amount: 180000, createdAt: new Date(Date.now() - 2 * 86400000).toISOString(), operator: 'سارا احمدی' },
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

export const mockService = {
  getStations: async (stations: import('../types').StationDto[]) => stations,
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
  getCustomers: async () => customers,
  getProducts: async () => products,
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
  getCurrentShift: async () => currentShift ? { ...currentShift } : null,
  startShift: async (operator = 'علی محمدی') => {
    if (currentShift) throw new Error('شیفت فعلی هنوز باز است');
    currentShift = { id: crypto.randomUUID(), operator, openedAt: new Date().toISOString() };
    return { ...currentShift };
  },
  closeShift: async (countedCash: number) => {
    if (!currentShift) throw new Error('شیفت بازی برای بستن وجود ندارد');
    const expectedCash = reportRows.filter(row => row.operator === currentShift!.operator && row.method === 'cash').reduce((sum, row) => sum + row.amount, 0);
    const closed = { ...currentShift, closedAt: new Date().toISOString(), expectedCash, countedCash, difference: countedCash - expectedCash, sales: reportRows.filter(row => row.operator === currentShift!.operator).reduce((sum, row) => sum + row.amount, 0) };
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
