import { useEffect, useMemo, useRef, useState } from 'react';
import { getServerGames } from '../../services/gameService';
import { getServerAccountPool } from '../../services/accountPoolService';
import { getServerCustomers } from '../../services/customerService';
import { getManagementInvoices, getServerProducts } from '../../services/operationsService';
import { getUsers } from '../../services/authService';
import { getTariffs } from '../../services/tariffService';
import { getAgentStatuses } from '../../services/agentService';
import type {
  AccountRecord,
  AgentStatusDto,
  AppUserRecord,
  CustomerRecord,
  GameRecord,
  ManagementInvoiceRecord,
  PageKey,
  ProductRecord,
  StationDto,
  TariffRecord,
  UserRecord,
} from '../../types';

type CommandItem = {
  id: string;
  title: string;
  description: string;
  key: string;
  page: PageKey;
  keywords: string[];
};

type SearchResult = {
  id: string;
  title: string;
  meta: string;
  kind: string;
  page: PageKey;
  keywords: string;
};

type Props = {
  open: boolean;
  stations: StationDto[];
  onNavigate: (page: PageKey) => void;
  onClose: () => void;
};

const commands: CommandItem[] = [
  { id: 'start-session', title: 'شروع جلسه جدید', description: 'اولین ایستگاه آزاد را برای شروع جلسه انتخاب می‌کند.', key: 'start-session', page: 'dashboard', keywords: ['جلسه', 'شروع', 'ایستگاه', 'session'] },
  { id: 'quick-charge', title: 'شارژ مستقیم جلسه', description: 'پنجره شارژ سریع برای یک جلسه فعال را باز می‌کند.', key: 'quick-charge', page: 'dashboard', keywords: ['شارژ', 'جلسه', 'اعتبار', 'charge'] },
  { id: 'charge-debt', title: 'شارژ + بدهی', description: 'جریان مالی سریع شارژ و ثبت بدهی را باز می‌کند.', key: 'flow', page: 'dashboard', keywords: ['شارژ', 'بدهی', 'مالی'] },
  { id: 'deduct-wallet', title: 'کسر اعتبار', description: 'کسر مبلغ از کیف پول مشتری را اجرا می‌کند.', key: 'flow', page: 'dashboard', keywords: ['کسر', 'کیف پول', 'اعتبار'] },
  { id: 'buffet-sale', title: 'فروش سریع بوفه', description: 'به فروش سریع بوفه می‌رود.', key: 'buffet', page: 'buffet', keywords: ['بوفه', 'فروش', 'کالا'] },
  { id: 'customer-search', title: 'جست‌وجوی مشتری', description: 'صفحه مشتریان را باز می‌کند.', key: 'customers', page: 'customers', keywords: ['مشتری', 'customer', 'شناسه', 'کد'] },
  { id: 'extend-session', title: 'تمدید وقت', description: 'تمدید جلسه فعال را از داشبورد باز می‌کند.', key: 'extend-session', page: 'dashboard', keywords: ['تمدید', 'زمان', 'وقت'] },
  { id: 'close-shift', title: 'بستن صندوق / پایان شیفت', description: 'به بخش بستن شیفت می‌رود.', key: 'close-shift', page: 'users', keywords: ['صندوق', 'شیفت', 'بستن', 'handover'] },
];

function normalize(value: unknown): string {
  return String(value ?? '')
    .toLocaleLowerCase('fa-IR')
    .replace(/[يى]/g, 'ی')
    .replace(/[ك]/g, 'ک')
    .replace(/\u200c/g, ' ')
    .replace(/[۰-۹]/g, digit => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(digit)));
}

