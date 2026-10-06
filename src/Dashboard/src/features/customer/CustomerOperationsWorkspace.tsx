import { useEffect, useMemo, useState } from 'react';
import type { CustomerRecord, PendingSettlementAccount, ProductRecord, StationDto } from '../../types';
import { getServerProducts, recordServerBuffetSale } from '../../services/buffetService';

type FlowAction = 'walletAdd' | 'debtAdd' | 'walletDeduct' | 'walletDebt';
type ChargeMethod = 'cash' | 'card' | 'wallet';
type Tab = 'charge' | 'buffet';

type Props = {
  customers: CustomerRecord[];
  stations: StationDto[];
  pendingAccounts: PendingSettlementAccount[];
  hotkeys: Record<string, string>;
  customerId: string | null;
  search: string;
  amount: string;
  busy?: boolean;
  canSellBuffet: boolean;
  onSearchChange: (value: string) => void;
  onSearchSubmit: () => void;
  onSelectCustomer: (customer: CustomerRecord) => void;
  onAmountChange: (value: string) => void;
  onAction: (action: FlowAction) => void;
  onSessionCharge: (station: StationDto, amount: number, method: ChargeMethod) => Promise<boolean>;
  onDataChanged: (customerId: string) => Promise<void>;
  onBuffetAdded?: (stationId: string, amount: number) => void;
};

const actionDefinitions: Array<{
  key: FlowAction;
  title: string;
  detail: string;
  tone: string;
  hotkeyKey: string;
}> = [
  { key: 'walletAdd', title: 'شارژ کیف پول', detail: 'افزایش موجودی قابل‌مصرف مشتری', tone: 'primary', hotkeyKey: 'walletAdd' },
  { key: 'debtAdd', title: 'ثبت بدهی', detail: 'افزودن مستقیم مبلغ به بدهی مشتری', tone: 'warning', hotkeyKey: 'debtAdd' },
  { key: 'walletDeduct', title: 'کسر از کیف پول', detail: 'برداشت از موجودی بدون ایجاد بدهی', tone: 'danger', hotkeyKey: 'walletDeduct' },
  { key: 'walletDebt', title: 'کیف پول + بدهی', detail: 'ابتدا کیف پول، باقی‌مانده به بدهی', tone: 'neutral', hotkeyKey: 'walletDebt' },
];

function money(value: number) {
  return new Intl.NumberFormat('fa-IR').format(Math.round(value));
}
function decimal(value: number) {
  return new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 1 }).format(value);
}
function parseMoney(value: string) {
  return Number(
    value
      .replace(/[۰-۹]/g, d => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d)))
      .replace(/[٬,s]/g, ''),
  ) || 0;
}
function minutes(value: number | null | undefined) {
  if (value == null) return '—';
  const total = Math.max(0, Math.round(value));
  const hours = Math.floor(total / 60);
  const mins = total % 60;
  if (hours === 0) return mins + ' دقیقه';
  if (mins === 0) return hours + ' ساعت';
  return hours + ' ساعت و ' + mins + ' دقیقه';
}
function vipLabel(value: CustomerRecord['vip']) {
  if (value === 'gold') return 'طلایی';
  if (value === 'silver') return 'نقره‌ای';
  if (value === 'bronze') return 'برنزی';
  if (value === 'custom') return 'ویژه';
  return 'عادی';
}
function statusLabel(value: CustomerRecord['status']) {
  if (value === 'active') return 'فعال';
  if (value === 'warning') return 'نیازمند توجه';
  return 'مسدود';
}
function stationStateLabel(value: StationDto['state']) {
  if (value === 'busy') return 'در حال استفاده';
  if (value === 'paused') return 'متوقف';
  if (value === 'reserved') return 'رزرو';
  if (value === 'off') return 'خارج از سرویس';
  return 'آماده';
}
function findMatches(customers: CustomerRecord[], search: string) {
  const needle = search.trim().toLocaleLowerCase('fa-IR');
  if (!needle) return [];
  return customers
    .map(customer => {
      const fields = [customer.code, customer.username, customer.mobile, customer.id, customer.name, customer.alias, customer.nationalId]
        .filter(Boolean)
        .map(value => String(value).toLocaleLowerCase('fa-IR'));
      const exact = fields.some(value => value === needle);
      const partial = fields.some(value => value.includes(needle));
      return { customer, score: exact ? 3 : partial ? 1 : 0 };
    })
    .filter(item => item.score > 0)
    .sort((a, b) => b.score - a.score || a.customer.name.localeCompare(b.customer.name, 'fa'))
    .slice(0, 6)
    .map(item => item.customer);
}

