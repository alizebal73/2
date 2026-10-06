import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { CSSProperties, MouseEvent } from 'react';
import type { AppUserRecord, CustomerRecord, DashboardSnapshotDto, PendingSettlementAccount, ServerInfoDto, SessionTimelineEvent, StationDto, StationState, ZoneKey } from '../types';
import { createServerCustomerDebt, getCustomerHistory, getServerCustomers } from '../services/customerService';
import { hasPermission } from '../services/authService';
import { recordWalletTransaction } from '../services/walletLedgerService';
import { getAgentCommand, requestAgentRollback, requestAgentUpdate, sendAgentCommand, updateAgentPolicy } from '../services/agentService';
import { calculateBilling } from '../services/billingEngine';
import { adjustServerSessionTime, chargeServerSession, getPendingSettlementAccounts, isServerGuid, markPendingSettlementAsDebt, pauseServerSession, requestServerInvoiceReverseApproval, resumeServerSession, settlePendingSettlement, settleServerSession, settleServerSessionLater, startServerSession, transferServerSession, updateServerSessionDetails } from '../services/sessionService';
import { SessionCenter } from '../features/session/SessionCenter';
import { userErrorMessage } from '../utils/userError';
import { DashboardAttentionSidebar, type SidebarAttentionItem } from '../features/attention/DashboardAttentionSidebar';
import { ReverseDialog } from '../components/ReverseDialog';
import { CustomerOperationsWorkspace } from '../features/customer/CustomerOperationsWorkspace';

const zoneLabels: Record<ZoneKey, string> = { all: 'همه', pc: 'رایانه‌ها (۴۰)', console: 'کنسول‌ها (۱۶)', table: 'میزها (۵)' };
const stateLabels: Record<StationState, string> = { free: 'آماده استفاده', busy: 'در حال استفاده', paused: 'متوقف', reserved: 'رزرو', off: 'خاموش / خارج از سرویس' };
const emptyStations: StationDto[] = [];
type ViewMode = 'v-card' | 'v-compact' | 'v-list';
type PcGroupBy = 'state' | 'vip' | 'network' | 'remaining';
type StationSortKey = 'computer' | 'identifier' | 'surname' | 'remaining' | 'debt' | 'note' | 'status';
type StationSortDirection = 'asc' | 'desc';
type ModalKind = 'start' | 'flow' | 'charge' | 'settle' | 'extend' | 'reduce' | null;

function money(value: number) { return new Intl.NumberFormat('fa-IR').format(Math.round(value)); }
function number(value: string) { return Number(value.replace(/[۰-۹]/g, digit => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(digit))).replace(/[٬,\s]/g, '')) || 0; }

type Props = {
  snapshot: DashboardSnapshotDto | null;
  apiState: 'loading' | 'online' | 'offline';
  serverInfo: ServerInfoDto | null;
  error: string;
  onNavigate: (page: 'client-shell') => void;
  role?: 'operator' | 'manager' | 'owner';
  user: AppUserRecord;
};

type ContextMenu = { x: number; y: number; station: StationDto } | null;
type AttentionKind = 'action' | 'warning' | 'info';
type SessionFollowUp = { id: string; stationId: string; stationName: string; customerCode: string; amount: number; createdAt: string; status: 'watching' | 'unpaid' | 'paid'; };
type AttentionItem = { id: string; kind: AttentionKind; station: StationDto; title: string; detail: string; actionLabel: string; followUpId?: string; };