function createResults(
  customers: CustomerRecord[],
  products: ProductRecord[],
  tariffs: TariffRecord[],
  games: GameRecord[],
  accounts: AccountRecord[],
  agents: AgentStatusDto[],
  users: UserRecord[],
  invoices: ManagementInvoiceRecord[],
  stations: StationDto[],
): SearchResult[] {
  return [
    ...stations.map(item => ({
      id: item.id,
      title: item.name,
      meta: `${item.type} · ${item.customerCode ? `مشتری ${item.customerCode}` : 'بدون مشتری'} · ${item.state === 'busy' ? 'در حال بازی' : item.state === 'free' ? 'آزاد' : item.state === 'paused' ? 'متوقف' : item.state === 'reserved' ? 'رزرو' : 'خارج از سرویس'}`,
      kind: 'ایستگاه',
      page: 'dashboard' as PageKey,
      keywords: [item.name, item.type, item.zone, item.customerCode, item.state, item.outOfServiceReason].filter(Boolean).join(' '),
    })),
    ...customers.map(item => ({
      id: item.id,
      title: item.name,
      meta: `مشتری · ${item.code ?? item.username} · کیف پول ${new Intl.NumberFormat('fa-IR').format(item.wallet)} تومان`,
      kind: 'مشتری',
      page: 'customers' as PageKey,
      keywords: [item.code, item.nationalId, item.name, item.alias, item.mobile, item.username, item.packageName, item.status].filter(Boolean).join(' '),
    })),
    ...products.map(item => ({
      id: item.id,
      title: item.name,
      meta: `کالا · ${item.category} · موجودی ${item.stock}`,
      kind: 'بوفه',
      page: 'buffet' as PageKey,
      keywords: [item.name, item.category, item.price, item.stock].join(' '),
    })),
    ...tariffs.map(item => ({
      id: item.id,
      title: item.title,
      meta: `تعرفه · ${item.stationType} · ${item.tier === 'vip' ? 'VIP' : 'عادی'} · ${new Intl.NumberFormat('fa-IR').format(item.pricePerHour)} تومان/ساعت`,
      kind: 'تعرفه',
      page: 'tariffs' as PageKey,
      keywords: [item.title, item.stationType, item.tier, item.pricePerHour, item.nightHours].join(' '),
    })),
    ...games.map(item => ({
      id: item.id,
      title: item.name,
      meta: `بازی · ${item.category} · ${item.version}`,
      kind: 'بازی',
      page: 'games' as PageKey,
      keywords: [item.name, item.version, item.category, item.path, item.executable, item.targetSystem].join(' '),
    })),
    ...accounts.map(item => ({
      id: item.id,
      title: item.title,
      meta: `اکانت · ${item.platform} · ${item.status === 'free' ? 'آزاد' : item.status === 'in-use' ? 'در استفاده' : 'قفل'}`,
      kind: 'اکانت',
      page: 'accounts' as PageKey,
      keywords: [item.title, item.platform, item.status, item.owner, item.assignedClient, ...item.allowedGames].join(' '),
    })),
    ...agents.map(item => ({
      id: item.agentId,
      title: item.name,
      meta: `کلاینت · ${item.isOnline ? 'آنلاین' : 'آفلاین'} · ${item.deviceId} · ${item.lifecycleState}`,
      kind: 'کلاینت',
      page: 'client-shell' as PageKey,
      keywords: [item.name, item.deviceId, item.stationName, item.agentVersion, item.lifecycleState, item.isLocked ? 'قفل' : 'باز'].filter(Boolean).join(' '),
    })),
    ...users.map(item => ({
      id: item.id,
      title: item.name,
      meta: `کاربر · ${item.role === 'owner' ? 'صاحب' : item.role === 'admin' ? 'مدیر' : 'اپراتور'} · شیفت ${item.shift}`,
      kind: 'کاربر',
      page: 'users' as PageKey,
      keywords: [item.name, item.role, item.shift, ...item.permissions].join(' '),
    })),
    ...invoices.map(item => ({
      id: item.id,
      title: `فاکتور ${item.stationName}`,
      meta: `فاکتور · مشتری ${item.customerCode || 'مهمان'} · ${new Intl.NumberFormat('fa-IR').format(item.totalAmount)} تومان · ${item.status === 'paid' ? 'پرداخت‌شده' : item.status === 'pending' ? 'در انتظار' : 'باطل'}`,
      kind: 'فاکتور',
      page: 'reports' as PageKey,
      keywords: [item.id, item.stationId, item.stationName, item.customerCode, item.paymentMethod, item.operator, item.status, item.totalAmount].join(' '),
    })),
  ];
}