export function CustomerOperationsWorkspace({
  customers,
  stations,
  pendingAccounts,
  hotkeys,
  customerId,
  search,
  amount,
  busy = false,
  canSellBuffet,
  onSearchChange,
  onSearchSubmit,
  onSelectCustomer,
  onAmountChange,
  onAction,
  onSessionCharge,
  onDataChanged,
  onBuffetAdded,
}: Props) {
  const customer = customers.find(item => item.id === customerId) ?? null;
  const matches = useMemo(() => findMatches(customers, search), [customers, search]);

  const activeStation = customer
    ? stations.find(station =>
        station.customerUsername === customer.username ||
        station.customerCode === customer.code ||
        station.customerCode === customer.username ||
        station.customerFullName === customer.name
      ) ?? null
    : null;

  const pendingAccount = customer
    ? pendingAccounts.find(item => item.customerId === customer.id) ?? null
    : null;

  const activeSession = activeStation
    && ['busy', 'paused'].includes(activeStation.state)
    && activeStation.serverSessionId
    ? activeStation
    : null;

  const [tab, setTab] = useState<Tab>('charge');
  const [chargeMethod, setChargeMethod] = useState<ChargeMethod>('cash');
  const [products, setProducts] = useState<ProductRecord[]>([]);
  const [buffetCart, setBuffetCart] = useState<Record<string, number>>({});
  const [buffetLoading, setBuffetLoading] = useState(false);
  const [buffetBusy, setBuffetBusy] = useState(false);
  const [buffetQuery, setBuffetQuery] = useState('');
  const [buffetCategory, setBuffetCategory] = useState('همه');
  const [buffetNotice, setBuffetNotice] = useState('');

  useEffect(() => {
    setTab('charge');
    setBuffetCart({});
    setBuffetQuery('');
    setBuffetCategory('همه');
  }, [customer?.id]);

  useEffect(() => {
    if (!customer || !canSellBuffet || tab !== 'buffet') return;
    setBuffetLoading(true);
    getServerProducts()
      .then(setProducts)
      .catch(() => setProducts([]))
      .finally(() => setBuffetLoading(false));
  }, [customer?.id, canSellBuffet, tab]);

  const actions = actionDefinitions.map(item => ({
    ...item,
    hotkey: hotkeys[item.hotkeyKey] ||
      (item.key === 'walletAdd'
        ? hotkeys.walletAdd || 'F5'
        : item.key === 'debtAdd'
          ? hotkeys.debtAdd || 'F6'
          : item.key === 'walletDeduct'
            ? hotkeys.walletDeduct || 'F7'
            : hotkeys.walletDebt || 'F8'),
  }));

  const categories = useMemo(
    () => ['همه', ...Array.from(new Set(products.map(item => item.category)))],
    [products],
  );

  const visibleProducts = useMemo(() => {
    const query = buffetQuery.trim().toLocaleLowerCase('fa-IR');
    return products.filter(item =>
      item.showcaseStock > 0 &&
      (buffetCategory === 'همه' || item.category === buffetCategory) &&
      (!query || item.name.toLocaleLowerCase('fa-IR').includes(query)),
    );
  }, [products, buffetCategory, buffetQuery]);

  const cartItems = products.filter(item => (buffetCart[item.id] ?? 0) > 0);
  const cartTotal = cartItems.reduce(
    (sum, item) => sum + item.price * (buffetCart[item.id] ?? 0),
    0,
  );

  const buffetDestinationKind: 'session' | 'pending' | 'customer' =
    activeSession ? 'session' : pendingAccount ? 'pending' : 'customer';

  const buffetDestinationLabel =
    activeSession
      ? 'جلسه فعال'
      : pendingAccount
        ? 'حساب باز'
        : (customer?.debt ?? 0) > 0
          ? 'بدهی موجود'
          : 'حساب جدید';

  function addBuffetProduct(product: ProductRecord) {
    setBuffetCart(current => ({
      ...current,
      [product.id]: Math.min(
        product.showcaseStock,
        (current[product.id] ?? 0) + 1,
      ),
    }));
  }

  function changeBuffetQuantity(product: ProductRecord, delta: number) {
    setBuffetCart(current => {
      const next = Math.max(
        0,
        Math.min(product.showcaseStock, (current[product.id] ?? 0) + delta),
      );
      const copy = { ...current };
      if (next === 0) delete copy[product.id];
      else copy[product.id] = next;
      return copy;
    });
  }

  async function submitBuffet() {
    if (!customer || !cartItems.length) return;

    if (buffetDestinationKind === 'customer') {
      const message = customer.debt > 0
        ? 'این مشتری بدهی باز دارد. بوفه به همان حساب بدهی اضافه می‌شود. ادامه می‌دهید؟'
        : 'این مشتری جلسه یا حساب باز ندارد. ثبت بوفه یک حساب باز برای مشتری ایجاد می‌کند. ادامه می‌دهید؟';
      if (!window.confirm(message)) return;
    }

    setBuffetNotice('');
    setBuffetBusy(true);
    try {
      const result = await recordServerBuffetSale(
        cartItems.map(item => ({
          productId: item.id,
          quantity: buffetCart[item.id] ?? 0,
        })),
        buffetDestinationKind,
        activeSession?.serverSessionId,
        pendingAccount?.invoiceId,
        customer.id,
      );

      setBuffetCart({});
      await onDataChanged(customer.id);

      if (activeSession && onBuffetAdded) {
        onBuffetAdded(activeSession.id, result.total);
      }
      setBuffetNotice('بوفه به همان حساب مشتری اضافه شد.');
    } catch (error) {
      setBuffetNotice(error instanceof Error ? error.message : 'ثبت بوفه انجام نشد');
    } finally {
      setBuffetBusy(false);
    }
  }

  async function submitSessionCharge() {
    if (!customer || !activeSession) return;
    const value = parseMoney(amount);
    if (!value) return;
    const saved = await onSessionCharge(activeSession, value, chargeMethod);
    if (saved) onAmountChange('');
  }

  return (
    <div className="customer-flow-workspace">
      <section className="customer-flow-search-panel">
        <div className="customer-flow-search-heading">
          <div>
            <span className="customer-flow-eyebrow">ورود سریع مشتری</span>
            <strong>مشتری را از همین پنجره پیدا و انتخاب کن</strong>
          </div>
          <div className="customer-flow-shortcuts">
            <span>{hotkeys.flow || 'F1'} Workspace</span>
            <span>{hotkeys.amount || 'F4'} مبلغ</span>
            <span>{hotkeys.walletAdd || 'F5'}–{hotkeys.walletDebt || 'F8'} عملیات</span>
          </div>
        </div>

        <div className="customer-flow-search-row">
          <label>
            <span>شناسه مشتری، username، موبایل، کد ملی یا نام</span>
            <input
              autoFocus={!customer}
              value={search}
              onChange={event => onSearchChange(event.target.value)}
              onKeyDown={event => {
                if (event.key === 'Enter') {
                  event.preventDefault();
                  onSearchSubmit();
                }
              }}
              placeholder="مثلاً 1006 یا ali123 یا 0912..."
              disabled={busy}
            />
          </label>
          <button type="button" className="btn primary" onClick={onSearchSubmit} disabled={!search.trim() || busy}>
            نمایش مشتری
          </button>
        </div>

        {!customer && matches.length > 0 && (
          <div className="customer-flow-search-results">
            {matches.map(item => (
              <button type="button" key={item.id} onClick={() => onSelectCustomer(item)} disabled={busy}>
                <span>
                  <strong>{item.name}</strong>
                  <small>@{item.username} · {item.code || item.mobile || 'بدون شناسه'}</small>
                </span>
                <em>{money(item.wallet)} تومان</em>
              </button>
            ))}
          </div>
        )}
      </section>

      {customer ? (
        <>
          <section className="customer-flow-profile">
            <div className="customer-flow-profile-head">
              <div className="customer-flow-identity">
                <div className="customer-flow-avatar">{customer.name.trim().charAt(0) || 'م'}</div>
                <div className="customer-flow-identity-text">
                  <div className="customer-flow-name-line">
                    <strong>{customer.name}</strong>
                    <span className={'status-pill ' + customer.status}>{statusLabel(customer.status)}</span>
                    <span className={'vip-tag ' + customer.vip}>{vipLabel(customer.vip)}</span>
                  </div>
                  <span className="customer-flow-meta">
                    @{customer.username} · کد {customer.code || '—'} · موبایل {customer.mobile || '—'}
                  </span>
                </div>
              </div>
              <div className="customer-flow-profile-tags">
                <span>{activeSession ? 'جلسه فعال' : pendingAccount ? 'حساب باز' : 'بدون حساب باز'}</span>
              </div>
            </div>

            <div className="customer-flow-info-grid">
              <div><span>کد ملی</span><strong>{customer.nationalId || 'ثبت نشده'}</strong></div>
              <div><span>پکیج / تعرفه</span><strong>{customer.packageName || 'بدون پکیج'}</strong></div>
              <div><span>سقف ورود همزمان</span><strong>{customer.concurrentLoginLimit ? money(customer.concurrentLoginLimit) + ' دستگاه' : 'پیش‌فرض سیستم'}</strong></div>
              <div><span>یادداشت</span><strong>{customer.notes || 'بدون یادداشت'}</strong></div>
            </div>
          </section>

          <section className="customer-flow-finance">
            <div className="customer-flow-section-head">
              <div>
                <span className="customer-flow-eyebrow">وضعیت مالی</span>
                <strong>اعتبار و بدهی</strong>
              </div>
              <span className="customer-flow-selected">آخرین وضعیت از Server</span>
            </div>

            <div className="customer-flow-stats">
              <div className="highlight"><span>کیف پول</span><strong className="positive">{money(customer.wallet)} تومان</strong></div>
              <div className={customer.debt > 0 ? 'danger-stat' : ''}><span>بدهی</span><strong className={customer.debt > 0 ? 'negative' : ''}>{money(customer.debt)} تومان</strong></div>
              <div><span>اعتبار رایگان</span><strong>{money(customer.giftCredit)} تومان</strong></div>
              <div><span>زمان رایگان</span><strong>{minutes(customer.freeTimeMinutes)}</strong></div>
              <div><span>تخفیف</span><strong>{money(customer.discountLevel)}٪</strong></div>
              <div><span>مصرف امروز</span><strong>{customer.hoursUsedToday != null ? decimal(customer.hoursUsedToday) + ' ساعت' : '—'}</strong></div>
            </div>
          </section>

          <section className="customer-flow-live-panel">
            <div className="customer-flow-section-head">
              <div><span className="customer-flow-eyebrow">وضعیت لحظه‌ای</span><strong>جلسه و دستگاه</strong></div>
              <span className="customer-flow-selected">{activeStation ? 'جلسه فعال' : 'بدون دستگاه فعال'}</span>
            </div>

            {activeStation ? (
              <div className="customer-flow-live-grid">
                <div className="customer-flow-live-primary">
                  <span>دستگاه فعلی</span>
                  <strong>{activeStation.name}</strong>
                  <small>{stationStateLabel(activeStation.state)} · {activeStation.zone === 'pc' ? 'PC' : activeStation.type}</small>
                </div>
                <div><span>زمان باقی‌مانده</span><strong>{minutes(activeStation.remainingMinutes)}</strong></div>
                <div><span>اینترنت</span><strong>{activeStation.network === 2 ? 'اینترنت ۲' : 'اینترنت ۱'}</strong></div>
                <div><span>بدهی</span><strong className={customer.debt > 0 ? 'negative' : ''}>{money(customer.debt)} تومان</strong></div>
                <div className="wide"><span>یادداشت جلسه</span><strong>{activeStation.customerNote || customer.notes || 'یادداشتی برای این جلسه ثبت نشده است.'}</strong></div>
              </div>
            ) : (
              <div className="customer-flow-no-station">
                مشتری در حال حاضر روی هیچ دستگاه فعالی نیست؛ عملیات مالی همچنان از همین پنجره قابل انجام است.
              </div>
            )}
          </section>

          <section className="customer-flow-action-panel">
            <div className="customer-flow-tabs" role="tablist" aria-label="عملیات مشتری">
              <button type="button" className={tab === 'charge' ? 'active' : ''} onClick={() => setTab('charge')}>💳 شارژ</button>
              {canSellBuffet && <button type="button" className={tab === 'buffet' ? 'active' : ''} onClick={() => setTab('buffet')}>🥤 بوفه</button>}
            </div>

            {tab === 'charge' ? (
              <>
                <div className="customer-flow-section-head">
                  <div>
                    <span className="customer-flow-eyebrow">شارژ مشتری</span>
                    <strong>{activeSession ? 'شارژ زمان همین جلسه' : 'عملیات کیف پول و بدهی'}</strong>
                  </div>
                  <span className="customer-flow-selected">
                    {activeSession ? 'Server-authoritative' : 'مشتری بدون Session فعال'}
                  </span>
                </div>

                {activeSession && (
                  <>
                    <label className="customer-flow-amount">
                      <span>مبلغ شارژ زمان · تومان</span>
                      <input
                        id="flow-amount"
                        autoComplete="off"
                        inputMode="numeric"
                        value={amount}
                        onChange={event => onAmountChange(event.target.value)}
                        onKeyDown={event => {
                          if (event.key === 'Enter') {
                            event.preventDefault();
                            void submitSessionCharge();
                          }
                        }}
                        placeholder="مثلاً ۱۰۰٬۰۰۰"
                        disabled={busy}
                      />
                    </label>

                    <div className="customer-flow-charge-methods">
                      {([
                        ['cash', 'نقد'],
                        ['card', 'کارتخوان'],
                        ['wallet', 'کیف پول'],
                      ] as Array<[ChargeMethod, string]>).map(([method, label]) => (
                        <button
                          key={method}
                          type="button"
                          className={chargeMethod === method ? 'active' : ''}
                          onClick={() => setChargeMethod(method)}
                        >{label}</button>
                      ))}
                      <button
                        type="button"
                        className="btn primary"
                        onClick={() => void submitSessionCharge()}
                        disabled={!amount.trim() || busy}
                      >
                        ثبت شارژ · {money(parseMoney(amount))} تومان
                      </button>
                    </div>
                  </>
                )}

                <div className="customer-flow-secondary-finance">
                  {activeSession && <div className="customer-flow-selected">برای عملیات F5 تا F8 از همین مبلغ بالا استفاده می‌شود.</div>}
                  {!activeSession && <label className="customer-flow-amount">
                    <span>مبلغ عملیات · تومان</span>
                    <input
                      inputMode="numeric"
                      value={amount}
                      onChange={event => onAmountChange(event.target.value)}
                      placeholder="مثلاً ۱۰۰٬۰۰۰"
                      disabled={busy}
                    />
                  </label>}
                  <div className="customer-flow-actions">
                    {actions.map(action => (
                      <button
                        type="button"
                        key={action.key}
                        className={'customer-flow-action ' + action.tone}
                        onClick={() => onAction(action.key)}
                        disabled={busy}
                      >
                        <span className="customer-flow-action-key">{action.hotkey}</span>
                        <strong>{action.title}</strong>
                        <small>{action.detail}</small>
                      </button>
                    ))}
                  </div>
                </div>
              </>
            ) : (
              <>
                <div className="customer-flow-section-head">
                  <div>
                    <span className="customer-flow-eyebrow">بوفه</span>
                    <strong>افزودن محصول به حساب مشتری</strong>
                  </div>
                  <span className="customer-flow-selected">مقصد خودکار: {buffetDestinationLabel}</span>
                </div>

                {buffetNotice && <div className="customer-buffet-notice">{buffetNotice}</div>}
                <div className="customer-buffet-toolbar">
                  <input value={buffetQuery} onChange={event => setBuffetQuery(event.target.value)} placeholder="جست‌وجوی محصول…" disabled={buffetLoading || buffetBusy} />
                  <select value={buffetCategory} onChange={event => setBuffetCategory(event.target.value)} disabled={buffetLoading || buffetBusy}>
                    {categories.map(category => <option key={category}>{category}</option>)}
                  </select>
                </div>

                <div className="customer-buffet-layout">
                  <div className="customer-buffet-products">
                    {buffetLoading ? (
                      <div className="customer-flow-empty-history">در حال دریافت کالاهای ویترین…</div>
                    ) : visibleProducts.length ? (
                      visibleProducts.map(product => (
                        <button
                          key={product.id}
                          type="button"
                          className="customer-buffet-product"
                          onDoubleClick={() => addBuffetProduct(product)}
                          title="دوبار کلیک = افزودن به انتخاب‌های مشتری"
                        >
                          <strong>{product.name}</strong>
                          <span>{money(product.price)} تومان · موجودی {product.showcaseStock.toLocaleString('fa-IR')}</span>
                          <small>دوبار کلیک برای افزودن</small>
                        </button>
                      ))
                    ) : (
                      <div className="customer-flow-empty-history">محصول قابل فروش در این فیلتر نیست.</div>
                    )}
                  </div>

                  <div className="customer-buffet-cart">
                    <div className="customer-buffet-cart-head"><strong>انتخاب‌های مشتری</strong><span>{cartItems.length.toLocaleString('fa-IR')} قلم</span></div>
                    {cartItems.length ? cartItems.map(item => (
                      <div className="customer-buffet-cart-item" key={item.id}>
                        <div><strong>{item.name}</strong><small>{money(item.price)} تومان</small></div>
                        <div className="customer-buffet-qty">
                          <button type="button" onClick={() => changeBuffetQuantity(item, -1)}>−</button>
                          <b>{(buffetCart[item.id] ?? 0).toLocaleString('fa-IR')}</b>
                          <button type="button" onClick={() => changeBuffetQuantity(item, 1)}>+</button>
                        </div>
                      </div>
                    )) : (
                      <div className="customer-flow-empty-history">هنوز محصولی انتخاب نشده است.</div>
                    )}
                    <div className="customer-buffet-total"><span>جمع</span><strong>{money(cartTotal)} تومان</strong></div>
                    <button type="button" className="btn primary customer-buffet-submit" onClick={() => void submitBuffet()} disabled={!cartItems.length || buffetBusy || busy}>
                      {buffetBusy ? 'در حال ثبت…' : 'افزودن به پروفایل مشتری'}
                    </button>
                  </div>
                </div>

                {pendingAccount && <div className="customer-buffet-account-note">این مشتری یک حساب باز دارد؛ بوفه و عملیات بعدی به همان حساب وصل می‌شوند و حساب دوم ساخته نمی‌شود.</div>}
              </>
            )}
          </section>

          <section className="customer-flow-history">
            <div className="customer-flow-section-head">
              <div><span className="customer-flow-eyebrow">سابقه</span><strong>آخرین فعالیت‌های مشتری</strong></div>
              <span className="customer-flow-selected">۵ مورد اخیر</span>
            </div>

            {customer.transactionHistory?.length ? (
              <div className="customer-flow-history-list">
                {customer.transactionHistory.slice(0, 5).map((item, index) => (
                  <div key={index}><span className="customer-flow-history-dot">●</span><strong>{item}</strong></div>
                ))}
              </div>
            ) : (
              <div className="customer-flow-empty-history">برای این مشتری سابقه‌ای در خلاصه پروفایل ثبت نشده است.</div>
            )}
          </section>
        </>
      ) : (
        <section className="customer-flow-empty">
          <div className="customer-flow-empty-icon">⌕</div>
          <strong>هنوز مشتری انتخاب نشده است</strong>
          <span>شناسه، username، موبایل یا نام مشتری را وارد کن. بعد از انتخاب، اطلاعات هویتی، مالی، دستگاه و عملیات در همین پنجره نمایش داده می‌شود.</span>
        </section>
      )}
    </div>
  );
}