export function DashboardPage({ snapshot, apiState, serverInfo, onNavigate: _onNavigate, user }: Props) {
  const canStartSession = hasPermission(user, 'session.start');
  const canManageSession = hasPermission(user, 'session.manage');
  const canSettleSession = hasPermission(user, 'session.settle');
  const canControlClient = hasPermission(user, 'client.control');
  const canPowerClient = hasPermission(user, 'client.power');
  const [stationOverrides, setStationOverrides] = useState<StationDto[] | null>(null);
  const [zone, setZone] = useState<ZoneKey>('all');
  const [query, setQuery] = useState('');
  const [pcGroupBy, setPcGroupBy] = useState<PcGroupBy>('state');
  const [view, setView] = useState<ViewMode>('v-card');
  const [stationSort, setStationSort] = useState<{ key: StationSortKey; direction: StationSortDirection }>({ key: 'computer', direction: 'asc' });
  const [zoom, setZoom] = useState(100);
  const [now, setNow] = useState(Date.now());
  const [modal, setModal] = useState<ModalKind>(null);
  const [activeStation, setActiveStation] = useState<StationDto | null>(null);
  const [sessionCenterStation, setSessionCenterStation] = useState<StationDto | null>(null);
  const [customers, setCustomers] = useState<CustomerRecord[]>([]);
  const [customerCode, setCustomerCode] = useState('');
  const [persons, setPersons] = useState(1);
  const [amount, setAmount] = useState('');
  const [chargeTarget, setChargeTarget] = useState<'session' | 'wallet' | 'discount'>('session');
  const [context, setContext] = useState<ContextMenu>(null);
  const [sessionFollowUps, setSessionFollowUps] = useState<SessionFollowUp[]>([]);
  const [pendingSettlements, setPendingSettlements] = useState<PendingSettlementAccount[]>([]);
  const [sessionTimeline, setSessionTimeline] = useState<SessionTimelineEvent[]>([]);
  const [message, setMessage] = useState('');
  const [reverseRequest, setReverseRequest] = useState<SessionTimelineEvent | null>(null);
  const [reversedEventIds, setReversedEventIds] = useState<string[]>([]);
  const [flowStep, setFlowStep] = useState<1 | 2>(1);
  const [flowCustomerId, setFlowCustomerId] = useState<string | null>(null);
  const [flowBusy, setFlowBusy] = useState(false);
  const [extendMinutes, setExtendMinutes] = useState(30);
  const [customExtendMinutes, setCustomExtendMinutes] = useState('30');
  const [reduceMinutes, setReduceMinutes] = useState(15);
  const [customReduceMinutes, setCustomReduceMinutes] = useState('15');
  const [discountPercent, setDiscountPercent] = useState(0);
  const [settlementWhyOpen, setSettlementWhyOpen] = useState(false);
  const [receivedAmount, setReceivedAmount] = useState('');
  const [splitPaymentEnabled, setSplitPaymentEnabled] = useState(false);
  const [splitCash, setSplitCash] = useState('');
  const [splitCard, setSplitCard] = useState('');
  const [splitWallet, setSplitWallet] = useState('');
  const [hotkeys, setHotkeys] = useState<Record<string,string>>(() => { try { return JSON.parse(localStorage.getItem('gamenet-hotkeys-v1') || '{}'); } catch { return {}; } });
  const [selectedStationIds, setSelectedStationIds] = useState<string[]>([]);
  const [selectionAnchorId, setSelectionAnchorId] = useState<string | null>(null);
  const [selectionRect, setSelectionRect] = useState<{ startX: number; startY: number; endX: number; endY: number } | null>(null);
  const selectionRectRef = useRef<{ startX: number; startY: number; endX: number; endY: number } | null>(null);
  const selectionDragRef = useRef<{ stationId: string; startX: number; startY: number; dragging: boolean; ctrlKey: boolean; shiftKey: boolean } | null>(null);
  const suppressNextStationClickRef = useRef(false);

  const stations = stationOverrides
    ? stationOverrides.map(station => {
        const serverStation = snapshot?.stations.find(item => item.id === station.id);
        return serverStation
          ? {
              ...station,
              state: serverStation.state,
              customerUsername: serverStation.customerUsername,
              customerFullName: serverStation.customerFullName,
              customerDebt: serverStation.customerDebt,
              customerNote: serverStation.customerNote,
              remainingMinutes: serverStation.remainingMinutes,
              serverSessionId: serverStation.serverSessionId,
              buffetTotal: serverStation.buffetTotal,
              agentOnline: serverStation.agentOnline,
              agentLastSeenAt: serverStation.agentLastSeenAt,
              agentVersion: serverStation.agentVersion,
              agentLocked: serverStation.agentLocked,
              agentKioskEnabled: serverStation.agentKioskEnabled,
              agentLockOnDisconnect: serverStation.agentLockOnDisconnect,
              agentLifecycleState: serverStation.agentLifecycleState,
              agentPendingUpdateVersion: serverStation.agentPendingUpdateVersion,
              agentLastUpdateError: serverStation.agentLastUpdateError,
              agentLastHealthyAt: serverStation.agentLastHealthyAt,
              sessionStartedAt: serverStation.sessionStartedAt,
              sessionPausedAt: serverStation.sessionPausedAt,
              sessionPausedMinutes: serverStation.sessionPausedMinutes,
              sessionTimeAdjustmentMinutes: serverStation.sessionTimeAdjustmentMinutes,
              sessionPrepaidAmount: serverStation.sessionPrepaidAmount,
            }
          : station;
      })
    : snapshot?.stations ?? emptyStations;
  const liveSessionCenterStation = sessionCenterStation ? stations.find(item => item.id === sessionCenterStation.id) ?? null : null;
  const updateStation = useCallback((id: string, update: Partial<StationDto>) => {
    setStationOverrides(items => (items ?? snapshot?.stations ?? emptyStations).map(item => item.id === id ? { ...item, ...update } : item));
  }, [snapshot?.stations]);
  const refreshPendingSettlements = useCallback(async () => {
    const rows = await getPendingSettlementAccounts();
    setPendingSettlements(rows);
  }, []);

  const duration = useCallback((station: StationDto) => {
    const startedAt = station.startedAt ?? station.sessionStartedAt ?? undefined;
    if (!startedAt) return station.sessionMinutes ?? 0;
    const pausedAt = station.pausedAt ?? station.sessionPausedAt ?? undefined;
    const pausedMinutes = station.pausedMinutes ?? station.sessionPausedMinutes ?? 0;
    const timeAdjustment = station.sessionTimeAdjustmentMinutes ?? 0;
    const referenceNow = station.state === 'paused' && pausedAt ? new Date(pausedAt).getTime() : now;
    const activePauseMinutes = station.state === 'paused' && pausedAt
      ? Math.max(0, (referenceNow - new Date(pausedAt).getTime()) / 60000)
      : 0;
    return Math.max(0, (referenceNow - new Date(startedAt).getTime()) / 60000 - pausedMinutes - activePauseMinutes + timeAdjustment);
  }, [now]);
  function addSessionTimeline(stationId: string, kind: SessionTimelineEvent['kind'], title: string, detail: string, amount?: number, serverReferenceId?: string) {
    setSessionTimeline(current => [{ id: crypto.randomUUID(), stationId, createdAt: new Date().toISOString(), kind, title, detail, amount, serverReferenceId }, ...current].slice(0, 300));
  }

  const chargeFlowSession = useCallback(async (station: StationDto, value: number, method: 'cash' | 'card' | 'wallet'): Promise<boolean> => {
    if (!station.serverSessionId || !isServerGuid(station.serverSessionId)) {
      throw new Error('جلسه باید روی Server ثبت شده باشد.');
    }
    try {
      const result = await chargeServerSession(station.serverSessionId, value, method);
      updateStation(station.id, {
        sessionCredit: result.prepaidTotal,
        sessionPrepaidAmount: result.prepaidTotal,
        prepaidEndsAt: result.sessionEndAt ?? undefined,
      });
      if (method === 'wallet') {
        const customer = customers.find(item =>
          item.code === station.customerCode ||
          item.username === station.customerCode ||
          item.id === station.customerCode);
        if (customer) {
          setCustomers(current => current.map(item =>
            item.id === customer.id ? { ...item, wallet: result.walletBalanceAfter } : item));
        }
      }
      addSessionTimeline(station.id, 'charge', 'شارژ جلسه', money(value) + ' تومان شارژ شد', value, result.invoiceId);
      await refreshPendingSettlements();
      setMessage(money(value) + ' تومان شارژ شد؛ حساب مالی مشتری روی Server به‌روز شد.');
      return true;
    } catch (error) {
      setMessage(userErrorMessage(error, 'ثبت شارژ انجام نشد'));
      return false;
    }
  }, [customers, refreshPendingSettlements, updateStation]);

  const applyFlow = useCallback(async (action: 'walletAdd' | 'debtAdd' | 'walletDeduct' | 'walletDebt') => {
    const value = number(amount);
    if (!value) { setMessage('مبلغ معتبر وارد کنید'); return; }
    const customer = flowCustomerId ? customers.find(item => item.id === flowCustomerId) : null;
    if (!customer) { setMessage('ابتدا یک مشتری را انتخاب کنید'); return; }
    setFlowBusy(true);
    try {
      if (action === 'walletAdd') {
        await recordWalletTransaction(customer.id, { amount: value, type: 'credit', description: 'شارژ مستقیم توسط اپراتور' });
        setMessage('شارژ مستقیم ' + money(value) + ' تومان ثبت شد');
      } else if (action === 'debtAdd') {
        await createServerCustomerDebt(customer.id, value, 'ثبت بدهی توسط اپراتور');
        setMessage('بدهی ' + money(value) + ' تومان ثبت شد');
      } else if (action === 'walletDeduct') {
        await recordWalletTransaction(customer.id, { amount: value, type: 'debit', description: 'کسر مستقیم توسط اپراتور' });
        setMessage(money(value) + ' تومان از کیف پول کسر شد');
      } else {
        const deducted = Math.min(customer.wallet, value);
        const remainder = Math.max(0, value - deducted);
        if (deducted > 0) {
          await recordWalletTransaction(customer.id, { amount: deducted, type: 'debit', description: 'کسر کیف پول و ثبت مابه‌التفاوت' });
        }
        if (remainder > 0) {
          await createServerCustomerDebt(customer.id, remainder, 'باقی‌مانده عملیات کیف پول + بدهی');
        }
        setMessage('عملیات کیف پول + بدهی ثبت شد؛ ' + money(remainder) + ' تومان به بدهی منتقل شد');
      }
      const refreshed = await getServerCustomers();
      const history = await getCustomerHistory(customer.id).catch(() => []);
      const historyText = history.slice(0, 5).map(item => {
        const amountText = item.amount ? ' · ' + money(item.amount) + ' تومان' : '';
        const dateText = item.createdAt ? ' · ' + new Date(item.createdAt).toLocaleString('fa-IR') : '';
        return (item.description || item.type || 'فعالیت مشتری') + amountText + dateText;
      });
      setCustomers(refreshed.map(item => item.id === customer.id ? { ...item, transactionHistory: historyText } : item));
      setAmount('');
    } catch (error) {
      setMessage(userErrorMessage(error, 'ثبت تراکنش مشتری انجام نشد'));
    } finally {
      setFlowBusy(false);
    }
  }, [amount, customers, flowCustomerId]);
  useEffect(() => {
    void getServerCustomers()
      .then(setCustomers)
      .catch(error => setMessage(userErrorMessage(error, 'دریافت مشتریان از سرور انجام نشد')));
    void refreshPendingSettlements().catch(() => undefined);
    const pendingTimer = window.setInterval(() => {
      void refreshPendingSettlements().catch(() => undefined);
    }, 3500);
    const onHotkeys = (event: Event) => setHotkeys((event as CustomEvent<Record<string,string>>).detail || {});
    window.addEventListener('gamenet-hotkeys-changed', onHotkeys);
    return () => {
      window.clearInterval(pendingTimer);
      window.removeEventListener('gamenet-hotkeys-changed', onHotkeys);
    };
  }, [refreshPendingSettlements]);
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);
  useEffect(() => {
    if (!context) return;
    const close = (event: globalThis.MouseEvent) => {
      const target = event.target;
      if (target instanceof Element && target.closest('.context-menu')) return;
      setContext(null);
    };
    const closeOnViewportChange = () => setContext(null);
    document.addEventListener('mousedown', close);
    window.addEventListener('resize', closeOnViewportChange);
    window.addEventListener('scroll', closeOnViewportChange, true);
    return () => {
      document.removeEventListener('mousedown', close);
      window.removeEventListener('resize', closeOnViewportChange);
      window.removeEventListener('scroll', closeOnViewportChange, true);
    };
  }, [context]);

  useEffect(() => {
    if (!message) return;
    const timer = window.setTimeout(() => setMessage(''), 3200);
    return () => window.clearTimeout(timer);
  }, [message]);
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const pressed = event.key.toUpperCase();
      if (event.key === 'Escape') {
        setModal(null);
        setContext(null);
        setSessionCenterStation(null);
        setSelectedStationIds([]);
        setSelectionAnchorId(null);
        setSelectionRect(null);
        return;
      }
      if ((event.ctrlKey || event.metaKey) && pressed === 'A' && !['INPUT', 'TEXTAREA', 'SELECT'].includes((event.target as HTMLElement)?.tagName)) {
        event.preventDefault();
        event.stopPropagation();
        setSelectedStationIds(visibleStations.map(item => item.id));
        setSelectionAnchorId(visibleStations[visibleStations.length - 1]?.id ?? null);
        return;
      }

      const flowKey = (hotkeys.flow || 'F1').toUpperCase();
      const amountKey = (hotkeys.amount || 'F4').toUpperCase();
      const walletAddKey = (hotkeys.walletAdd || 'F5').toUpperCase();
      const debtAddKey = (hotkeys.debtAdd || 'F6').toUpperCase();
      const walletDeductKey = (hotkeys.walletDeduct || 'F7').toUpperCase();
      const walletDebtKey = (hotkeys.walletDebt || 'F8').toUpperCase();

      if (event.repeat && [flowKey, amountKey, walletAddKey, debtAddKey, walletDeductKey, walletDebtKey].includes(pressed)) return;

      if (pressed === flowKey) {
        event.preventDefault();
        event.stopPropagation();
        setActiveStation(null);
        setFlowCustomerId(null);
        setCustomerCode('');
        setAmount('');
        setFlowStep(1);
        setModal('flow');
        return;
      }

      if (modal === 'flow' && flowStep === 2 && pressed === amountKey) {
        event.preventDefault();
        event.stopPropagation();
        document.getElementById('flow-amount')?.focus();
        return;
      }

      if (modal === 'flow' && flowStep === 2) {
        const action = pressed === walletAddKey
          ? 'walletAdd'
          : pressed === debtAddKey
            ? 'debtAdd'
            : pressed === walletDeductKey
              ? 'walletDeduct'
              : pressed === walletDebtKey
                ? 'walletDebt'
                : null;
        if (action) {
          event.preventDefault();
          event.stopPropagation();
          void applyFlow(action);
        }
      }
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [modal, flowStep, applyFlow, hotkeys, visibleStations]);
  useEffect(() => {
    const onCommand = (event: Event) => {
      const command = (event as CustomEvent<string>).detail;
      if (command === 'start-session') open('start', stations.find(item => item.state === 'free') ?? null);
      if (command === 'quick-charge') open('charge', stations.find(item => item.state === 'busy') ?? null);
      if (command === 'flow') {
        const busyStation = stations.find(item => item.state === 'busy') ?? null;
        const busyCustomer = busyStation ? customers.find(item =>
          item.username === busyStation.customerUsername ||
          item.code === busyStation.customerCode ||
          item.username === busyStation.customerCode
        ) : null;
        open('flow', busyStation);
        setFlowCustomerId(busyCustomer?.id ?? null);
        setCustomerCode(busyCustomer ? (busyCustomer.username || busyCustomer.code || '') : '');
        setFlowStep(busyCustomer ? 2 : 1);
      }
      if (command === 'extend-session') {
        const station = stations.find(item => item.state === 'busy');
        if (station) {
          const minutes = duration(station) + 30;
          updateStation(station.id, { sessionMinutes: minutes, startedAt: new Date(Date.now() - minutes * 60000).toISOString() });
          setMessage('۳۰ دقیقه به جلسه فعال اضافه شد');
        } else setMessage('جلسه فعالی وجود ندارد');
      }
    };
    window.addEventListener('gamenet-command', onCommand);
    return () => window.removeEventListener('gamenet-command', onCommand);
  }, [stations, duration, updateStation]);
  useEffect(() => {
    const onBuffetSale = (event: Event) => {
      const detail = (event as CustomEvent<{ total: number; sessionId?: string; invoiceId?: string; buffetTotal?: number }>).detail;
      const station = detail.sessionId
        ? stations.find(item => item.serverSessionId === detail.sessionId)
        : undefined;
      if (station) {
        const buffetTotal = detail.buffetTotal ?? ((station.buffetTotal ?? 0) + detail.total);
        updateStation(station.id, { buffetTotal });
        addSessionTimeline(station.id, 'buffet', 'افزودن بوفه', money(detail.total) + ' تومان به فاکتور جلسه اضافه شد', detail.total);
        setMessage('فروش ' + money(detail.total) + ' تومان به فاکتور ' + station.name + ' اضافه شد');
      } else if (detail.invoiceId) {
        void refreshPendingSettlements().catch(() => undefined);
        setMessage('فروش بوفه به حساب باز مشتری اضافه شد');
      } else setMessage('جلسه مقصد در داشبورد پیدا نشد؛ داشبورد را تازه‌سازی کنید');
    };
    window.addEventListener('gamenet-buffet-sale', onBuffetSale);
    return () => window.removeEventListener('gamenet-buffet-sale', onBuffetSale);
  }, [stations, updateStation, refreshPendingSettlements]);

  const visibleStations = useMemo(() => stations.filter(station =>
    (zone === 'all' || station.zone === zone) && station.name.toLowerCase().includes(query.trim().toLowerCase())), [stations, zone, query]);

  const stationSortLabels: Record<StationSortKey, string> = {
    computer: 'رایانه',
    identifier: 'شناسه',
    surname: 'نام خانوادگی',
    remaining: 'زمان باقی‌مانده',
    debt: 'بدهکاری',
    note: 'توضیحات',
    status: 'وضعیت رایانه',
  };

  const stationSortButtons = (Object.keys(stationSortLabels) as StationSortKey[]);

  function lastNameForStation(station: StationDto) {
    const customer = customers.find(item =>
      item.username === station.customerCode ||
      item.code === station.customerCode ||
      item.id === station.customerCode
    );
    const fullName = (station.customerFullName || customer?.name || '').trim();
    if (!fullName) return '';
    const pieces = fullName.split(/\s+/).filter(Boolean);
    return pieces[pieces.length - 1] || '';
  }

  function identifierForStation(station: StationDto) {
    return (station.customerCode || station.customerUsername || '').trim();
  }

  function noteForStation(station: StationDto) {
    const customer = customers.find(item =>
      item.username === station.customerCode ||
      item.code === station.customerCode ||
      item.id === station.customerCode
    );
    return (station.customerNote || customer?.notes || customer?.alias || '').trim();
  }

  function remainingForStation(station: StationDto) {
    if (station.prepaidEndsAt) return Math.max(0, Math.ceil((new Date(station.prepaidEndsAt).getTime() - now) / 60000));
    return station.remainingMinutes ?? (station.state === 'busy' ? duration(station) : 0);
  }

  function debtForStation(station: StationDto) {
    const customer = customers.find(item =>
      item.username === station.customerCode ||
      item.code === station.customerCode ||
      item.id === station.customerCode
    );
    return station.customerDebt ?? customer?.debt ?? 0;
  }

  function compareStationValues(a: StationDto, b: StationDto, key: StationSortKey) {
    if (key === 'computer') return a.name.localeCompare(b.name, 'fa', { numeric: true });
    if (key === 'identifier') return identifierForStation(a).localeCompare(identifierForStation(b), 'fa', { numeric: true });
    if (key === 'surname') return lastNameForStation(a).localeCompare(lastNameForStation(b), 'fa', { numeric: true });
    if (key === 'remaining') return remainingForStation(a) - remainingForStation(b);
    if (key === 'debt') return debtForStation(a) - debtForStation(b);
    if (key === 'note') return noteForStation(a).localeCompare(noteForStation(b), 'fa', { numeric: true });
    const statusRank: Record<StationState, number> = { free: 1, busy: 2, paused: 3, reserved: 4, off: 5 };
    return (statusRank[a.state as StationState] ?? 99) - (statusRank[b.state as StationState] ?? 99);
  }

  function toggleStationSort(key: StationSortKey) {
    setStationSort(current => current.key === key
      ? { key, direction: current.direction === 'asc' ? 'desc' : 'asc' }
      : { key, direction: 'asc' });
  }

  const sortedVisibleStations = useMemo(() => {
    const items = [...visibleStations];
    const direction = stationSort.direction === 'asc' ? 1 : -1;
    return items.sort((a, b) => {
      const primary = compareStationValues(a, b, stationSort.key) * direction;
      return primary || a.name.localeCompare(b.name, 'fa', { numeric: true });
    });
  }, [visibleStations, stationSort, customers, now]);

const pcGroupLabel = (station: StationDto) => {
    const customer = customers.find(item => item.username === station.customerCode || item.code === station.customerCode);
    if (pcGroupBy === 'vip') return customer && customer.vip !== 'none' ? 'VIP' : 'عادی';
    if (pcGroupBy === 'network') return station.network === 2 ? 'اینترنت ۲' : 'اینترنت ۱';
    if (pcGroupBy === 'remaining') {
      if (station.prepaidEndsAt) {
        const remaining = Math.max(0, Math.ceil((new Date(station.prepaidEndsAt).getTime() - now) / 60000));
        if (remaining === 0) return 'زمان تمام‌شده';
        if (remaining < 15) return 'کمتر از ۱۵ دقیقه';
        if (remaining <= 30) return '۱۵ تا ۳۰ دقیقه';
        if (remaining <= 60) return '۳۰ تا ۶۰ دقیقه';
        return 'بیشتر از ۶۰ دقیقه';
      }
      return 'بدون زمان پایان';
    }
    return stateLabels[station.state as StationState] ?? station.state;
  };