export function GlobalCommandCenter({ open, stations, onNavigate, onClose }: Props) {
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState('');
  const [data, setData] = useState<SearchResult[]>([]);
  const [activeIndex, setActiveIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!open) {
      setQuery('');
      setActiveIndex(0);
      return;
    }
    inputRef.current?.focus();
  }, [open]);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    setLoadError('');
    void Promise.all([
      getServerCustomers(),
      getServerProducts(),
      getTariffs(),
      getServerGames(),
      getServerAccountPool(),
      getAgentStatuses(),
      getUsers(),
      getManagementInvoices(),
    ])
      .then(([customers, products, tariffs, games, accounts, agents, users, invoices]) => {
        if (cancelled) return;
        const mappedTariffs: TariffRecord[] = tariffs.map(item => ({
          id: item.id,
          title: item.name,
          stationType: 'PC',
          tier: 'normal',
          pricePerHour: item.hourlyRate,
          daily: item.dailyRate,
          vipDiscount: 0,
          nightRate: 0,
          nightHours: '',
          active: item.isActive,
        }));
        const mappedUsers: UserRecord[] = (users as AppUserRecord[]).map(item => ({
          id: item.id,
          name: item.fullName,
          role: item.role.toLowerCase() === 'owner' ? 'owner' : item.role.toLowerCase() === 'admin' || item.role.toLowerCase() === 'manager' ? 'admin' : 'operator',
          shift: 'سرور',
          sales: 0,
          permissions: item.permissions,
        }));
        setData(createResults(customers, products, mappedTariffs, games, accounts, agents, mappedUsers, invoices, stations));
      })
      .catch(() => {
        if (!cancelled) setLoadError('اطلاعات جست‌وجو بارگذاری نشد. دوباره تلاش کنید.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [open, stations]);

  const filtered = useMemo(() => {
    const needle = normalize(query.trim());
    const commandRows = commands.filter(command => !needle || normalize([command.title, command.description, ...command.keywords].join(' ')).includes(needle));
    const resultRows = data.filter(item => !needle || normalize([item.title, item.meta, item.keywords].join(' ')).includes(needle));
    return { commandRows, resultRows };
  }, [data, query]);

  const keyboardRows = useMemo(
    () => [
      ...filtered.commandRows.map(item => ({ kind: 'command' as const, id: item.id, item })),
      ...filtered.resultRows.map(item => ({ kind: 'result' as const, id: `${item.kind}-${item.id}`, item })),
    ],
    [filtered],
  );

  useEffect(() => {
    if (!keyboardRows.length) setActiveIndex(0);
    else setActiveIndex(index => Math.min(index, keyboardRows.length - 1));
  }, [keyboardRows.length]);

  function runCommand(command: CommandItem) {
    onNavigate(command.page);
    onClose();
    window.dispatchEvent(new CustomEvent('gamenet-command', { detail: command.key }));
  }

  function openResult(item: SearchResult) {
    onNavigate(item.page);
    onClose();
    window.dispatchEvent(new CustomEvent('gamenet-search-selection', { detail: { type: item.kind, id: item.id } }));
  }

  if (!open) return null;

  return (
    <div className="modal-backdrop command-backdrop" onMouseDown={event => event.target === event.currentTarget && onClose()}>
      <section className="global-search-center" role="dialog" aria-modal="true" aria-label="مرکز جست‌وجو و فرمان">
        <div className="global-search-head">
          <div>
            <strong>مرکز جست‌وجو و فرمان</strong>
            <span>مشتری، ایستگاه، جلسه، فاکتور، بازی، اکانت، کلاینت یا یک فرمان را پیدا کنید.</span>
          </div>
          <button type="button" className="modal-close" onClick={onClose} aria-label="بستن">×</button>
        </div>

        <div className="global-search-input-wrap">
          <input
            ref={inputRef}
            value={query}
            onChange={event => setQuery(event.target.value)}
            onKeyDown={event => {
              if (event.key === 'ArrowDown') { event.preventDefault(); setActiveIndex(index => Math.min(index + 1, Math.max(0, keyboardRows.length - 1))); }
              if (event.key === 'ArrowUp') { event.preventDefault(); setActiveIndex(index => Math.max(0, index - 1)); }
              if (event.key === 'Enter' && keyboardRows[activeIndex]) {
                event.preventDefault();
                const row = keyboardRows[activeIndex];
                if (row.kind === 'command') runCommand(row.item as CommandItem);
                else openResult(row.item as SearchResult);
              }
              if (event.key === 'Escape') { event.preventDefault(); onClose(); }
            }}
            placeholder="مثلاً: ۱۰۵۰، PC-07، سروش، FIFA، Steam یا «تمدید وقت»"
            aria-label="جست‌وجوی سراسری"
          />
          <kbd>↑ ↓ انتخاب</kbd>
          <kbd>Enter اجرا</kbd>
          <kbd>Esc بستن</kbd>
        </div>

        <div className="global-search-body">
          {loading && <div className="global-search-empty">در حال آماده‌سازی جست‌وجو…</div>}
          {!loading && loadError && <div className="global-search-empty error">{loadError}</div>}

          {!loading && !loadError && filtered.commandRows.length > 0 && (
            <section className="global-search-section">
              <div className="global-search-section-title">فرمان‌ها <span>{filtered.commandRows.length}</span></div>
              {filtered.commandRows.map(command => {
                const rowIndex = keyboardRows.findIndex(row => row.kind === 'command' && row.id === command.id);
                return (
                  <button
                    type="button"
                    key={command.id}
                    className={`global-search-row ${rowIndex === activeIndex ? 'active' : ''}`}
                    onMouseEnter={() => setActiveIndex(rowIndex)}
                    onClick={() => runCommand(command)}
                  >
                    <span className="global-search-row-icon">⌘</span>
                    <span className="global-search-row-main"><strong>{command.title}</strong><small>{command.description}</small></span>
                    <span className="global-search-row-kind">فرمان</span>
                  </button>
                );
              })}
            </section>
          )}

          {!loading && !loadError && filtered.resultRows.length > 0 && (
            <section className="global-search-section">
              <div className="global-search-section-title">اطلاعات <span>{filtered.resultRows.length}</span></div>
              {filtered.resultRows.slice(0, 40).map(item => {
                const rowIndex = keyboardRows.findIndex(row => row.kind === 'result' && row.id === `${item.kind}-${item.id}`);
                return (
                  <button
                    type="button"
                    key={`${item.kind}-${item.id}`}
                    className={`global-search-row ${rowIndex === activeIndex ? 'active' : ''}`}
                    onMouseEnter={() => setActiveIndex(rowIndex)}
                    onClick={() => openResult(item)}
                  >
                    <span className="global-search-row-icon">⌕</span>
                    <span className="global-search-row-main"><strong>{item.title}</strong><small>{item.meta}</small></span>
                    <span className="global-search-row-kind">{item.kind}</span>
                  </button>
                );
              })}
            </section>
          )}

          {!loading && !loadError && !filtered.commandRows.length && !filtered.resultRows.length && (
            <div className="global-search-empty">
              نتیجه‌ای برای «{query}» پیدا نشد.
              <small>کد مشتری، نام، موبایل، نام ایستگاه، بازی، اکانت یا بخشی از اطلاعات را امتحان کنید.</small>
            </div>
          )}
        </div>

        <footer className="global-search-footer">
          <span>{filtered.resultRows.length.toLocaleString('fa-IR')} رکورد · {filtered.commandRows.length.toLocaleString('fa-IR')} فرمان</span>
          <span>نتیجه‌ها از داده‌های فعلی برنامه ساخته می‌شوند؛ دسترسی نهایی هر فرمان در سرور کنترل خواهد شد.</span>
        </footer>
      </section>
    </div>
  );
}