const sortedPcGroupedStations = useMemo(() => {
    const pcStations = sortedVisibleStations.filter(item => item.zone === 'pc');
    const groups = new Map<string, StationDto[]>();
    for (const station of pcStations) {
      const key = pcGroupLabel(station);
      const bucket = groups.get(key) ?? [];
      bucket.push(station);
      groups.set(key, bucket);
    }
    return Array.from(groups.entries());
  }, [sortedVisibleStations, customers, pcGroupBy, now, stationSort]);

  const selectStationWithModifiers = useCallback((stationId: string, ctrlKey: boolean, shiftKey: boolean) => {
    if (shiftKey && selectionAnchorId) {
      const anchorIndex = sortedVisibleStations.findIndex(item => item.id === selectionAnchorId);
      const targetIndex = sortedVisibleStations.findIndex(item => item.id === stationId);
      if (anchorIndex >= 0 && targetIndex >= 0) {
        const [from, to] = anchorIndex < targetIndex ? [anchorIndex, targetIndex] : [targetIndex, anchorIndex];
        const rangeIds = sortedVisibleStations.slice(from, to + 1).map(item => item.id);
        setSelectedStationIds(current => ctrlKey ? Array.from(new Set([...current, ...rangeIds])) : rangeIds);
        return;
      }
    }
    if (ctrlKey) {
      setSelectedStationIds(current => current.includes(stationId) ? current.filter(id => id !== stationId) : [...current, stationId]);
    } else {
      setSelectedStationIds([stationId]);
    }
    setSelectionAnchorId(stationId);
  }, [selectionAnchorId, sortedVisibleStations]);

  useEffect(() => {
    const onMove = (event: globalThis.MouseEvent) => {
      const drag = selectionDragRef.current;
      if (!drag) return;
      const moved = Math.abs(event.clientX - drag.startX) + Math.abs(event.clientY - drag.startY);
      if (!drag.dragging && moved < 6) return;
      drag.dragging = true;
      suppressNextStationClickRef.current = true;
      const nextRect = { startX: drag.startX, startY: drag.startY, endX: event.clientX, endY: event.clientY };
      selectionRectRef.current = nextRect;
      setSelectionRect(nextRect);
    };
    const onUp = () => {
      const drag = selectionDragRef.current;
      if (!drag) return;
      const finalRect = selectionRectRef.current;
      if (drag.dragging && finalRect) {
        const left = Math.min(finalRect.startX, finalRect.endX);
        const right = Math.max(finalRect.startX, finalRect.endX);
        const top = Math.min(finalRect.startY, finalRect.endY);
        const bottom = Math.max(finalRect.startY, finalRect.endY);
        const selected = Array.from(document.querySelectorAll<HTMLElement>('[data-station-id]'))
          .filter(element => {
            const rect = element.getBoundingClientRect();
            return rect.right >= left && rect.left <= right && rect.bottom >= top && rect.top <= bottom;
          })
          .map(element => element.dataset.stationId)
          .filter((id): id is string => Boolean(id));
        setSelectedStationIds(current => {
          if (drag.shiftKey && selectionAnchorId) return Array.from(new Set([...current, ...selected]));
          if (drag.ctrlKey) return Array.from(new Set([...current, ...selected]));
          return selected;
        });
        setSelectionAnchorId(selected[selected.length - 1] ?? null);
      } else if (!drag.dragging && !drag.stationId && !drag.ctrlKey && !drag.shiftKey) {
        setSelectedStationIds([]);
        setSelectionAnchorId(null);
      }
      setSelectionRect(null);
      selectionRectRef.current = null;
      selectionDragRef.current = null;
      window.setTimeout(() => {
        suppressNextStationClickRef.current = false;
      }, 0);
    };
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
    return () => {
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
    };
  }, [selectionAnchorId]);


  const counts = useMemo(() => ({
    free: stations.filter(item => item.state === 'free').length,
    busy: stations.filter(item => item.state === 'busy').length,
    reserved: stations.filter(item => item.state === 'reserved').length,
    off: stations.filter(item => item.state === 'off').length,
  }), [stations]);

  const attentionItems = useMemo<AttentionItem[]>(() => {
    const items: AttentionItem[] = [];
    const followUpsByStation = new Map(sessionFollowUps.filter(item => item.status !== 'paid').map(item => [item.stationId, item]));
    for (const station of stations) {
      if (station.state === 'paused') items.push({ id: `paused-${station.id}`, kind: 'action', station, title: `${station.name} · جلسه متوقف`, detail: station.customerCode ? `مشتری ${station.customerCode} منتظر ادامه جلسه است.` : 'جلسه مهمان متوقف شده و نیازمند بررسی است.', actionLabel: 'ادامه جلسه' });
      if (station.state === 'busy' && station.prepaidEndsAt) {
        const minutesUntil = Math.ceil((new Date(station.prepaidEndsAt).getTime() - now) / 60000);
        if (minutesUntil <= 0) items.push({ id: `prepaid-ended-${station.id}`, kind: 'action', station, title: `${station.name} · اعتبار تمام شد`, detail: station.customerCode ? `اعتبار شارژ مشتری ${station.customerCode} تمام شده است؛ تمدید یا پایان جلسه را بررسی کنید.` : 'اعتبار شارژ جلسه تمام شده است؛ تمدید یا پایان جلسه را بررسی کنید.', actionLabel: 'بررسی جلسه' });
        else if (minutesUntil <= 10) items.push({ id: `prepaid-low-${station.id}`, kind: 'warning', station, title: `${station.name} · کمتر از ${minutesUntil} دقیقه اعتبار`, detail: 'شارژ دستی این جلسه رو به پایان است.', actionLabel: 'مشاهده' });
      }
      const followUp = followUpsByStation.get(station.id);
      if (followUp) {
        items.push({
          id: `followup-${followUp.id}`,
          kind: followUp.status === 'unpaid' ? 'action' : 'info',
          station,
          followUpId: followUp.id,
          title: followUp.status === 'unpaid' ? `${station.name} · پرداخت هنوز مانده` : `${station.name} · شارژ دستی ثبت شده`,
          detail: `یوزر ${followUp.customerCode || 'مهمان'} · ${money(followUp.amount)} تومان`,
          actionLabel: followUp.status === 'unpaid' ? 'یادآوری پرداخت' : 'مشاهده',
        });
      }
      if (station.state === 'off') items.push({ id: `off-${station.id}`, kind: 'action', station, title: `${station.name} · خارج از سرویس`, detail: station.outOfServiceReason ?? 'ایستگاه برای استفاده عادی در دسترس نیست.', actionLabel: 'بررسی ایستگاه' });
      if (station.state === 'reserved' && station.reservationAt) {
        const reservationMs = new Date(station.reservationAt).getTime();
        const minutesUntil = Math.round((reservationMs - now) / 60000);
        if (minutesUntil >= 0 && minutesUntil <= 30) items.push({ id: `reservation-${station.id}`, kind: 'warning', station, title: `${station.name} · رزرو نزدیک`, detail: minutesUntil === 0 ? 'زمان رزرو همین حالا فرا رسیده است.' : `${minutesUntil} دقیقه تا شروع رزرو باقی مانده است.`, actionLabel: 'مشاهده ایستگاه' });
      }
    }
    return items.sort((a, b) => { const rank: Record<AttentionKind, number> = { action: 0, warning: 1, info: 2 }; return rank[a.kind] - rank[b.kind]; });
  }, [stations, now, sessionFollowUps]);

  const sidebarAttentions = useMemo<SidebarAttentionItem[]>(() => attentionItems.map(item => ({
    id: item.id,
    kind: item.kind,
    title: item.title,
    detail: item.detail,
    actionLabel: item.actionLabel,
  })), [attentionItems]);


  const isReversibleTimelineEvent = useCallback((event: SessionTimelineEvent) => {
    return ['charge', 'extend', 'reduce', 'buffet'].includes(event.kind) && !reversedEventIds.includes(event.id);
  }, [reversedEventIds]);

  const sidebarRecentActions = useMemo(() => sessionTimeline.map(item => ({
    id: item.id,
    title: item.title,
    station: stations.find(row => row.id === item.stationId)?.name ?? 'ایستگاه حذف‌شده',
    detail: item.detail,
    createdAt: item.createdAt,
    kind: item.kind,
    canReverse: isReversibleTimelineEvent(item),
  })), [sessionTimeline, stations, isReversibleTimelineEvent]);

  function focusAttentionItem(item: AttentionItem) {
    setZone(item.station.zone as ZoneKey);
    setQuery(item.station.name);
    window.setTimeout(() => { document.getElementById(`station-${item.station.id}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' }); }, 80);
    if (item.kind === 'action' && item.station.state === 'paused') { resumeSession(item.station); return; }
    if (item.kind === 'action' && item.station.state === 'busy' && item.station.prepaidEndsAt) { setSessionCenterStation(item.station); return; }
    if (item.followUpId && item.kind === 'action') { setMessage('پرداخت این مبلغ را از مشتری پیگیری کنید'); return; }
    if (item.kind === 'action' && item.station.state === 'off') { openContextAt(window.innerWidth / 2, Math.min(window.innerHeight - 80, 260), item.station); return; }
    setMessage(item.detail);
  }

  function open(kind: ModalKind, station: StationDto | null = null) {
    setActiveStation(station);
    setAmount('');
    setPersons(station?.zone === 'pc' ? 1 : 2);
    setChargeTarget('session');
    setCustomerCode(station?.customerCode ?? '');
    setDiscountPercent(0);
    
    setSettlementWhyOpen(false);
    setReceivedAmount('');
    setSplitPaymentEnabled(false);
    setSplitCash('');
    setSplitCard('');
    setSplitWallet('');
    setExtendMinutes(30);
    setCustomExtendMinutes('30');
    setReduceMinutes(15);
    setCustomReduceMinutes('15');
    setContext(null);
    setModal(kind);
  }
  function findCustomer(needle: string) {
    const value = needle.trim().toLocaleLowerCase('fa-IR');
    if (!value) return undefined;
    return customers.find(item => [item.code, item.username, item.mobile, item.id, item.name, item.alias].filter(Boolean).some(candidate => String(candidate).toLocaleLowerCase('fa-IR') === value));
  }

  function lookupStartCustomer() {
    const found = findCustomer(customerCode);
    if (!found) {
      setMessage('مشتری با این شناسه پیدا نشد؛ کد، نام کاربری یا موبایل را بررسی کنید.');
      return;
    }
    setCustomerCode(found.username || found.code || customerCode.trim());
    setMessage('پروفایل ' + found.name + ' پیدا شد و آماده ورود است.');
  }

  async function selectFlowCustomer(customer: CustomerRecord) {
    setFlowCustomerId(customer.id);
    setCustomerCode(customer.username || customer.code || customer.mobile || customer.id);
    setAmount('');
    setFlowStep(2);

    try {
      const history = await getCustomerHistory(customer.id);
      const historyText = history.slice(0, 5).map(item => {
        const amountText = item.amount ? ' · ' + money(item.amount) + ' تومان' : '';
        const dateText = item.createdAt ? ' · ' + new Date(item.createdAt).toLocaleString('fa-IR') : '';
        return (item.description || item.type || 'فعالیت مشتری') + amountText + dateText;
      });
      setCustomers(current => current.map(item => item.id === customer.id ? { ...item, transactionHistory: historyText } : item));
    } catch (error) {
      setMessage(userErrorMessage(error, 'دریافت تاریخچه مشتری انجام نشد'));
    }
  }

  function submitFlowSearch() {
    const needle = customerCode.trim().toLocaleLowerCase('fa-IR');
    if (!needle) { setMessage('شناسه مشتری را وارد کنید'); return; }
    const exact = findCustomer(customerCode);
    const partial = exact ?? customers.find(item =>
      [item.code, item.username, item.mobile, item.id, item.name, item.alias]
        .filter(Boolean)
        .some(candidate => String(candidate).toLocaleLowerCase('fa-IR').includes(needle))
    );
    if (!partial) {
      setFlowCustomerId(null);
      setMessage('مشتری پیدا نشد؛ کد، username، موبایل یا نام را بررسی کنید.');
      return;
    }
    selectFlowCustomer(partial);
  }

  function openCustomerProfile() {
    window.dispatchEvent(new CustomEvent('gamenet-navigate', { detail: 'customers' }));
    setModal(null);
  }

  async function startSession() {
    if (!canStartSession) { setMessage('دسترسی شروع جلسه ندارید'); return; }
    if (!activeStation || activeStation.state !== 'free') { setMessage('این ایستگاه دیگر آزاد نیست'); return; }
    const customer = findCustomer(customerCode);
    if (!customer) { setMessage('اول یوزر مشتری را وارد کنید و Enter بزنید.'); return; }
    if (!isServerGuid(customer.id) || !isServerGuid(activeStation.id)) {
      setMessage('این ایستگاه یا مشتری هنوز شناسهٔ معتبر Server ندارد.');
      return;
    }
    const sessionRate = activeStation.sessionRate ?? activeStation.ratePerHour;
    try {
      const result = await startServerSession({
        customerId: customer.id,
        stationId: activeStation.id,
        persons: activeStation.zone === 'pc' ? 1 : persons,
      });
            if (!result) {
         setMessage('پاسخ شروع جلسه از Server ناقص بود.');
         return;
       }
 const serverSessionId = result.sessionId;
      updateStation(activeStation.id, {
        state: 'busy',
        startedAt: result.startAt,
        sessionStartedAt: result.startAt,
        sessionMinutes: 0,
        sessionRate,
        amountSoFar: 0,
        customerCode: customer.code ?? customer.username,
        persons: activeStation.zone === 'pc' ? 1 : persons,
        serverSessionId,
      });
      setActiveStation(current => current ? { ...current, state: 'busy', sessionStartedAt: result.startAt, sessionRate, serverSessionId } : current);
      addSessionTimeline(activeStation.id, 'start', 'شروع جلسه روی سرور', customer.code + ' · ' + customer.name + ' · نرخ نهایی توسط Server تعیین می‌شود');
      setModal(null);
      setMessage('جلسه روی سرور ثبت شد؛ زمان و مبلغ نهایی از وضعیت Server پیروی می‌کنند.');
    } catch (error) {
      setMessage(userErrorMessage(error, 'شروع جلسه روی سرور انجام نشد'));
    }
  }


  function endSessionForPayment(station: StationDto) {
    if (!canSettleSession) { setMessage('دسترسی تسویه جلسه ندارید'); return; }
    if (!['busy', 'paused'].includes(station.state)) { setMessage('این ایستگاه جلسه فعالی ندارد'); return; }
    if (!station.serverSessionId || !isServerGuid(station.serverSessionId)) {
      setMessage('فقط جلسه‌ای که روی Server ثبت شده باشد قابل تسویه است.');
      return;
    }
    open('settle', station);
  }

  async function finishSplitSession(finalTotal: number) {
    if (!activeStation) return;

    const cash = number(splitCash);
    const card = number(splitCard);
    const wallet = number(splitWallet);
    const total = cash + card + wallet;

    if (total !== finalTotal) {
      setMessage('جمع پرداخت‌های ترکیبی باید دقیقاً برابر ' + money(finalTotal) + ' تومان باشد.');
      return;
    }

    const customer = customers.find(item =>
      item.code === activeStation.customerCode
      || item.username === activeStation.customerCode
      || item.id === activeStation.customerCode);

    if (wallet > 0 && (!customer || customer.wallet < wallet)) {
      setMessage('موجودی کیف پول برای سهم انتخاب‌شده کافی نیست.');
      return;
    }

    if (!activeStation.serverSessionId
      || !isServerGuid(activeStation.serverSessionId)
      || !customer
      || !isServerGuid(customer.id)) {
      setMessage('تسویهٔ ترکیبی فقط برای جلسهٔ معتبر Server قابل ثبت است.');
      return;
    }

    try {
      const parts = [
        ...(cash > 0 ? [{ method: 'cash' as const, amount: cash }] : []),
        ...(card > 0 ? [{ method: 'card' as const, amount: card }] : []),
        ...(wallet > 0 ? [{ method: 'wallet' as const, amount: wallet }] : []),
      ];

      const serverResult = await settleServerSession(
        activeStation.serverSessionId,
        finalTotal,
        parts);

      if (wallet > 0) {
        setCustomers(current => current.map(item =>
          item.id === customer.id
            ? { ...item, wallet: serverResult.walletBalanceAfter }
            : item));
      }

      addSessionTimeline(
        activeStation.id,
        'settle',
        'تسویه ترکیبی سروری',
        parts.map(item =>
          (item.method === 'cash'
            ? 'نقدی'
            : item.method === 'card'
              ? 'کارتخوان'
              : 'کیف پول') + ' ' + money(item.amount))
          .join(' · '),
        finalTotal,
        serverResult.invoiceId);

      updateStation(activeStation.id, {
        state: 'free',
        startedAt: undefined,
        sessionMinutes: undefined,
        sessionRate: undefined,
        amountSoFar: undefined,
        customerCode: undefined,
        persons: undefined,
        buffetTotal: undefined,
        sessionCredit: undefined,
        prepaidEndsAt: undefined,
        pausedAt: undefined,
        pausedMinutes: undefined,
        serverSessionId: undefined,
      });setSessionFollowUps(current =>
        current.map(item =>
          item.stationId === activeStation.id && item.status !== 'paid'
            ? { ...item, status: 'paid' }
            : item));

      setModal(null);
      setSessionCenterStation(null);
      setMessage('تسویه ترکیبی سروری با موفقیت ثبت شد.');
    } catch (error) {
      setMessage(userErrorMessage(error, 'تسویه ترکیبی سروری انجام نشد'));
    }
  }

  async function finishSession(method: string) {
    if (method === 'debt') {
      if (!canSettleSession) {
        setMessage('دسترسی تسویه جلسه ندارید');
        return;
      }
      if (!activeStation || !activeStation.serverSessionId || !isServerGuid(activeStation.serverSessionId)) {
        setMessage('جلسه سروری برای ثبت پرداخت بعداً لازم است.');
        return;
      }

      const customer = customers.find(item =>
        item.code === activeStation.customerCode ||
        item.username === activeStation.customerCode ||
        item.id === activeStation.customerCode);
      const freeTimeMinutes = Math.min(
        customer?.freeTimeMinutes ?? 0,
        Math.ceil(duration(activeStation)),
      );

      try {
        await settleServerSessionLater(activeStation.serverSessionId, freeTimeMinutes);
        updateStation(activeStation.id, {
          state: 'free',
          startedAt: undefined,
          sessionMinutes: undefined,
          sessionRate: undefined,
          amountSoFar: undefined,
          customerCode: undefined,
          persons: undefined,
          buffetTotal: undefined,
          sessionCredit: undefined,
          sessionPrepaidAmount: undefined,
          prepaidEndsAt: undefined,
          pausedAt: undefined,
          pausedMinutes: undefined,
          serverSessionId: undefined,
        });
        await refreshPendingSettlements();
        setModal(null);
        setSessionCenterStation(null);
        setMessage('جلسه بسته شد و حساب مشتری در «در انتظار پرداخت» ثبت شد.');
      } catch (error) {
        setMessage(userErrorMessage(error, 'ثبت پرداخت بعداً انجام نشد'));
      }
      return;
    }


    if (!canSettleSession) { setMessage('دسترسی تسویه جلسه ندارید'); return; }
    if (!activeStation) return;
    const elapsed = duration(activeStation);
    const customer = customers.find(item => item.code === activeStation.customerCode || item.username === activeStation.customerCode || item.id === activeStation.customerCode);
    const billing = calculateBilling({
      elapsedMinutes: elapsed,
      ratePerHour: activeStation.sessionRate ?? activeStation.ratePerHour,
      buffetAmount: activeStation.buffetTotal ?? 0,
      freeMinutes: customer?.freeTimeMinutes ?? 0,
      discountPercent,
      minimumCharge: 0,
      roundingStep: 0,
    });
    const prepaidUsed = Math.min(billing.finalAmount, activeStation.sessionCredit ?? 0);
    const finalTotal = Math.max(0, billing.finalAmount - prepaidUsed);
    if (activeStation.serverSessionId && customer && isServerGuid(activeStation.serverSessionId) && ['cash', 'card', 'wallet', 'gift'].includes(method)) {
      try {
        const serverResult = await settleServerSession(
          activeStation.serverSessionId,
          finalTotal,
          [{ method: method as 'cash' | 'card' | 'wallet' | 'gift', amount: finalTotal }],
          undefined,
          Math.min(customer.freeTimeMinutes ?? 0, Math.ceil(elapsed)),
          billing.timeAmount,
          billing.discountAmount,
          prepaidUsed,
        );
        setCustomers(current => current.map(item => item.id === customer.id
          ? {
              ...item,
              wallet: method === 'wallet' ? serverResult.walletBalanceAfter : item.wallet,
              giftCredit: serverResult.freeMoneyBalanceAfter,
              freeTimeMinutes: serverResult.freeTimeMinutesAfter,
              transactionHistory: [
                method === 'gift' ? 'مصرف اعتبار رایگان سرور · ' + money(finalTotal) + ' تومان' : method === 'wallet' ? 'تسویه سرور از کیف پول · ' + money(finalTotal) + ' تومان' : 'تسویه سرور · ' + money(finalTotal) + ' تومان',
                ...(item.transactionHistory ?? []),
              ],
            }
          : item));
        addSessionTimeline(activeStation.id, 'settle', 'تسویه سروری', money(finalTotal) + ' تومان · ' + (method === 'cash' ? 'نقدی' : method === 'card' ? 'کارتخوان' : method === 'wallet' ? 'کیف پول' : 'اعتبار رایگان'), finalTotal, serverResult.invoiceId);
        setSessionFollowUps(current => current.map(item => item.stationId === activeStation.id && item.status !== 'paid' ? { ...item, status: 'paid' } : item));
        updateStation(activeStation.id, {
          state: 'free', startedAt: undefined, sessionMinutes: undefined, sessionRate: undefined, amountSoFar: undefined,
          customerCode: undefined, persons: undefined, buffetTotal: undefined, sessionCredit: undefined,
          prepaidEndsAt: undefined, pausedAt: undefined, pausedMinutes: undefined, serverSessionId: undefined,
        });
        setModal(null);
        setSessionCenterStation(null);
        setMessage('تسویه سروری با موفقیت ثبت شد.');
        return;
      } catch (error) {
        setMessage(userErrorMessage(error, 'تسویه سروری انجام نشد'));
        return;
      }
    }

    setMessage('جلسهٔ غیرسروری قابل تسویه نیست؛ عملیات مالی فقط از مسیر Server انجام می‌شود.');
    return;
  }

  async function pauseSession(stationOverride?: StationDto) {
    if (!canManageSession) { setMessage('دسترسی مدیریت جلسه ندارید'); return; }
    const station = stationOverride ?? activeStation;
    if (!station || station.state !== 'busy') { setMessage('فقط جلسه در حال بازی قابل توقف است'); return; }
    try {
      if (station.serverSessionId) await pauseServerSession(station.serverSessionId);
      const pausedAt = new Date().toISOString();
      updateStation(station.id, { state: 'paused', pausedAt, sessionPausedAt: pausedAt });
      addSessionTimeline(station.id, 'pause', 'توقف جلسه', 'جلسه موقتاً متوقف شد');
      setModal(null);
      setMessage(station.serverSessionId ? 'جلسه روی سرور متوقف شد.' : 'جلسه متوقف موقت شد؛ زمان صورتحساب جلو نمی‌رود');
    } catch (error) {
      setMessage(userErrorMessage(error, 'توقف جلسه روی سرور ثبت نشد'));
    }
  }

  async function resumeSession(stationOverride?: StationDto) {
    if (!canManageSession) { setMessage('دسترسی مدیریت جلسه ندارید'); return; }
    const station = stationOverride ?? activeStation;
    const pausedAt = station?.pausedAt ?? station?.sessionPausedAt;
    if (!station || station.state !== 'paused' || !pausedAt) { setMessage('جلسه متوقفی برای ادامه وجود ندارد'); return; }
    const currentPaused = (Date.now() - new Date(pausedAt).getTime()) / 60000;
    try {
      if (station.serverSessionId) await resumeServerSession(station.serverSessionId);
      updateStation(station.id, {
        state: 'busy',
        pausedAt: undefined,
        sessionPausedAt: undefined,
        pausedMinutes: (station.pausedMinutes ?? station.sessionPausedMinutes ?? 0) + Math.max(0, currentPaused),
      });
      addSessionTimeline(station.id, 'resume', 'ادامه جلسه', 'توقف ' + money(currentPaused) + ' دقیقه محاسبه شد');
      setMessage(station.serverSessionId ? 'جلسه روی سرور ادامه پیدا کرد.' : 'جلسه ادامه پیدا کرد');
    } catch (error) {
      setMessage(userErrorMessage(error, 'ادامه جلسه روی سرور ثبت نشد'));
    }
  }

  async function completeReduce() {
    if (!canManageSession) { setMessage('دسترسی مدیریت جلسه ندارید'); return; }
    if (!activeStation || !['busy', 'paused'].includes(activeStation.state)) { setMessage('فقط جلسه فعال یا متوقف قابل کاهش زمان است'); return; }
    const minutes = reduceMinutes === -1 ? Math.max(1, Number(customReduceMinutes.replace(/[۰-۹]/g, digit => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(digit)))) || 0) : reduceMinutes;
    const current = duration(activeStation);
    if (!minutes || minutes >= current) { setMessage('زمان کاهش باید کمتر از زمان استفاده‌شده باشد'); return; }
    try {
      if (activeStation.serverSessionId) await adjustServerSessionTime(activeStation.serverSessionId, -minutes);
      const currentStartedAt = activeStation.startedAt ?? activeStation.sessionStartedAt ?? new Date().toISOString();
      const nextStartedAt = new Date(new Date(currentStartedAt).getTime() + minutes * 60000).toISOString();
      updateStation(activeStation.id, {
        startedAt: nextStartedAt,
        sessionStartedAt: nextStartedAt,
        sessionMinutes: Math.max(0, current - minutes),
        sessionTimeAdjustmentMinutes: (activeStation.sessionTimeAdjustmentMinutes ?? 0) - minutes,
      });
      addSessionTimeline(activeStation.id, 'reduce', 'کاهش زمان', money(minutes) + ' دقیقه از زمان صورتحساب کم شد', minutes);
      setModal(null);
      setMessage(money(minutes) + ' دقیقه از زمان قابل صورتحساب کم شد');
    } catch (error) {
      setMessage(userErrorMessage(error, 'کاهش زمان روی سرور ثبت نشد'));
    }
  }

  async function completeExtend() {
    if (!canManageSession) { setMessage('دسترسی مدیریت جلسه ندارید'); return; }
    if (!activeStation || activeStation.state !== 'busy') {
      setMessage('جلسه فعالی برای تمدید وجود ندارد');
      setModal(null);
      return;
    }
    const minutes = extendMinutes === -1
      ? Math.max(1, Number(customExtendMinutes.replace(/[۰-۹]/g, digit => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(digit)))) || 0)
      : extendMinutes;
    if (!minutes) {
      setMessage('مدت تمدید معتبر نیست');
      return;
    }
    try {
      if (activeStation.serverSessionId) await adjustServerSessionTime(activeStation.serverSessionId, minutes);
      const currentStartedAt = activeStation.startedAt ?? activeStation.sessionStartedAt ?? new Date().toISOString();
      const nextStartedAt = new Date(new Date(currentStartedAt).getTime() - minutes * 60000).toISOString();
      updateStation(activeStation.id, {
        sessionMinutes: duration(activeStation) + minutes,
        startedAt: nextStartedAt,
        sessionStartedAt: nextStartedAt,
        sessionTimeAdjustmentMinutes: (activeStation.sessionTimeAdjustmentMinutes ?? 0) + minutes,
      });
      addSessionTimeline(activeStation.id, 'extend', 'تمدید جلسه', money(minutes) + ' دقیقه به جلسه اضافه شد', minutes);
      setModal(null);
      setMessage(money(minutes) + ' دقیقه به جلسه ' + activeStation.name + ' اضافه شد');
    } catch (error) {
      setMessage(userErrorMessage(error, 'تمدید جلسه روی سرور ثبت نشد'));
    }
  }

  async function changeSessionRate(station: StationDto, rate: number) {
    if (!canManageSession) { setMessage('دسترسی مدیریت جلسه ندارید'); return; }
    if (!rate || rate <= 0) { setMessage('نرخ جلسه باید بیشتر از صفر باشد'); return; }
    const nextRate = Math.round(rate);
    try {
      if (station.serverSessionId) {
        await updateServerSessionDetails(station.serverSessionId, { hourlyRate: nextRate });
      }
      updateStation(station.id, { sessionRate: nextRate });
      addSessionTimeline(station.id, 'note', 'تغییر نرخ جلسه', 'نرخ این جلسه به ' + money(nextRate) + ' تومان/ساعت تغییر کرد', nextRate);
      setMessage(station.serverSessionId ? 'نرخ جلسه روی سرور هم ثبت شد.' : 'نرخ جلسه به ' + money(nextRate) + ' تومان/ساعت تغییر کرد');
    } catch (error) {
      setMessage(userErrorMessage(error, 'تغییر نرخ جلسه ثبت نشد'));
    }
  }

  async function changeSessionPersons(station: StationDto, persons: number) {
    if (!canManageSession) { setMessage('دسترسی مدیریت جلسه ندارید'); return; }
    const next = station.zone === 'pc' ? 1 : Math.max(1, Math.min(4, persons));
    try {
      if (station.serverSessionId) {
        await updateServerSessionDetails(station.serverSessionId, { persons: next });
      }
      updateStation(station.id, { persons: next });
      addSessionTimeline(station.id, 'note', 'تغییر نفرات', 'تعداد نفرات به ' + money(next) + ' نفر تغییر کرد', next);
      setMessage(station.serverSessionId ? 'تعداد نفرات روی سرور هم ثبت شد.' : 'تعداد نفرات جلسه به ' + money(next) + ' نفر تغییر کرد');
    } catch (error) {
      setMessage(userErrorMessage(error, 'تغییر تعداد نفرات ثبت نشد'));
    }
  }

  async function transferSession(station: StationDto, targetId: string) {
    if (!canManageSession) { setMessage('دسترسی انتقال جلسه ندارید'); return; }
    const target = stations.find(item => item.id === targetId);
    if (!target || target.state !== 'free') { setMessage('ایستگاه مقصد دیگر آزاد نیست'); return; }
    try {
      if (station.serverSessionId) {
        await transferServerSession(station.serverSessionId, target.id);
      }
      updateStation(station.id, {
        state: 'free',
        startedAt: undefined,
        sessionMinutes: undefined,
        sessionRate: undefined,
        amountSoFar: undefined,
        customerCode: undefined,
        persons: undefined,
        buffetTotal: undefined,
        sessionCredit: undefined,
        prepaidEndsAt: undefined,
        pausedAt: undefined,
        pausedMinutes: undefined,
        serverSessionId: undefined,
      });
      updateStation(target.id, {
        state: station.state,
        startedAt: station.startedAt,
        sessionMinutes: station.sessionMinutes,
        sessionRate: station.sessionRate,
        amountSoFar: station.amountSoFar,
        customerCode: station.customerCode,
        persons: target.zone === 'pc' ? 1 : (station.persons ?? 1),
        buffetTotal: station.buffetTotal,
        sessionCredit: station.sessionCredit,
        prepaidEndsAt: station.prepaidEndsAt,
        pausedAt: station.pausedAt,
        pausedMinutes: station.pausedMinutes,
        network: target.network,
        serverSessionId: station.serverSessionId,
      });
      addSessionTimeline(station.id, 'note', 'انتقال جلسه', 'جلسه از ' + station.name + ' به ' + target.name + ' منتقل شد');
      addSessionTimeline(target.id, 'note', 'دریافت جلسه', 'جلسه از ' + station.name + ' منتقل شد');
      setSessionCenterStation({ ...target, state: station.state, serverSessionId: station.serverSessionId });
      setActiveStation({ ...target, state: station.state, serverSessionId: station.serverSessionId });
      setMessage(station.serverSessionId ? 'جلسه روی سرور به ' + target.name + ' منتقل شد.' : 'جلسه به ' + target.name + ' منتقل شد');
    } catch (error) {
      setMessage(userErrorMessage(error, 'انتقال جلسه ثبت نشد'));
    }
  }

  async function applyCharge(method: string) {
    const value = number(amount);
    if (!value || !activeStation) {
      setMessage('مبلغ معتبر وارد کنید');
      return;
    }

    if (chargeTarget === 'session') {
      if (!['cash', 'card', 'wallet'].includes(method)) {
        setMessage('شارژ زمان فقط از مسیر نقدی، کارتخوان یا کیف پول ثبت می‌شود.');
        return;
      }
      if (!hasPermission(user, 'session.manage')) {
        setMessage('دسترسی مدیریت مالی جلسه را ندارید');
        return;
      }
      if (!activeStation.serverSessionId || !isServerGuid(activeStation.serverSessionId)) {
        setMessage('جلسه باید روی Server ثبت شده باشد.');
        return;
      }

      try {
        const result = await chargeServerSession(
          activeStation.serverSessionId,
          value,
          method as 'cash' | 'card' | 'wallet',
        );

        updateStation(activeStation.id, {
          sessionCredit: result.prepaidTotal,
          sessionPrepaidAmount: result.prepaidTotal,
          prepaidEndsAt: result.sessionEndAt ?? undefined,
        });

        if (method === 'wallet') {
          const customer = customers.find(item =>
            item.code === activeStation.customerCode ||
            item.username === activeStation.customerCode ||
            item.id === activeStation.customerCode);
          if (customer) {
            setCustomers(current => current.map(item =>
              item.id === customer.id
                ? { ...item, wallet: result.walletBalanceAfter }
                : item));
          }
        }

        addSessionTimeline(
          activeStation.id,
          'charge',
          'شارژ جلسه',
          money(value) + ' تومان شارژ شد',
          value,
          result.invoiceId,
        );
        await refreshPendingSettlements();
        setModal(null);
        setMessage(money(value) + ' تومان شارژ شد؛ حساب مالی مشتری روی Server به‌روز شد.');
      } catch (error) {
        setMessage(userErrorMessage(error, 'ثبت شارژ انجام نشد'));
      }
      return;
    }

    if (chargeTarget === 'wallet' || chargeTarget === 'discount') {
      if (!hasPermission(user, 'customer.wallet')) {
        setMessage('دسترسی مدیریت کیف پول مشتری را ندارید');
        return;
      }
      const customer = customers.find(item =>
        item.code === activeStation.customerCode ||
        item.username === activeStation.customerCode ||
        item.id === activeStation.customerCode);
      if (!customer) {
        setMessage('جلسه به مشتری وصل نیست');
        return;
      }
      if (method === 'wallet') {
        setMessage('برای شارژ کیف پول، نقد یا کارت را انتخاب کنید');
        return;
      }

      try {
        const entry = await recordWalletTransaction(customer.id, {
          amount: value,
          type: 'credit',
          description: chargeTarget === 'discount' ? 'شارژ + تخفیف' : 'شارژ کیف پول',
        });
        setCustomers(current => current.map(item =>
          item.id === customer.id
            ? {
                ...item,
                wallet: entry.balanceAfter,
                discountLevel: chargeTarget === 'discount' ? item.discountLevel + 1 : item.discountLevel,
                transactionHistory: [
                  (chargeTarget === 'discount' ? 'شارژ + تخفیف' : 'شارژ کیف پول') + ' · ' + money(value) + ' تومان',
                  ...(item.transactionHistory ?? []),
                ],
              }
            : item));
        setModal(null);
        setMessage(chargeTarget === 'discount' ? 'شارژ + تخفیف ثبت شد' : 'کیف پول شارژ شد');
      } catch (error) {
        setMessage(userErrorMessage(error, 'ثبت شارژ کیف پول انجام نشد'));
      }
      return;
    }

    setMessage('هدف شارژ معتبر نیست');
  }

  async function reverseTimelineEvent(event: SessionTimelineEvent) {
    if (event.serverReferenceId) {
      if (!hasPermission(user, 'finance.manage')) {
        setMessage('دسترسی ثبت درخواست برگشت فاکتور را ندارید');
        return;
      }

      try {
        const result = await requestServerInvoiceReverseApproval(
          event.serverReferenceId,
          'برگشت عملیات از تایم‌لاین جلسه: ' + event.title,
        );
        setReverseRequest(null);
        setMessage(result.status === 'Pending'
          ? 'درخواست برگشت فاکتور ثبت شد و برای تأیید مسئول مجاز ارسال شد.'
          : 'درخواست برگشت فاکتور روی سرور ثبت شد.');
        return;
      } catch (error) {
        setMessage(userErrorMessage(error, 'درخواست برگشت عملیات روی سرور ثبت نشد'));
        return;
      }
    }

    const station = stations.find(item => item.id === event.stationId);
    if (!station || event.amount === undefined) {
      setMessage('این عملیات در وضعیت فعلی قابل برگشت نیست');
      return;
    }

    if (event.kind === 'buffet') {
      updateStation(station.id, { buffetTotal: Math.max(0, (station.buffetTotal ?? 0) - event.amount) });
    } else if (event.kind === 'charge') {
      const rate = station.sessionRate ?? station.ratePerHour;
      const minutes = rate > 0 ? event.amount / (rate / 60) : 0;
      const currentDuration = duration(station);
      const nextDuration = Math.max(0, currentDuration - minutes);
      const currentEnd = station.prepaidEndsAt ? new Date(station.prepaidEndsAt).getTime() : 0;
      updateStation(station.id, {
        sessionCredit: Math.max(0, (station.sessionCredit ?? 0) - event.amount),
        sessionMinutes: nextDuration,
        startedAt: new Date(Date.now() - nextDuration * 60000).toISOString(),
        prepaidEndsAt: currentEnd ? new Date(currentEnd - minutes * 60000).toISOString() : undefined,
      });
    } else if (event.kind === 'extend') {
      const currentDuration = duration(station);
      const minutes = event.amount;
      updateStation(station.id, {
        sessionMinutes: Math.max(0, currentDuration - minutes),
        startedAt: new Date(Date.now() - Math.max(0, currentDuration - minutes) * 60000).toISOString(),
      });
    } else if (event.kind === 'reduce') {
      const currentDuration = duration(station);
      const minutes = event.amount;
      updateStation(station.id, {
        sessionMinutes: currentDuration + minutes,
        startedAt: new Date(Date.now() - (currentDuration + minutes) * 60000).toISOString(),
      });
    }

    setReversedEventIds(current => [...current, event.id]);
    addSessionTimeline(
      event.stationId,
      'note',
      'برگشت عملیات',
      'عملیات «' + event.title + '» معکوس شد؛ رکورد اصلی حذف نشده است.',
      event.amount,
    );
    setReverseRequest(null);
    setMessage('برگشت عملیات ثبت شد');
  }

  function openCustomerFlowForStation(station: StationDto) {
    const customer = customers.find(item =>
      item.username === station.customerUsername ||
      item.code === station.customerCode ||
      item.id === station.customerCode);
    setActiveStation(station);
    setFlowCustomerId(customer?.id ?? null);
    setCustomerCode(customer ? (customer.username || customer.code || customer.mobile || customer.id) : station.customerUsername || station.customerCode || '');
    setAmount('');
    setFlowStep(customer ? 2 : 1);
    setModal('flow');
  }

  function openSessionCenter(station: StationDto) {
    if (!['busy', 'paused'].includes(station.state)) {
      setMessage('این ایستگاه جلسه فعالی ندارد');
      return;
    }
    setSessionCenterStation(station);
    setContext(null);
  }

  function openContextAt(x: number, y: number, station: StationDto) {
    const width = 300;
    const height = Math.min(620, window.innerHeight - 16);
    setContext({ x: Math.max(8, Math.min(x, window.innerWidth - width - 8)), y: Math.max(8, Math.min(y, window.innerHeight - height - 8)), station });
  }

  function showContext(event: MouseEvent<HTMLElement>, station: StationDto) {
    event.preventDefault();
    openContextAt(event.clientX, event.clientY, station);
  }
  async function setAgentLock(station: StationDto, locked: boolean) {
    if (!canControlClient) { setMessage('دسترسی کنترل کلاینت ندارید'); return; }
    if (!station.agentId || station.agentOnline !== true) {
      setMessage('Agent این دستگاه آنلاین نیست؛ فرمان ارسال نشد.');
      return;
    }

    try {
      setMessage(locked ? 'درخواست قفل دستگاه ارسال شد…' : 'درخواست بازگشایی دستگاه ارسال شد…');
      const command = await sendAgentCommand(station.agentId, locked ? 'lock' : 'unlock');

      for (let attempt = 0; attempt < 20; attempt += 1) {
        const status = await getAgentCommand(command.commandId);
        if (status.status === 'Succeeded') {
          setMessage(locked ? 'دستگاه قفل شد.' : 'دستگاه باز شد.');
          return;
        }
        if (status.status === 'Failed') {
          setMessage(status.resultMessage || 'اجرای فرمان Agent ناموفق بود.');
          return;
        }
        await new Promise(resolve => window.setTimeout(resolve, 500));
      }

      setMessage('Agent به فرمان پاسخ نداد؛ وضعیت دستگاه را بررسی کنید.');
    } catch (error) {
      setMessage(userErrorMessage(error, locked ? 'قفل کردن دستگاه انجام نشد' : 'بازگشایی دستگاه انجام نشد'));
    }
  }

  async function setAgentLogoutLock(station: StationDto) {
    if (!canControlClient || !station.agentId || station.agentOnline !== true) {
      setMessage('Agent این دستگاه آنلاین نیست؛ فرمان ارسال نشد.');
      return;
    }

    try {
      setMessage('درخواست خروج کاربر و قفل دستگاه ارسال شد…');
      const command = await sendAgentCommand(station.agentId, 'logout-lock');
      for (let attempt = 0; attempt < 20; attempt += 1) {
        const status = await getAgentCommand(command.commandId);
        if (status.status === 'Succeeded') {
          setMessage('کاربر خارج شد و دستگاه قفل شد.');
          return;
        }
        if (status.status === 'Failed') {
          setMessage(status.resultMessage || 'خروج و قفل دستگاه ناموفق بود.');
          return;
        }
        await new Promise(resolve => window.setTimeout(resolve, 500));
      }
      setMessage('Agent به فرمان خروج و قفل پاسخ نداد.');
    } catch (error) {
      setMessage(userErrorMessage(error, 'خروج کاربر و قفل دستگاه انجام نشد'));
    }
  }

  async function setAgentUpdate(station: StationDto) {
    if (!canControlClient || !station.agentId || station.agentOnline !== true) {
      setMessage('Agent این دستگاه آنلاین نیست؛ Update ارسال نشد.');
      return;
    }

    try {
      setMessage('درخواست به‌روزرسانی Client ارسال شد…');
      const command = await requestAgentUpdate(station.agentId);
      for (let attempt = 0; attempt < 30; attempt += 1) {
        const status = await getAgentCommand(command.commandId);
        if (status.status === 'Succeeded') {
          setMessage('درخواست Update اجرا شد؛ Client در حال راه‌اندازی نسخه جدید است.');
          return;
        }
        if (status.status === 'Failed') {
          setMessage(status.resultMessage || 'به‌روزرسانی Client ناموفق بود.');
          return;
        }
        await new Promise(resolve => window.setTimeout(resolve, 500));
      }
      setMessage('Agent به درخواست Update پاسخ نداد؛ وضعیت Client را بررسی کنید.');
    } catch (error) {
      setMessage(userErrorMessage(error, 'درخواست به‌روزرسانی Client انجام نشد'));
    }
  }

  async function setAgentRollback(station: StationDto) {
    if (!canControlClient || !station.agentId || station.agentOnline !== true) {
      setMessage('Agent این دستگاه آنلاین نیست؛ Rollback ارسال نشد.');
      return;
    }

    try {
      setMessage('درخواست Rollback Client ارسال شد…');
      const command = await requestAgentRollback(station.agentId);
      for (let attempt = 0; attempt < 30; attempt += 1) {
        const status = await getAgentCommand(command.commandId);
        if (status.status === 'Succeeded') {
          setMessage('Rollback اجرا شد؛ Client در حال بازیابی نسخه قبلی است.');
          return;
        }
        if (status.status === 'Failed') {
          setMessage(status.resultMessage || 'Rollback Client ناموفق بود.');
          return;
        }
        await new Promise(resolve => window.setTimeout(resolve, 500));
      }
      setMessage('Agent به درخواست Rollback پاسخ نداد؛ وضعیت Client را بررسی کنید.');
    } catch (error) {
      setMessage(userErrorMessage(error, 'درخواست Rollback Client انجام نشد'));
    }
  }

  async function runDirectAgentCommand(
    station: StationDto,
    commandType: 'ping' | 'restart' | 'shutdown',
    pendingMessage: string,
    successMessage: string,
  ) {
    const needsPower = commandType === 'restart' || commandType === 'shutdown';
    if ((needsPower && !canPowerClient) || (!needsPower && !canControlClient)) {
      setMessage(needsPower ? 'دسترسی روشن/خاموش کردن کلاینت را ندارید.' : 'دسترسی کنترل کلاینت را ندارید.');
      return;
    }
    if (!station.agentId || station.agentOnline !== true) {
      setMessage('Agent این دستگاه آنلاین نیست؛ فرمان ارسال نشد.');
      return;
    }

    try {
      setMessage(pendingMessage);
      const command = await sendAgentCommand(station.agentId, commandType);
      if (needsPower) {
        setMessage(successMessage);
        return;
      }
      for (let attempt = 0; attempt < 20; attempt += 1) {
        const status = await getAgentCommand(command.commandId);
        if (status.status === 'Succeeded') {
          setMessage(status.resultMessage || successMessage);
          return;
        }
        if (status.status === 'Failed') {
          setMessage(status.resultMessage || successMessage + ' ناموفق بود.');
          return;
        }
        await new Promise(resolve => window.setTimeout(resolve, 500));
      }
      setMessage('Agent به فرمان پاسخ نداد؛ وضعیت دستگاه را بررسی کنید.');
    } catch (error) {
      setMessage(userErrorMessage(error, successMessage + ' انجام نشد'));
    }
  }

  async function setAgentKioskPolicy(station: StationDto, enabled: boolean) {
    if (!canControlClient || !station.agentId || station.agentOnline !== true) {
      setMessage('Agent این دستگاه آنلاین نیست؛ Policy تغییر نکرد.');
      return;
    }

    try {
      const policy = await updateAgentPolicy(station.agentId, {
        kioskEnabled: enabled,
        lockOnDisconnect: enabled,
      });
      updateStation(station.id, {
        agentKioskEnabled: policy.kioskEnabled,
        agentLockOnDisconnect: policy.lockOnDisconnect,
      });
      setMessage(enabled ? 'حالت Kiosk برای این Agent فعال شد.' : 'حالت Kiosk برای این Agent غیرفعال شد.');
    } catch (error) {
      setMessage(userErrorMessage(error, 'تنظیم Policy Kiosk انجام نشد'));
    }
  }

  function contextAction(action: string) {
    const station = context?.station;
    setContext(null);
    if (!station) return;
    if (action === 'details') { openSessionCenter(station); return; }
    if (action === 'settle') { endSessionForPayment(station); return; }
    if (action === 'extend') { open('extend', station); return; }
    if (action === 'pause') { pauseSession(station); return; }
    if (action === 'resume') { resumeSession(station); return; }
    if (action === 'reduce') { open('reduce', station); return; }
    if (action === 'lock') { void setAgentLock(station, true); return; }
    if (action === 'unlock') { void setAgentLock(station, false); return; }
    if (action === 'logout-lock') { void setAgentLogoutLock(station); return; }
    if (action === 'kiosk-toggle') { void setAgentKioskPolicy(station, !Boolean(station.agentKioskEnabled)); return; }
    if (action === 'agent-update') { void setAgentUpdate(station); return; }
    if (action === 'agent-rollback') { void setAgentRollback(station); return; }
    if (action === 'agent-ping') { void runDirectAgentCommand(station, 'ping', 'در حال بررسی ارتباط Agent…', 'ارتباط Agent سالم است.'); return; }
    if (action === 'agent-restart') {
      if (!window.confirm('Client این دستگاه Restart شود؟')) return;
      void runDirectAgentCommand(station, 'restart', 'درخواست راه‌اندازی مجدد Client ارسال شد…', 'Restart Client با موفقیت درخواست شد.');
      return;
    }
    if (action === 'agent-shutdown') {
      if (!window.confirm('Client این دستگاه خاموش شود؟')) return;
      void runDirectAgentCommand(station, 'shutdown', 'درخواست خاموش کردن Client ارسال شد…', 'خاموش کردن Client با موفقیت درخواست شد.');
      return;
    }
    setMessage('فرمان پشتیبانی‌نشده درخواست شد.');
  }

  function stationSupportsAgentLock(station: StationDto) {
    return station.zone === 'pc' && Boolean(station.agentId);
  }

  function stationSupportsAgentPower(station: StationDto) {
    return station.zone === 'pc' && Boolean(station.agentId) && station.agentOnline === true;
  }

  function renderStation(station: StationDto) {
    const minutes = duration(station);
    const style = { '--zoom': zoom / 100 } as CSSProperties;
    const selected = selectedStationIds.includes(station.id);
    return <article
      id={`station-${station.id}`}
      data-station-id={station.id}
      key={station.id}
      style={style}
      className={`station-card ${station.state} ${view} ${selected ? 'selected' : ''}`}
      draggable={false}
      onMouseDown={event => {
        if (event.button !== 0 || (event.target instanceof Element && event.target.closest('button, input, select, textarea, a'))) return;
        const ctrlKey = event.ctrlKey || event.metaKey;
        const shiftKey = event.shiftKey;
        event.preventDefault();
        event.stopPropagation();
        window.getSelection()?.removeAllRanges();

        if (ctrlKey || shiftKey) {
          selectStationWithModifiers(station.id, ctrlKey, shiftKey);
          suppressNextStationClickRef.current = true;
        } else {
          suppressNextStationClickRef.current = false;
        }

        selectionDragRef.current = {
          stationId: station.id,
          startX: event.clientX,
          startY: event.clientY,
          dragging: false,
          ctrlKey,
          shiftKey,
        };
        selectionRectRef.current = null;
      }}
      onClick={event => {
        event.stopPropagation();
        if (suppressNextStationClickRef.current) {
          suppressNextStationClickRef.current = false;
          window.getSelection()?.removeAllRanges();
          return;
        }
        if (event.ctrlKey || event.metaKey || event.shiftKey) {
          event.preventDefault();
          selectStationWithModifiers(station.id, event.ctrlKey || event.metaKey, event.shiftKey);
          window.getSelection()?.removeAllRanges();
          return;
        }
        if (station.state === 'free') open('start', station);
        else if (station.state === 'busy' || station.state === 'paused') openSessionCenter(station);
        else setMessage(station.state === 'reserved' ? 'رزرو ساعت ۱۸:۰۰ — هنوز مشتری وارد نشده' : station.outOfServiceReason ?? 'این دستگاه خارج از سرویس است');
      }}
      onDragStart={event => event.preventDefault()}
      onSelect={event => event.preventDefault()}
      onDoubleClick={event => {
        event.preventDefault();
        if (station.state === 'busy' || station.state === 'paused') openCustomerFlowForStation(station);
      }}
      onContextMenu={event => showContext(event, station)}
    >
      <div className="top"><div className="name">{station.name}</div><span className={`status-badge ${station.state}`}>{stateLabels[station.state as StationState] ?? station.state}</span></div>
      {(() => {
        const customer = customers.find(item => item.username === station.customerCode || item.code === station.customerCode);
        const remaining = station.prepaidEndsAt
          ? Math.max(0, Math.ceil((new Date(station.prepaidEndsAt).getTime() - now) / 60000))
          : station.remainingMinutes;
        const username = station.customerUsername || station.customerCode || customer?.username;
        const customerName = station.customerFullName || customer?.name;
        const customerDebt = station.customerDebt ?? customer?.debt ?? 0;
        const customerNote = station.customerNote || customer?.notes || customer?.alias || '—';
        return <div className="station-customer-summary">
          <div className="station-customer-line"><strong>{username || 'مهمان'}</strong><span>{customerName || 'بدون مشتری ثبت‌شده'}</span></div>
          {(customer || station.customerUsername) && <div className="station-customer-line secondary"><span>بدهی: {money(customerDebt)} تومان</span><span>{customerNote.split(/\s+/).slice(0, 3).join(' ')}</span></div>}
          {station.state === 'busy' && <div className="station-remaining">{remaining == null ? 'جلسه باز' : remaining <= 0 ? 'زمان تمام‌شده' : 'باقی‌مانده: ' + money(remaining) + ' دقیقه'}</div>}
        </div>;
      })()}
      <span className="type">
        {station.type} · اینترنت {station.network ?? 1}
        {station.zone === 'pc' && station.agentOnline !== undefined && (
          <span
            className={`agent-state ${station.agentOnline ? 'online' : 'offline'}`}
            title={station.agentLastSeenAt ? `آخرین ارتباط Agent: ${new Date(station.agentLastSeenAt).toLocaleTimeString('fa-IR')}` : 'Agent هنوز heartbeat معتبر ندارد'}
          >
            · Agent {station.agentOnline ? 'متصل' : 'آفلاین'}{station.agentLocked ? ' · قفل' : ''}
          </span>
        )}
        {station.zone === 'pc' && station.agentLifecycleState && (
          <span
            className="agent-state lifecycle"
            title={station.agentLastUpdateError || (station.agentLastHealthyAt ? `آخرین سلامت Client: ${new Date(station.agentLastHealthyAt).toLocaleTimeString('fa-IR')}` : 'وضعیت Lifecycle ثبت نشده')}
          >
            {' · ' + (
              station.agentLifecycleState === 'Running' ? 'Client سالم' :
              station.agentLifecycleState === 'Updating' ? 'در حال به‌روزرسانی' :
              station.agentLifecycleState === 'UpdatePending' ? 'در انتظار به‌روزرسانی' :
              station.agentLifecycleState === 'Recovering' ? 'در حال بازیابی' :
              station.agentLifecycleState === 'Degraded' ? 'Client نیازمند بررسی' :
              station.agentLifecycleState === 'Failed' ? 'Client خطا دارد' :
              'در حال راه‌اندازی'
            )}
            {station.agentPendingUpdateVersion ? ` · نسخه ${station.agentPendingUpdateVersion}` : ''}
          </span>
        )}
      </span>
      <div className="time">{station.state === 'busy' ? `${money(Math.floor(minutes / 60)).padStart(2, '۰')}:${money(Math.floor(minutes % 60)).padStart(2, '۰')}` : station.state === 'reserved' ? 'رزرو' : station.state === 'off' ? '⛔' : '--:--'}</div>
      {station.state === 'busy' && <><div className="person-dots">{'● '.repeat(station.persons ?? 1)}</div><div className="progress-bar"><span style={{ width: `${Math.min(100, minutes % 60 / 60 * 100)}%` }} /></div><span className="pulse" /></>}
      {station.state === 'off' && <small>{station.outOfServiceReason ?? 'در تعمیر'}</small>}
      <div className="station-hover-actions" draggable={false} onMouseDown={event => { event.stopPropagation(); window.getSelection()?.removeAllRanges(); }} onClick={event => event.stopPropagation()} onDoubleClick={event => event.stopPropagation()}>
        {canStartSession && station.state === 'free' && <button type="button" className="quick primary" onClick={() => open('start', station)}>▶ شروع</button>}
        {canManageSession && station.state === 'busy' && <button type="button" className="quick" onClick={() => { setActiveStation(station); pauseSession(); }}>⏸ مکث</button>}
        {canManageSession && station.state === 'paused' && <button type="button" className="quick primary" onClick={() => { setActiveStation(station); resumeSession(); }}>▶ ادامه</button>}
        {canSettleSession && (station.state === 'busy' || station.state === 'paused') && <button type="button" className="quick" onClick={() => endSessionForPayment(station)}>🧾 پایان بازی</button>}
        {canManageSession && (station.state === 'busy' || station.state === 'paused') && <button type="button" className="quick" onClick={() => open('extend', station)}>⏱ تمدید</button>}
        <button
          type="button"
          className="quick more"
          aria-label={`عملیات بیشتر برای ${station.name}`}
          onClick={event => {
            const rect = event.currentTarget.getBoundingClientRect();
            openContextAt(rect.left + rect.width / 2, rect.bottom + 6, station);
          }}
        >•••</button>
      </div>
    </article>;
  }

  const groups: Array<[ZoneKey, string]> = [['pc', 'رایانه‌ها — ۴۰ دستگاه'], ['console', 'کنسول‌ها — PS5 / PS4'], ['table', 'میزها — فوتبال‌دستی']];
  return <>
    <div className="page-header"><div><p>وضعیت زنده · {serverInfo?.name ?? 'GameNet Manager'}</p><h1>داشبورد</h1></div><div className="page-meta"><span>{apiState === 'online' ? 'API متصل' : apiState === 'loading' ? 'در حال اتصال' : 'API قطع'}</span><span>{snapshot ? `آخرین دریافت ${new Date(snapshot.generatedAt).toLocaleTimeString('fa-IR')}` : 'در انتظار داده'}</span></div></div>
    <div className="summary-grid">
      {[
        ["ایستگاه آزاد", counts.free, 'green'],
        ["در حال جلسه", counts.busy + stations.filter(item => item.state === 'paused').length, 'red'],
        ["رزرو امروز", counts.reserved, 'blue'],
        ["مشتری حاضر", stations.filter(item => item.state === 'busy').reduce((sum, item) => sum + (item.persons ?? 1), 0), 'blue']
      ].map(([label, value, color]) => (
        <div key={label} className="summary-card">
          <div className="label">{label}</div>
          <div className={`value ${color}`}>{money(Number(value))}</div>
        </div>
      ))}
    </div>
        <div
      className="dashboard-workspace"
      onDragStart={event => event.preventDefault()}
      onSelect={event => {
        const target = event.target as HTMLElement;
        if (!target.closest('input, textarea, select')) event.preventDefault();
      }}
    >
      <DashboardAttentionSidebar
        payments={pendingSettlements}
        attentions={sidebarAttentions}
        recentActions={sidebarRecentActions}
        money={money}
        onMarkDebt={async invoiceId => {
          try {
            const result = await markPendingSettlementAsDebt(invoiceId);
            await refreshPendingSettlements();
            const refreshed = await getServerCustomers();
            setCustomers(refreshed);
            setMessage('حساب ' + result.customerName + ' با مبلغ ' + money(result.amountDue) + ' تومان به بدهی منتقل شد.');
          } catch (error) {
            setMessage(userErrorMessage(error, 'انتقال حساب به بدهی انجام نشد'));
          }
        }}
        onPay={async (invoiceId, method) => {
          const account = pendingSettlements.find(item => item.invoiceId === invoiceId);
          if (!account) {
            await refreshPendingSettlements().catch(() => undefined);
            setMessage('حساب باز پیدا نشد؛ فهرست به‌روزرسانی شد.');
            return;
          }

          try {
            const amountDue = Math.max(0, account.amountDue);
            const result = await settlePendingSettlement(invoiceId, {
              totalAmount: amountDue,
              parts: amountDue > 0 ? [{ method, amount: amountDue }] : [],
            });

            if (method === 'wallet') {
              setCustomers(current => current.map(item =>
                item.id === account.customerId
                  ? { ...item, wallet: result.walletBalanceAfter }
                  : item));
            }

            await refreshPendingSettlements();
            setMessage(amountDue > 0
              ? 'حساب ' + account.customerName + ' تسویه شد.'
              : 'حساب ' + account.customerName + ' بسته شد.');
          } catch (error) {
            setMessage(userErrorMessage(error, 'تسویه حساب باز انجام نشد'));
          }
        }}
        onAttention={id => {
          const item = attentionItems.find(row => row.id === id);
          if (item) focusAttentionItem(item);
        }}
        onReverse={id => {
          const item = sessionTimeline.find(row => row.id === id);
          if (item) setReverseRequest(item);
        }}
      />
      <main
        className="dashboard-main"
        onDragStart={event => event.preventDefault()}
        onMouseDown={event => {
          if (event.button !== 0) return;
          if (event.target instanceof Element && event.target.closest('.dashboard-toolbar, [data-station-id], button, input, select, textarea, a')) return;
          const ctrlKey = event.ctrlKey || event.metaKey;
          const shiftKey = event.shiftKey;
          event.preventDefault();
          window.getSelection()?.removeAllRanges();
          if (!ctrlKey && !shiftKey) setSelectionAnchorId(null);
          selectionDragRef.current = {
            stationId: '',
            startX: event.clientX,
            startY: event.clientY,
            dragging: false,
            ctrlKey,
            shiftKey,
          };
          selectionRectRef.current = null;
        }}
      >
        <div className="toolbar dashboard-toolbar">
          <div className="zone-filter">{Object.entries(zoneLabels).map(([key, label]) => <button key={key} type="button" className={zone === key ? 'active' : ''} onClick={() => setZone(key as ZoneKey)}>{label}</button>)}</div>
          {(zone === 'pc' || zone === 'all') && <label className="pc-group-control">گروه‌بندی PC
            <select value={pcGroupBy} onChange={event => setPcGroupBy(event.target.value as PcGroupBy)}>
              <option value="state">وضعیت</option>
              <option value="vip">VIP / عادی</option>
              <option value="network">اینترنت ۱ / ۲</option>
              <option value="remaining">زمان باقی‌مانده</option>
            </select>
          </label>}
          <div className="search-box"><input aria-label="جست‌وجوی ایستگاه" value={query} onChange={event => setQuery(event.target.value)} placeholder="جست‌وجوی ایستگاه…" /></div>
          <div className="view-switch" role="group" aria-label="حالت نمایش">
            {(['v-card', 'v-compact', 'v-list'] as ViewMode[]).map((item, index) => (
              <button
                key={item}
                type="button"
                className={view === item ? 'active' : ''}
                title={['کارتی', 'فشرده', 'لیستی'][index]}
                aria-pressed={view === item}
                onClick={() => setView(item)}
              >
                <span aria-hidden="true">{['▦', '▤', '☰'][index]}</span>
                <span>{['کارت', 'فشرده', 'لیست'][index]}</span>
              </button>
            ))}
          </div>
          <label className="zoom-control">اندازه <input type="range" min="70" max="130" step="5" value={zoom} onChange={event => setZoom(Number(event.target.value))} />{money(zoom)}٪</label>
          {canStartSession && <button type="button" className="btn primary" onClick={() => open('start', stations.find(item => item.state === 'free') ?? null)}>+ شروع جلسه</button>}
          {selectedStationIds.length > 0 && <div className="station-selection-tools"><span>{selectedStationIds.length.toLocaleString('fa-IR')} ایستگاه انتخاب شده</span><button type="button" className="btn sm" onClick={() => { setSelectedStationIds([]); setSelectionAnchorId(null); }}>لغو انتخاب</button></div>}
        </div>
        {(zone === 'pc' || zone === 'all') && <div className="station-sort-strip" role="group" aria-label="مرتب‌سازی رایانه‌ها">
          <span className="station-sort-caption">مرتب‌سازی</span>
          {stationSortButtons.map(key => {
            const active = stationSort.key === key;
            const arrow = active ? (stationSort.direction === 'asc' ? '↑' : '↓') : '';
            return <button
              key={key}
              type="button"
              className={active ? 'active' : ''}
              aria-pressed={active}
              onClick={() => toggleStationSort(key)}
              title={active ? 'مرتب‌سازی ' + stationSortLabels[key] + (stationSort.direction === 'asc' ? ' صعودی' : ' نزولی') : 'مرتب‌سازی بر اساس ' + stationSortLabels[key]}
            >{stationSortLabels[key]}{arrow && <span aria-hidden="true"> {arrow}</span>}</button>;
          })}
        </div>}
        {apiState === 'loading' && <p className="empty-state">در حال دریافت اطلاعات از سرور…</p>}
        {apiState === 'online' && !visibleStations.length && <p className="empty-state">ایستگاهی با این جست‌وجو پیدا نشد.</p>}
        {zone === 'all' ? groups.map(([key, title]) => {
          const items = visibleStations.filter(item => item.zone === key);
          if (!items.length) return null;
          if (key === 'pc') {
            return <section key={key}><div className="section-title">{title} · {items.length}</div>{pcGroupedStations.map(([groupName, groupItems]) => <div key={groupName} className="pc-group"><div className="pc-group-title">{groupName} · {groupItems.length}</div><div className={'station-grid ' + view} style={{ '--card-min': ((view === 'v-compact' ? 128 : 168) * zoom / 100) + 'px' } as CSSProperties}>{groupItems.map(renderStation)}</div></div>)}</section>;
          }
          return <section key={key}><div className="section-title">{title} · {items.length}</div><div className={'station-grid ' + view} style={{ '--card-min': ((view === 'v-compact' ? 128 : 168) * zoom / 100) + 'px' } as CSSProperties}>{items.map(renderStation)}</div></section>;
        }) : zone === 'pc' ? <div>{pcGroupedStations.map(([groupName, groupItems]) => <div key={groupName} className="pc-group"><div className="pc-group-title">{groupName} · {groupItems.length}</div><div className={'station-grid ' + view} style={{ '--card-min': ((view === 'v-compact' ? 128 : 168) * zoom / 100) + 'px' } as CSSProperties}>{groupItems.map(renderStation)}</div></div>)}</div> : <div className={'station-grid ' + view} style={{ '--card-min': ((view === 'v-compact' ? 128 : 168) * zoom / 100) + 'px' } as CSSProperties}>{sortedVisibleStations.map(renderStation)}</div>}
      </main>
      {selectionRect && <div
        className="station-selection-rect"
        style={{
          left: Math.min(selectionRect.startX, selectionRect.endX),
          top: Math.min(selectionRect.startY, selectionRect.endY),
          width: Math.abs(selectionRect.endX - selectionRect.startX),
          height: Math.abs(selectionRect.endY - selectionRect.startY),
        }}
        aria-hidden="true"
      />}
    </div>

    {liveSessionCenterStation && <SessionCenter
      station={liveSessionCenterStation}
      customer={customers.find(item => item.code === liveSessionCenterStation.customerCode || item.username === liveSessionCenterStation.customerCode || item.id === liveSessionCenterStation.customerCode)}
      durationMinutes={duration(liveSessionCenterStation)}
      now={now}
      onClose={() => setSessionCenterStation(null)}
      onPause={() => pauseSession(liveSessionCenterStation)}
      onResume={() => resumeSession(liveSessionCenterStation)}
      onCharge={() => { setSessionCenterStation(null); open('charge', liveSessionCenterStation); }}
      onExtend={() => { setSessionCenterStation(null); open('extend', liveSessionCenterStation); }}
      onReduce={() => { setSessionCenterStation(null); open('reduce', liveSessionCenterStation); }}
      onSettle={() => { setSessionCenterStation(null); open('settle', liveSessionCenterStation); }}
      onRateChange={rate => changeSessionRate(liveSessionCenterStation, rate)}
      onPersonsChange={persons => changeSessionPersons(liveSessionCenterStation, persons)}
      availableStations={stations.filter(item => item.state === 'free' && item.id !== liveSessionCenterStation.id)}
      onTransfer={targetId => transferSession(liveSessionCenterStation, targetId)}
      timeline={sessionTimeline.filter(item => item.stationId === liveSessionCenterStation.id)}
    />}
    {context && <div className="context-menu" style={{ left: context.x, top: context.y }} onClick={event => event.stopPropagation()}>
      <strong>{context.station.name} · {stateLabels[context.station.state as StationState]}</strong>
      {canManageSession && (context.station.state === 'busy' || context.station.state === 'paused') && <button onClick={() => contextAction('details')}>▣ جزئیات کامل جلسه</button>}
      {canSettleSession && (context.station.state === 'busy' || context.station.state === 'paused') && <button onClick={() => contextAction('settle')}>🧾 تسویه و بستن جلسه</button>}
      {canManageSession && context.station.state === 'busy' && <button onClick={() => contextAction('pause')}>⏸ توقف موقت جلسه</button>}
      {canManageSession && context.station.state === 'paused' && <button onClick={() => contextAction('resume')}>▶ ادامه جلسه</button>}
      {canManageSession && (context.station.state === 'busy' || context.station.state === 'paused') && <button onClick={() => contextAction('extend')}>⏱ تمدید وقت</button>}
      {canManageSession && (context.station.state === 'busy' || context.station.state === 'paused') && <button onClick={() => contextAction('reduce')}>↘ کاهش زمان</button>}
      {canControlClient && stationSupportsAgentLock(context.station) && context.station.agentOnline === true && !context.station.agentLocked && <button onClick={() => contextAction('lock')}>🔒 قفل دستگاه</button>}
      {canControlClient && stationSupportsAgentLock(context.station) && context.station.agentOnline === true && context.station.agentLocked && <button onClick={() => contextAction('unlock')}>🔓 باز کردن قفل</button>}
      {canControlClient && stationSupportsAgentLock(context.station) && context.station.agentOnline === true && <button onClick={() => contextAction('logout-lock')}>🚪 خروج یوزر و قفل</button>}
      {canControlClient && stationSupportsAgentLock(context.station) && context.station.agentOnline === true && <button onClick={() => contextAction('kiosk-toggle')}>{context.station.agentKioskEnabled ? '🖥️ غیرفعال‌کردن Kiosk' : '🖥️ فعال‌کردن Kiosk'}</button>}
      {canControlClient && stationSupportsAgentLock(context.station) && context.station.agentOnline === true && !['Updating', 'UpdatePending'].includes(context.station.agentLifecycleState ?? '') && <button onClick={() => contextAction('agent-update')}>⬆️ به‌روزرسانی Client</button>}
      {canControlClient && stationSupportsAgentLock(context.station) && context.station.agentOnline === true && <button onClick={() => contextAction('agent-ping')}>📡 Ping / بررسی ارتباط Agent</button>}
      {canPowerClient && stationSupportsAgentPower(context.station) && <button onClick={() => contextAction('agent-restart')}>🔄 راه‌اندازی مجدد Client</button>}
      {canPowerClient && stationSupportsAgentPower(context.station) && <button onClick={() => contextAction('agent-shutdown')}>⏻ خاموش کردن Client</button>}
      {canControlClient && stationSupportsAgentLock(context.station) && context.station.agentOnline === true && <button onClick={() => contextAction('agent-rollback')}>↩️ Rollback Client</button>}
    </div>}
    {reverseRequest && <ReverseDialog open={Boolean(reverseRequest)} title={reverseRequest.title} detail={reverseRequest.detail} onCancel={() => setReverseRequest(null)} onConfirm={() => reverseTimelineEvent(reverseRequest)} />}
    {modal && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setModal(null)}><section className="operation-modal" role="dialog" aria-modal="true">
      <button className="modal-close" onClick={() => setModal(null)} aria-label="بستن">×</button>
      {modal === 'start' && <><h2>ورود یوزر · {activeStation?.name ?? 'انتخاب ایستگاه آزاد'}</h2>
        <label>ایستگاه
          <select value={activeStation?.id ?? ''} onChange={event => setActiveStation(stations.find(item => item.id === event.target.value) ?? null)}>
            {stations.filter(item => item.state === 'free').map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </label>
        <label>یوزر / کد مشتری
          <input
            autoFocus
            value={customerCode}
            onChange={event => setCustomerCode(event.target.value)}
            onKeyDown={event => { if (event.key === 'Enter') { event.preventDefault(); lookupStartCustomer(); } }}
            placeholder="یوزر یا کد را وارد کنید و Enter بزنید"
          />
        </label>
        {customerCode && (() => {
          const selectedCustomer = findCustomer(customerCode);
          if (!selectedCustomer) return <div className="start-customer-warning">این یوزر پیدا نشد. کد، نام کاربری یا موبایل را بررسی کنید.</div>;
          const walletEmpty = selectedCustomer.wallet <= 0;
          return <div className="start-customer-result">
            <div className="start-customer-main">
              <div><strong>{selectedCustomer.name}</strong><span>کد {selectedCustomer.code} · @{selectedCustomer.username}</span></div>
              <span className={'vip-tag ' + selectedCustomer.vip}>{selectedCustomer.vip === 'gold' ? 'طلایی' : selectedCustomer.vip === 'silver' ? 'نقره‌ای' : 'عادی'}</span>
            </div>
            <div className="start-customer-money">
              <span>کیف پول</span><strong>{money(selectedCustomer.wallet)} تومان</strong>
              <span>بدهی</span><strong className={selectedCustomer.debt > 0 ? 'debt-value' : ''}>{money(selectedCustomer.debt)} تومان</strong>
            </div>
            {walletEmpty && <div className="start-wallet-warning">
              کیف پول این مشتری شارژ نیست. می‌توانید کیف پول را شارژ کنید یا جلسه را با «تسویه بعد از بازی» شروع کنید.
              <div className="start-wallet-actions">
                <button type="button" className="btn sm" onClick={openCustomerProfile}>رفتن به پروفایل / شارژ</button>
              </div>
            </div>}
            <div className="start-session-note">{activeStation?.zone === 'pc' ? 'این رایانه فقط یک نفر دارد.' : 'تعداد نفرات برای این دستگاه در مرحله شروع قابل تنظیم است.'}</div>
          </div>;
        })()}
        {activeStation?.zone !== 'pc' && <div className="person-choice">{[1, 2, 3, 4].map(item => <button key={item} className={persons === item ? 'active' : ''} onClick={() => setPersons(item)}>{money(item)} نفر</button>)}</div>}
        <div className="modal-actions">
          <button className="btn primary" onClick={startSession}>▶ ورود و شروع بازی</button>
          <button className="btn" onClick={() => setModal(null)}>لغو</button>
        </div>
      </>}
      {modal === 'flow' && <div className="customer-flow-modal"><h2>⚡ عملیات مشتری · F1</h2><CustomerOperationsWorkspace
  customers={customers}
  stations={stations}
  pendingAccounts={pendingSettlements}
  hotkeys={hotkeys}
  customerId={flowCustomerId}
  search={customerCode}
  amount={amount}
  busy={flowBusy}
  canSellBuffet={hasPermission(user, 'buffet.sell')}
  onSearchChange={setCustomerCode}
  onSearchSubmit={submitFlowSearch}
  onSelectCustomer={selectFlowCustomer}
  onAmountChange={setAmount}
  onAction={applyFlow}
  onSessionCharge={chargeFlowSession}
  onDataChanged={async customerId => {
    const refreshed = await getServerCustomers();
    const history = await getCustomerHistory(customerId).catch(() => []);
    const historyText = history.slice(0, 5).map(item => {
      const amountText = item.amount ? ' · ' + money(item.amount) + ' تومان' : '';
      const dateText = item.createdAt ? ' · ' + new Date(item.createdAt).toLocaleString('fa-IR') : '';
      return (item.description || item.type || 'فعالیت مشتری') + amountText + dateText;
    });
    setCustomers(refreshed.map(item => item.id === customerId ? { ...item, transactionHistory: historyText } : item));
    await refreshPendingSettlements();
  }}
  onBuffetAdded={(stationId, buffetAmount) => {
    const station = stations.find(item => item.id === stationId);
    if (station) {
      updateStation(station.id, { buffetTotal: (station.buffetTotal ?? 0) + buffetAmount });
      addSessionTimeline(station.id, 'buffet', 'افزودن بوفه', money(buffetAmount) + ' تومان به فاکتور جلسه اضافه شد', buffetAmount);
    }
  }}
/></div>}
      {modal === 'charge' && <><h2>⚡ شارژ سریع · {activeStation?.name}</h2><label>مبلغ شارژ<input autoFocus inputMode="numeric" value={amount} onChange={event => setAmount(event.target.value)} onKeyDown={event => event.key === 'Enter' && applyCharge('cash')} /></label><label>هدف<select value={chargeTarget} onChange={event => setChargeTarget(event.target.value as 'session' | 'wallet' | 'discount')}><option value="session">شارژ زمان همین جلسه</option><option value="wallet">شارژ کیف پول</option><option value="discount">شارژ + تخفیف</option></select></label><div className="modal-actions">{[['cash', 'نقد'], ['card', 'کارت'], ['wallet', 'کیف پول']].map(([key, label]) => <button key={key} className="btn" onClick={() => applyCharge(key)}>{label}</button>)}</div></>}
      {modal === 'settle' && activeStation && (() => {
      const settlementCustomer = customers.find(item => item.code === activeStation.customerCode || item.username === activeStation.customerCode || item.id === activeStation.customerCode);
      const preview = calculateBilling({
        elapsedMinutes: duration(activeStation),
        ratePerHour: activeStation.sessionRate ?? activeStation.ratePerHour,
        buffetAmount: activeStation.buffetTotal ?? 0,
        freeMinutes: settlementCustomer?.freeTimeMinutes ?? 0,
        discountPercent,
        minimumCharge: 0,
        roundingStep: 0,
      });
      const prepaidUsed = Math.min(preview.finalAmount, activeStation.sessionCredit ?? 0);
      const finalTotal = Math.max(0, preview.finalAmount - prepaidUsed);
      const received = number(receivedAmount) || finalTotal;
      const change = Math.max(0, received - finalTotal);
      return <>
        <h2>تسویه جلسه · {activeStation.name}</h2>
        <div className="settlement-hero"><span>مبلغ قابل دریافت</span><strong>{money(finalTotal)} تومان</strong></div>
        <div className="settlement-grid">
          <div className="info-row"><span>مدت بازی</span><strong>{money(Math.floor(duration(activeStation)))} دقیقه</strong></div>
          <div className="info-row"><span>زمان قابل صورتحساب</span><strong>{money(preview.billableMinutes)} دقیقه</strong></div>
          <div className="info-row"><span>نرخ جلسه</span><strong>{money(activeStation.sessionRate ?? activeStation.ratePerHour)} تومان/ساعت</strong></div>
          <div className="info-row"><span>هزینه زمان</span><strong>{money(preview.timeAmount)} تومان</strong></div>
          <div className="info-row"><span>بوفه</span><strong>{money(preview.buffetAmount)} تومان</strong></div>
          <div className="info-row"><span>اعتبار پیش‌پرداخت</span><strong>{prepaidUsed ? '− ' + money(prepaidUsed) : '۰'} تومان</strong></div>
          <div className="info-row"><span>تخفیف</span><strong>− {money(preview.discountAmount)} تومان</strong></div>
          <div className="info-row"><span>مبلغ پس از تخفیف</span><strong>{money(preview.roundedAmount)} تومان</strong></div>
        </div>
        <div className="settlement-why">
          <button type="button" className="settlement-why-toggle" onClick={() => setSettlementWhyOpen(value => !value)}>
            {settlementWhyOpen ? '⌃ بستن جزئیات محاسبه' : '⌄ چرا این مبلغ؟'}
          </button>
          {settlementWhyOpen && <div className="settlement-why-body">
            <div>۱. زمان بازی با نرخ همین جلسه محاسبه شد.</div>
            <div>۲. {preview.billableMinutes.toLocaleString('fa-IR')} دقیقه قابل صورتحساب × نرخ جلسه اعمال شد.</div>
            {settlementCustomer?.freeTimeMinutes ? <div>۳. {money(settlementCustomer.freeTimeMinutes)} دقیقه اعتبار زمانی رایگان از صورتحساب کم شد.</div> : null}
            {prepaidUsed > 0 ? <div>۴. {money(prepaidUsed)} تومان از شارژ ثبت‌شده قبلی پوشش داده شد.</div> : null}
            {preview.buffetAmount > 0 ? <div>۵. {money(preview.buffetAmount)} تومان بوفه به مبلغ اضافه شد.</div> : null}
            {preview.discountAmount > 0 ? <div>۶. {money(preview.discountAmount)} تومان تخفیف اعمال شد.</div> : null}
            <div>محاسبهٔ مبلغ نهایی بر اساس زمان، نرخ Session، اعتبار و تخفیف انجام می‌شود و نتیجهٔ قطعی توسط Server تأیید می‌شود.</div>
          </div>}
        </div>
        {!splitPaymentEnabled ? (
          <div className="modal-grid-2 settlement-payment-inputs">
            <label>مبلغ دریافتی نقدی (در صورت پرداخت نقدی)<input inputMode="numeric" value={receivedAmount} onChange={event => setReceivedAmount(event.target.value)} placeholder={money(finalTotal)} /></label>
            <div className="settlement-change"><span>مبلغ برگشتی</span><strong>{money(change)} تومان</strong></div>
          </div>
        ) : (
          <div className="split-payment-box">
            <div className="split-payment-head"><strong>پرداخت ترکیبی</strong><span>جمع باید دقیقاً {money(finalTotal)} تومان باشد</span></div>
            <label>نقدی<input inputMode="numeric" value={splitCash} onChange={event => setSplitCash(event.target.value)} /></label>
            <label>کارتخوان<input inputMode="numeric" value={splitCard} onChange={event => setSplitCard(event.target.value)} /></label>
            <label>کیف پول<input inputMode="numeric" value={splitWallet} onChange={event => setSplitWallet(event.target.value)} /></label>
            <div className="split-payment-total"><span>جمع واردشده</span><strong>{money(number(splitCash) + number(splitCard) + number(splitWallet))} تومان</strong></div>
          </div>
        )}
        <div className="modal-actions">
          <button type="button" className={'btn ' + (splitPaymentEnabled ? 'active' : '')} onClick={() => setSplitPaymentEnabled(value => !value)}>تقسیم پرداخت</button>
          {!splitPaymentEnabled && <button className="btn" onClick={() => finishSession('cash')}>پرداخت نقدی</button>}
          {!splitPaymentEnabled && <button className="btn" onClick={() => finishSession('card')}>کارتخوان</button>}
          {!splitPaymentEnabled && <button className="btn" onClick={() => finishSession('wallet')}>کیف پول</button>}
          {!splitPaymentEnabled && <button className="btn" onClick={() => finishSession('gift')}>اعتبار رایگان</button>}
          {splitPaymentEnabled && <button className="btn primary" onClick={() => finishSplitSession(finalTotal)}>ثبت تسویه ترکیبی</button>}
          <button className="btn danger" onClick={() => finishSession('debt')}>پرداخت بعداً / انتقال به حساب باز</button>
          <button className="btn" onClick={() => window.print()}>چاپ فاکتور</button>
        </div>
      </>;
    })()}
    </section></div>}
      {modal === 'reduce' && activeStation && <><h2>↘ کاهش زمان جلسه · {activeStation.name}</h2><div className="info-row"><span>زمان قابل صورتحساب فعلی</span><strong>{money(Math.floor(duration(activeStation)))} دقیقه</strong></div><div className="person-choice">{[[5,'۵ دقیقه'],[10,'۱۰ دقیقه'],[15,'۱۵ دقیقه'],[30,'۳۰ دقیقه'],[-1,'مدت دلخواه']].map(([value,label]) => <button key={String(value)} className={reduceMinutes === value ? 'active' : ''} onClick={() => setReduceMinutes(Number(value))}>{label}</button>)}</div>{reduceMinutes === -1 && <label>مدت دلخواه (دقیقه)<input autoFocus type="number" min="1" value={customReduceMinutes} onChange={event => setCustomReduceMinutes(event.target.value)} /></label>}<div className="modal-actions"><button className="btn primary" onClick={completeReduce}>ثبت کاهش</button><button className="btn" onClick={() => setModal(null)}>لغو</button></div></>}

      {modal === 'extend' && activeStation && <><h2>⏱ تمدید جلسه · {activeStation.name}</h2><div className="info-row"><span>زمان فعلی</span><strong>{money(Math.floor(duration(activeStation)))} دقیقه</strong></div><div className="person-choice">{[[15,'۱۵ دقیقه'],[30,'۳۰ دقیقه'],[60,'۱ ساعت'],[120,'۲ ساعت'],[-1,'مدت دلخواه']].map(([value,label]) => <button key={String(value)} className={extendMinutes === value ? 'active' : ''} onClick={() => setExtendMinutes(Number(value))}>{label}</button>)}</div>{extendMinutes === -1 && <label>مدت دلخواه (دقیقه)<input autoFocus type="number" min="1" value={customExtendMinutes} onChange={event => setCustomExtendMinutes(event.target.value)} /></label>}<div className="modal-actions"><button className="btn primary" onClick={completeExtend}>ثبت تمدید</button><button className="btn" onClick={() => setModal(null)}>لغو</button></div></>}
    {message && <div className="operation-toast" role="status">{message}</div>}
    <div className="status-footer">{snapshot?.generatedAt ? `آخرین به‌روزرسانی ${new Date(snapshot.generatedAt).toLocaleTimeString('fa-IR')}` : 'در انتظار دریافت داده'} · {serverInfo?.environment ?? 'Development'}</div>
  </>;
}
