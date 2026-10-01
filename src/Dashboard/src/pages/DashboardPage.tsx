import { useCallback, useEffect, useMemo, useState } from 'react';
import type { CSSProperties, MouseEvent } from 'react';
import type { CustomerRecord, DashboardSnapshotDto, ServerInfoDto, StationDto, StationState, ZoneKey } from '../types';
import { mockService } from '../services/mockService';
import { calculateBilling, resolvePricingRate } from '../services/billingEngine';
import { SessionCenter } from '../features/session/SessionCenter';

const zoneLabels: Record<ZoneKey, string> = { all: 'همه', pc: 'رایانه‌ها (۴۰)', console: 'کنسول‌ها (۱۶)', table: 'میزها (۵)' };
const stateLabels: Record<StationState, string> = { free: 'آزاد', busy: 'در حال بازی', paused: 'متوقف', reserved: 'رزرو', off: 'خارج از سرویس' };
const emptyStations: StationDto[] = [];
type ViewMode = 'v-card' | 'v-compact' | 'v-list';
type ModalKind = 'start' | 'flow' | 'charge' | 'settle' | 'extend' | 'reduce' | null;
type Invoice = { station: string; total: number; payment: string; closedAt: string };

function money(value: number) { return new Intl.NumberFormat('fa-IR').format(Math.round(value)); }
function number(value: string) { return Number(value.replace(/[۰-۹]/g, digit => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(digit))).replace(/[٬,\s]/g, '')) || 0; }

type Props = {
  snapshot: DashboardSnapshotDto | null;
  apiState: 'loading' | 'online' | 'offline';
  serverInfo: ServerInfoDto | null;
  error: string;
  onNavigate: (page: 'client-shell') => void;
};

type ContextMenu = { x: number; y: number; station: StationDto } | null;
type AttentionKind = 'action' | 'warning' | 'info';
type SessionFollowUp = { id: string; stationId: string; stationName: string; customerCode: string; amount: number; createdAt: string; status: 'watching' | 'unpaid' | 'paid'; };
type AttentionItem = { id: string; kind: AttentionKind; station: StationDto; title: string; detail: string; actionLabel: string; followUpId?: string; };

export function DashboardPage({ snapshot, apiState, serverInfo, onNavigate }: Props) {
  const [stationOverrides, setStationOverrides] = useState<StationDto[] | null>(null);
  const [zone, setZone] = useState<ZoneKey>('all');
  const [query, setQuery] = useState('');
  const [view, setView] = useState<ViewMode>('v-card');
  const [zoom, setZoom] = useState(100);
  const [now, setNow] = useState(Date.now());
  const [modal, setModal] = useState<ModalKind>(null);
  const [activeStation, setActiveStation] = useState<StationDto | null>(null);
  const [sessionCenterStation, setSessionCenterStation] = useState<StationDto | null>(null);
  const [customers, setCustomers] = useState<CustomerRecord[]>([]);
  const [tariffs, setTariffs] = useState<import('../types').TariffRecord[]>([]);
  const [customerCode, setCustomerCode] = useState('');
  const [persons, setPersons] = useState(1);
  const [manualRate, setManualRate] = useState('');
  const [paymentMode, setPaymentMode] = useState<'settle-later' | 'prepaid'>('settle-later');
  const [amount, setAmount] = useState('');
  const [chargeTarget, setChargeTarget] = useState<'session' | 'wallet' | 'discount'>('session');
  const [context, setContext] = useState<ContextMenu>(null);
  const [attentionOpen, setAttentionOpen] = useState(false);
  const [sessionFollowUps, setSessionFollowUps] = useState<SessionFollowUp[]>([]);
  const [message, setMessage] = useState('');
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [flowStep, setFlowStep] = useState<1 | 2>(1);
  const [extendMinutes, setExtendMinutes] = useState(30);
  const [customExtendMinutes, setCustomExtendMinutes] = useState('30');
  const [reduceMinutes, setReduceMinutes] = useState(15);
  const [customReduceMinutes, setCustomReduceMinutes] = useState('15');
  const [discountPercent, setDiscountPercent] = useState(0);
  const [roundingEnabled, setRoundingEnabled] = useState(true);
  const [hotkeys, setHotkeys] = useState<Record<string,string>>(() => { try { return JSON.parse(localStorage.getItem('gamenet-hotkeys-v1') || '{}'); } catch { return {}; } });

  const stations = stationOverrides ?? snapshot?.stations ?? emptyStations;
  const liveSessionCenterStation = sessionCenterStation ? stations.find(item => item.id === sessionCenterStation.id) ?? null : null;
  const updateStation = useCallback((id: string, update: Partial<StationDto>) => {
    setStationOverrides(items => (items ?? snapshot?.stations ?? emptyStations).map(item => item.id === id ? { ...item, ...update } : item));
  }, [snapshot?.stations]);
  const duration = useCallback((station: StationDto) => {
    if (!station.startedAt) return station.sessionMinutes ?? 0;
    const referenceNow = station.state === 'paused' && station.pausedAt ? new Date(station.pausedAt).getTime() : now;
    const pausedMinutes = station.pausedMinutes ?? 0;
    return Math.max(0, (referenceNow - new Date(station.startedAt).getTime()) / 60000 - pausedMinutes);
  }, [now]);
  const applyFlow = useCallback((action: string) => {
    const value = number(amount);
    if (!value) { setMessage('مبلغ معتبر وارد کنید'); return; }
    const customer = customers.find(item => item.code === customerCode || item.username === customerCode || item.mobile === customerCode || item.id === customerCode || item.name.includes(customerCode));
    if (!customer) { setMessage('مشتری پیدا نشد'); return; }
    if (action === 'F5') {
      setCustomers(current => current.map(item => item.id === customer.id ? { ...item, wallet: item.wallet + value, transactionHistory: ['شارژ مستقیم · ' + money(value) + ' تومان', ...(item.transactionHistory ?? [])] } : item));
      setMessage('شارژ مستقیم ' + money(value) + ' تومان ثبت شد');
    } else if (action === 'F6') {
      setCustomers(current => current.map(item => item.id === customer.id ? { ...item, debt: item.debt + value, transactionHistory: ['ثبت بدهی · ' + money(value) + ' تومان', ...(item.transactionHistory ?? [])] } : item));
      setMessage('بدهی ' + money(value) + ' تومان ثبت شد');
    } else if (action === 'F7') {
      if (customer.wallet < value) { setMessage('موجودی کیف پول کافی نیست'); return; }
      setCustomers(current => current.map(item => item.id === customer.id ? { ...item, wallet: item.wallet - value, transactionHistory: ['کسر از کیف پول · ' + money(value) + ' تومان', ...(item.transactionHistory ?? [])] } : item));
      setMessage(money(value) + ' تومان از کیف پول کسر شد');
    } else {
      const deducted = Math.min(customer.wallet, value);
      setCustomers(current => current.map(item => item.id === customer.id ? { ...item, wallet: item.wallet - deducted, debt: item.debt + value - deducted, transactionHistory: ['کسر کیف پول/بدهی · ' + money(value) + ' تومان', ...(item.transactionHistory ?? [])] } : item));
      setMessage('تراکنش F8 ثبت شد؛ ' + money(Math.max(0, value - deducted)) + ' تومان مازاد به بدهی رفت');
    }
    setModal(null);
  }, [amount, customers, customerCode]);
  useEffect(() => { void Promise.all([mockService.getCustomers(), mockService.getTariffs()]).then(([customerRows, tariffRows]) => { setCustomers(customerRows); setTariffs(tariffRows); }); const onHotkeys = (event: Event) => setHotkeys((event as CustomEvent<Record<string,string>>).detail || {}); window.addEventListener('gamenet-hotkeys-changed', onHotkeys); return () => window.removeEventListener('gamenet-hotkeys-changed', onHotkeys); }, []);
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);
  useEffect(() => {
    if (!message) return;
    const timer = window.setTimeout(() => setMessage(''), 3200);
    return () => window.clearTimeout(timer);
  }, [message]);
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { setModal(null); setContext(null); setSessionCenterStation(null); }
      const flowKey = (hotkeys.flow || 'F1').toUpperCase();
      const amountKey = (hotkeys.amount || 'F4').toUpperCase();
      const walletAddKey = (hotkeys.walletAdd || 'F5').toUpperCase();
      const debtAddKey = (hotkeys.debtAdd || 'F6').toUpperCase();
      const walletDeductKey = (hotkeys.walletDeduct || 'F7').toUpperCase();
      const walletDebtKey = (hotkeys.walletDebt || 'F8').toUpperCase();
      if (event.key.toUpperCase() === flowKey) { event.preventDefault(); setFlowStep(1); setModal('flow'); }
      if (modal === 'flow' && flowStep === 2 && [walletAddKey, debtAddKey, walletDeductKey, walletDebtKey].includes(event.key.toUpperCase())) {
        event.preventDefault();
        const action = event.key.toUpperCase() === walletAddKey ? 'F5' : event.key.toUpperCase() === debtAddKey ? 'F6' : event.key.toUpperCase() === walletDeductKey ? 'F7' : 'F8';
        applyFlow(action);
      }
      if (modal === 'flow' && event.key.toUpperCase() === amountKey) document.getElementById('flow-amount')?.focus();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [modal, flowStep, applyFlow, hotkeys]);
  useEffect(() => {
    const onCommand = (event: Event) => {
      const command = (event as CustomEvent<string>).detail;
      if (command === 'start-session') open('start', stations.find(item => item.state === 'free') ?? null);
      if (command === 'quick-charge') open('charge', stations.find(item => item.state === 'busy') ?? null);
      if (command === 'flow') { setFlowStep(1); open('flow', stations.find(item => item.state === 'busy') ?? null); }
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
      const detail = (event as CustomEvent<{ total: number }>).detail;
      const station = stations.find(item => item.state === 'busy');
      if (station) {
        updateStation(station.id, { buffetTotal: (station.buffetTotal ?? 0) + detail.total });
        setMessage(`فروش ${money(detail.total)} تومان به فاکتور ${station.name} اضافه شد`);
      } else setMessage('جلسه فعالی نیست؛ فروش مستقل ثبت کنید');
    };
    window.addEventListener('gamenet-buffet-sale', onBuffetSale);
    return () => window.removeEventListener('gamenet-buffet-sale', onBuffetSale);
  }, [stations, updateStation]);

  const visibleStations = useMemo(() => stations.filter(station =>
    (zone === 'all' || station.zone === zone) && station.name.toLowerCase().includes(query.trim().toLowerCase())), [stations, zone, query]);
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

  const attentionCounts = useMemo(() => ({ total: attentionItems.length, action: attentionItems.filter(item => item.kind === 'action').length, warning: attentionItems.filter(item => item.kind === 'warning').length }), [attentionItems]);


  function focusAttentionItem(item: AttentionItem) {
    setAttentionOpen(false);
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
    setManualRate(station ? String(Math.round(station.ratePerHour)) : '');
    setPersons(station?.zone === 'pc' ? 1 : 2);
    setChargeTarget('session');
    setCustomerCode(station?.customerCode ?? '');
    setDiscountPercent(0);
    setRoundingEnabled(true);
    setExtendMinutes(30);
    setCustomExtendMinutes('30');
    setReduceMinutes(15);
    setCustomReduceMinutes('15');
    setContext(null);
    setModal(kind);
  }
  function startSession() {
    if (!activeStation || activeStation.state !== 'free') { setMessage('این ایستگاه دیگر آزاد نیست'); return; }
    const customer = customers.find(item => item.code === customerCode || item.username === customerCode);
    const tariff = tariffs.find(item => item.active && item.stationType === activeStation.type && item.tier === (customer && customer.vip !== 'none' ? 'vip' : 'normal'))
      ?? tariffs.find(item => item.active && item.stationType === activeStation.type);
    const baseRate = customer && customer.vip !== 'none' && tariff
      ? Math.max(0, Math.round(tariff.pricePerHour * (1 - Math.min(100, Math.max(0, tariff.vipDiscount)) / 100)))
      : (tariff?.pricePerHour ?? activeStation.ratePerHour);
    const sessionRate = tariff ? resolvePricingRate(baseRate, tariff.schedule, new Date()) : activeStation.ratePerHour;
    updateStation(activeStation.id, { state: 'busy', startedAt: new Date().toISOString(), sessionMinutes: 0, sessionRate, amountSoFar: 0, persons, customerCode, buffetTotal: 0, pausedAt: undefined, pausedMinutes: 0 });
    setModal(null);
    setMessage(`جلسه ${activeStation.name} شروع شد`);
  }
  function finishSession(method: string) {
    if (!activeStation) return;
    const elapsed = duration(activeStation);
    const customer = customers.find(item => item.code === activeStation.customerCode || item.username === activeStation.customerCode || item.id === activeStation.customerCode);
    const tariff = tariffs.find(item => item.stationType === activeStation.type);
    const billing = calculateBilling({
      elapsedMinutes: elapsed,
      ratePerHour: activeStation.sessionRate ?? activeStation.ratePerHour,
      buffetAmount: activeStation.buffetTotal ?? 0,
      freeMinutes: customer?.freeTimeMinutes ?? 0,
      discountPercent,
      minimumCharge: tariff?.minimumCharge ?? 0,
      roundingStep: roundingEnabled ? (tariff?.roundingStep ?? 1000) : 0,
    });
    const timeCost = billing.timeAmount;
    const prepaidUsed = Math.min(billing.finalAmount, activeStation.sessionCredit ?? 0);
    const finalTotal = Math.max(0, billing.finalAmount - prepaidUsed);
    if (method === 'wallet' && (!customer || customer.wallet < finalTotal)) { setMessage('موجودی کیف پول کافی نیست'); return; }
    const closedAt = new Date().toISOString();
    setInvoices(items => [{ station: activeStation.name, total: finalTotal, payment: method, closedAt }, ...items]);
    setSessionFollowUps(current => current.map(item => item.stationId === activeStation.id && item.status !== 'paid' ? { ...item, status: method === 'debt' ? 'unpaid' : 'paid' } : item));
    void mockService.addReportRow({
      id: crypto.randomUUID(),
      station: activeStation.name,
      timeAmount: Math.round(timeCost),
      buffet: activeStation.buffetTotal ?? 0,
      packageAmount: 0,
      amount: finalTotal,
      method: method === 'cash' ? 'cash' : method === 'card' ? 'card' : 'wallet',
      operator: 'علی محمدی',
      type: 'time',
      closedAt
    });
    if (method === 'wallet' && customer) {
      setCustomers(current => current.map(item => item.id === customer.id
        ? { ...item, wallet: item.wallet - finalTotal, transactionHistory: ['تسویه کیف پول · ' + money(finalTotal) + ' تومان', ...(item.transactionHistory ?? [])] }
        : item));
    }
    if (method === 'debt' && customer) {
      setCustomers(current => current.map(item => item.id === customer.id
        ? { ...item, debt: item.debt + finalTotal, transactionHistory: ['ثبت بدهی تسویه · ' + money(finalTotal) + ' تومان', ...(item.transactionHistory ?? [])] }
        : item));
    }
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
      prepaidEndsAt: undefined
    });
    setModal(null);
    setMessage('تسویه ' + money(finalTotal) + ' تومان ثبت شد؛ فاکتور در تاریخچه باقی ماند');
  }

  function pauseSession(stationOverride?: StationDto) {
    const station = stationOverride ?? activeStation;
    if (!station || station.state !== 'busy') { setMessage('فقط جلسه در حال بازی قابل توقف است'); return; }
    updateStation(station.id, { state: 'paused', pausedAt: new Date().toISOString() });
    setModal(null);
    setMessage('جلسه متوقف موقت شد؛ زمان صورتحساب جلو نمی‌رود');
  }

  function resumeSession(stationOverride?: StationDto) {
    const station = stationOverride ?? activeStation;
    if (!station || station.state !== 'paused' || !station.pausedAt) { setMessage('جلسه متوقفی برای ادامه وجود ندارد'); return; }
    const currentPaused = (Date.now() - new Date(station.pausedAt).getTime()) / 60000;
    updateStation(station.id, { state: 'busy', pausedAt: undefined, pausedMinutes: (station.pausedMinutes ?? 0) + Math.max(0, currentPaused) });
    setMessage('جلسه ادامه پیدا کرد');
  }

  function completeReduce() {
    if (!activeStation || !['busy', 'paused'].includes(activeStation.state)) { setMessage('فقط جلسه فعال یا متوقف قابل کاهش زمان است'); return; }
    const minutes = reduceMinutes === -1 ? Math.max(1, Number(customReduceMinutes.replace(/[۰-۹]/g, digit => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(digit)))) || 0) : reduceMinutes;
    const current = duration(activeStation);
    if (!minutes || minutes >= current) { setMessage('زمان کاهش باید کمتر از زمان استفاده‌شده باشد'); return; }
    const nextStartedAt = new Date(new Date(activeStation.startedAt ?? Date.now()).getTime() + minutes * 60000).toISOString();
    updateStation(activeStation.id, { startedAt: nextStartedAt, sessionMinutes: Math.max(0, current - minutes) });
    setModal(null);
    setMessage(money(minutes) + ' دقیقه از زمان قابل صورتحساب کم شد');
  }

  function completeExtend() {
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
    const nextStartedAt = new Date(new Date(activeStation.startedAt ?? Date.now()).getTime() - minutes * 60000).toISOString();
    updateStation(activeStation.id, { sessionMinutes: duration(activeStation) + minutes, startedAt: nextStartedAt });
    setModal(null);
    setMessage(`${money(minutes)} دقیقه به جلسه ${activeStation.name} اضافه شد`);
  }

  function applyCharge(method: string) {
    const value = number(amount);
    if (!value || !activeStation) { setMessage('مبلغ معتبر وارد کنید'); return; }
    const customer = customers.find(item => item.code === activeStation.customerCode || item.username === activeStation.customerCode || item.id === activeStation.customerCode);
    if (method === 'debt') {
      if (!customer) { setMessage('جلسه مشتری ثبت‌شده ندارد'); return; }
      setCustomers(current => current.map(item => item.id === customer.id ? { ...item, debt: item.debt + value, transactionHistory: ['ثبت بدهی · ' + money(value) + ' تومان', ...(item.transactionHistory ?? [])] } : item));
      setMessage('بدهی ' + money(value) + ' تومان ثبت شد');
    } else if (chargeTarget === 'wallet' || chargeTarget === 'discount') {
      if (!customer) { setMessage('جلسه به مشتری وصل نیست'); return; }
      if (method === 'wallet') { setMessage('برای شارژ کیف پول، نقد یا کارت را انتخاب کنید'); return; }
      setCustomers(current => current.map(item => item.id === customer.id ? { ...item, wallet: item.wallet + value, discountLevel: chargeTarget === 'discount' ? item.discountLevel + 1 : item.discountLevel, transactionHistory: [(chargeTarget === 'discount' ? 'شارژ + تخفیف' : 'شارژ کیف پول') + ' · ' + money(value) + ' تومان', ...(item.transactionHistory ?? [])] } : item));
      setMessage(chargeTarget === 'discount' ? 'شارژ + تخفیف ثبت شد' : 'کیف پول شارژ شد');
    } else if (activeStation.state === 'busy') {
      const rate = activeStation.sessionRate ?? activeStation.ratePerHour;
      const extraMinutes = rate > 0 ? value / (rate / 60) : 0;
      const currentEnd = activeStation.prepaidEndsAt ? new Date(activeStation.prepaidEndsAt).getTime() : Date.now();
      const base = Math.max(Date.now(), currentEnd);
      const prepaidEndsAt = new Date(base + Math.max(0, extraMinutes) * 60000).toISOString();
      updateStation(activeStation.id, {
        sessionMinutes: duration(activeStation) + extraMinutes,
        startedAt: new Date(Date.now() - (duration(activeStation) + extraMinutes) * 60000).toISOString(),
        sessionCredit: (activeStation.sessionCredit ?? 0) + value,
        prepaidEndsAt,
      });
      setSessionFollowUps(current => [...current, { id: crypto.randomUUID(), stationId: activeStation.id, stationName: activeStation.name, customerCode: activeStation.customerCode ?? customer?.code ?? 'مهمان', amount: value, createdAt: new Date().toISOString(), status: 'watching' }]);
      setMessage(money(value) + ' تومان شارژ شد؛ پیگیری آن در «نیازمند توجه» ثبت شد');
    } else setMessage('شارژ ' + money(value) + ' تومان ثبت شد');
    setModal(null);
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
    const width = 280;
    const height = 430;
    setContext({ x: Math.max(8, Math.min(x, window.innerWidth - width - 8)), y: Math.max(8, Math.min(y, window.innerHeight - height - 8)), station });
  }

  function showContext(event: MouseEvent<HTMLElement>, station: StationDto) {
    event.preventDefault();
    openContextAt(event.clientX, event.clientY, station);
  }
  function contextAction(action: string) {
    const station = context?.station;
    setContext(null);
    if (!station) return;
    if (action === 'details') { openSessionCenter(station); return; }
    if (action === 'settle') { open('settle', station); return; }
    if (action === 'extend') { open('extend', station); return; }
    if (action === 'pause') { pauseSession(station); return; }
    if (action === 'resume') { resumeSession(station); return; }
    if (action === 'reduce') { open('reduce', station); return; }
    if (action === 'offline') {
      updateStation(station.id, { state: station.state === 'off' ? 'free' : 'off', outOfServiceReason: station.state === 'off' ? undefined : 'تعمیر و نگهداری' });
      setMessage(station.state === 'off' ? 'ایستگاه فعال شد' : 'ایستگاه خارج از سرویس شد'); return;
    }
    if (action === 'settings') { window.dispatchEvent(new CustomEvent('gamenet-select-client', { detail: station.name })); onNavigate('client-shell'); return; }
    if (action === 'switch-net') { updateStation(station.id, { network: station.network === 1 ? 2 : 1 }); setMessage(`شبکه به اینترنت ${station.network === 1 ? '۲' : '۱'} تغییر کرد`); return; }
    setMessage(`${action} برای ${station.name} در صف دمو ثبت شد`);
  }

  function renderStation(station: StationDto) {
    const minutes = duration(station);
    const elapsedCost = station.state === 'busy' ? (station.sessionRate ?? station.ratePerHour) * minutes / 60 : 0;
    const style = { '--zoom': zoom / 100 } as CSSProperties;
    return <article id={`station-${station.id}`} key={station.id} style={style} className={`station-card ${station.state} ${view}`} onClick={() => {
      if (station.state === 'free') open('start', station);
      else if (station.state === 'busy' || station.state === 'paused') openSessionCenter(station);
      else setMessage(station.state === 'reserved' ? 'رزرو ساعت ۱۸:۰۰ — هنوز مشتری وارد نشده' : station.outOfServiceReason ?? 'این دستگاه خارج از سرویس است');
    }} onDoubleClick={() => station.state === 'busy' && open('charge', station)} onContextMenu={event => showContext(event, station)}>
      <div className="top"><div className="name">{station.name}</div><span className={`status-badge ${station.state}`}>{stateLabels[station.state as StationState] ?? station.state}</span></div>
      <span className="type">{station.type} · شبکه {station.network ?? 1}</span>
      <div className="time">{station.state === 'busy' ? `${money(Math.floor(minutes / 60)).padStart(2, '۰')}:${money(Math.floor(minutes % 60)).padStart(2, '۰')}` : station.state === 'reserved' ? 'رزرو ۱۸:۰۰' : station.state === 'off' ? '⛔' : '--:--'}</div>
      <div className="price">{station.state === 'busy' ? `هزینه ${money(elapsedCost)} تومان` : `از ${money(station.ratePerHour)} تومان / ساعت`}</div>
      {station.state === 'busy' && <><div className="person-dots">{'● '.repeat(station.persons ?? 1)}</div><div className="progress-bar"><span style={{ width: `${Math.min(100, minutes % 60 / 60 * 100)}%` }} /></div><span className="pulse" /></>}
      {station.state === 'off' && <small>{station.outOfServiceReason ?? 'در تعمیر'}</small>}
      <div className="station-hover-actions" onClick={event => event.stopPropagation()} onDoubleClick={event => event.stopPropagation()}>
        {station.state === 'free' && <button type="button" className="quick primary" onClick={() => open('start', station)}>▶ شروع</button>}
        {station.state === 'busy' && <button type="button" className="quick" onClick={() => { setActiveStation(station); pauseSession(); }}>⏸ مکث</button>}
        {station.state === 'paused' && <button type="button" className="quick primary" onClick={() => { setActiveStation(station); resumeSession(); }}>▶ ادامه</button>}
        {(station.state === 'busy' || station.state === 'paused') && <button type="button" className="quick" onClick={() => open('settle', station)}>🧾 تسویه</button>}
        {(station.state === 'busy' || station.state === 'paused') && <button type="button" className="quick" onClick={() => open('extend', station)}>⏱ تمدید</button>}
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
      {[["ایستگاه آزاد", counts.free, 'green'], ['در حال جلسه', counts.busy + stations.filter(item => item.state === 'paused').length, 'red'], ['رزرو امروز', counts.reserved, 'blue'], ['درآمد امروز', invoices.reduce((sum, item) => sum + item.total, 4820000), 'orange'], ['فروش بوفه', 860000, 'orange'], ['مشتری حاضر', stations.filter(item => item.state === 'busy').reduce((sum, item) => sum + (item.persons ?? 1), 0), 'blue']].map(([label, value, color]) => <div key={label} className="summary-card"><div className="label">{label}</div><div className={`value ${color}`}>{money(Number(value))}{String(label).includes('درآمد') || String(label).includes('فروش') ? ' تومان' : ''}</div></div>)}
    </div>
        <div className="attention-bar">
      <button type="button" className={`attention-trigger ${attentionCounts.total ? 'has-items' : ''}`} onClick={() => setAttentionOpen(value => !value)} aria-expanded={attentionOpen}>
        ⚠ نیازمند توجه
        <span>{attentionCounts.total ? money(attentionCounts.total) : '۰'}</span>
      </button>
      <span className="attention-summary">
        {attentionCounts.action ? `اقدام لازم: ${money(attentionCounts.action)}` : 'اقدام فوری نداریم'}
        {attentionCounts.warning ? ` · هشدار: ${money(attentionCounts.warning)}` : ''}
      </span>
    </div>
    {attentionOpen && (
      <section className="attention-panel" aria-label="مرکز نیازمند توجه">
        <div className="attention-panel-head">
          <div><strong>مرکز نیازمند توجه</strong><small>مواردی که از وضعیت فعلی ایستگاه‌ها نیاز به بررسی یا اقدام دارند</small></div>
          <button type="button" className="btn sm" onClick={() => setAttentionOpen(false)}>بستن</button>
        </div>
        {attentionItems.length === 0 ? <div className="attention-empty">در حال حاضر موردی نیازمند توجه نیست.</div> : <div className="attention-list">
          {attentionItems.map(item => <div className={`attention-item ${item.kind}`} key={item.id}>
            <span className="attention-dot" aria-hidden="true" />
            <div className="attention-content"><strong>{item.title}</strong><span>{item.detail}</span></div>
            <button type="button" className="btn sm" onClick={() => focusAttentionItem(item)}>{item.actionLabel}</button>
          </div>)}
        </div>}
      </section>
    )}
<div className="toolbar dashboard-toolbar">
      <div className="zone-filter">{Object.entries(zoneLabels).map(([key, label]) => <button key={key} type="button" className={zone === key ? 'active' : ''} onClick={() => setZone(key as ZoneKey)}>{label}</button>)}</div>
      <div className="search-box"><input aria-label="جست‌وجوی ایستگاه" value={query} onChange={event => setQuery(event.target.value)} placeholder="جست‌وجوی ایستگاه…" /></div>
      <div className="view-switch" aria-label="حالت نمایش">{(['v-card', 'v-compact', 'v-list'] as ViewMode[]).map((item, index) => <button key={item} type="button" className={view === item ? 'active' : ''} title={['کارتی', 'فشرده', 'لیستی'][index]} onClick={() => setView(item)}>{['▦', '▤', '☰'][index]}</button>)}</div>
      <label className="zoom-control">اندازه <input type="range" min="70" max="130" step="5" value={zoom} onChange={event => setZoom(Number(event.target.value))} />{money(zoom)}٪</label>
      <button type="button" className="btn" onClick={() => setMessage('۲ اعلان جدید')}>🔔 ۲</button>
      <button type="button" className="btn primary" onClick={() => open('start', stations.find(item => item.state === 'free') ?? null)}>+ شروع جلسه</button>
    </div>
    {apiState === 'loading' && <p className="empty-state">در حال دریافت DTO از سرور…</p>}
    {apiState === 'online' && !visibleStations.length && <p className="empty-state">ایستگاهی با این جست‌وجو پیدا نشد.</p>}
    {zone === 'all' ? groups.map(([key, title]) => {
      const items = visibleStations.filter(item => item.zone === key);
      if (!items.length) return null;
      return <section key={key}><div className="section-title">{title} · {items.length}</div><div className={`station-grid ${view}`} style={{ '--card-min': `${(view === 'v-compact' ? 128 : 168) * zoom / 100}px` } as CSSProperties}>{items.map(renderStation)}</div></section>;
    }) : <div className={`station-grid ${view}`} style={{ '--card-min': `${(view === 'v-compact' ? 128 : 168) * zoom / 100}px` } as CSSProperties}>{visibleStations.map(renderStation)}</div>}

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
    />}
    {context && <div className="context-menu" style={{ left: context.x, top: context.y }} onClick={event => event.stopPropagation()}>
      <strong>{context.station.name} · {stateLabels[context.station.state as StationState]}</strong>
      {(context.station.state === 'busy' || context.station.state === 'paused') && <button onClick={() => contextAction('details')}>▣ جزئیات کامل جلسه</button>}
      {(context.station.state === 'busy' || context.station.state === 'paused') && <button onClick={() => contextAction('settle')}>🧾 تسویه و بستن جلسه</button>}
      {context.station.state === 'busy' && <button onClick={() => contextAction('pause')}>⏸ توقف موقت جلسه</button>}
      {context.station.state === 'paused' && <button onClick={() => contextAction('resume')}>▶ ادامه جلسه</button>}
      {(context.station.state === 'busy' || context.station.state === 'paused') && <button onClick={() => contextAction('extend')}>⏱ تمدید وقت</button>}
      {(context.station.state === 'busy' || context.station.state === 'paused') && <button onClick={() => contextAction('reduce')}>↘ کاهش زمان</button>}
      <button onClick={() => contextAction('switch-net')}>🌐 تغییر اینترنت ۱ ↔ ۲</button>
      <button onClick={() => contextAction('move-user')}>🔀 جابه‌جایی یوزر</button>
      <button onClick={() => contextAction('logout-lock')}>🚪 خروج یوزر و قفل</button>
      <button onClick={() => contextAction('login-id')}>🔑 ورود با شناسه</button>
      <button onClick={() => contextAction('message')}>💬 پیام به مشتری</button>
      <button onClick={() => contextAction('screenshot')}>📸 اسکرین‌شات</button>
      <button onClick={() => contextAction('restart-shell')}>🔄 ری‌استارت Shell</button>
      <button onClick={() => contextAction('restart')}>⏻ ری‌استارت Windows</button>
      <button onClick={() => contextAction('shutdown')}>⛔ خاموش کردن</button>
      <button onClick={() => contextAction('offline')}>🛠 خارج از سرویس / فعال‌سازی</button>
      <button onClick={() => contextAction('settings')}>⚙ تنظیمات کامل کلاینت</button>
    </div>}
    {modal && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setModal(null)}><section className="operation-modal" role="dialog" aria-modal="true">
      <button className="modal-close" onClick={() => setModal(null)} aria-label="بستن">×</button>
      {modal === 'start' && <><h2>شروع جلسه · {activeStation?.name ?? 'انتخاب ایستگاه آزاد'}</h2><label>ایستگاه<select value={activeStation?.id ?? ''} onChange={event => setActiveStation(stations.find(item => item.id === event.target.value) ?? null)}>{stations.filter(item => item.state === 'free').map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label><label>مشتری<select value={customerCode} onChange={event => setCustomerCode(event.target.value)}><option value="">مهمان</option>{customers.map(item => <option key={item.id} value={item.username}>{item.code ?? item.username} · {item.name}</option>)}</select></label>{customerCode && (() => { const selectedCustomer = customers.find(item => item.username === customerCode || item.code === customerCode); if (!selectedCustomer) return null; const used = selectedCustomer.hoursUsedToday ?? 0; const cap = selectedCustomer.dailyHourCap ?? 0; const remaining = Math.max(0, cap - used); return <div className="package-box"><div className="title"><strong>{selectedCustomer.packageName ?? 'بدون پکیج VIP'}</strong><span className={`vip-tag ${selectedCustomer.vip}`}>{selectedCustomer.vip === 'gold' ? 'Gold' : selectedCustomer.vip === 'silver' ? 'Silver' : 'None'}</span></div><div className="info-row"><span>مصرف امروز</span><strong>{used} ساعت</strong></div><div className="info-row"><span>باقی‌مانده روزانه</span><strong>{selectedCustomer.packageName ? `${remaining} ساعت` : 'بدون سقف پکیج'}</strong></div>{selectedCustomer.packageName && used >= cap && <strong className="limit-warning">سقف روزانه تکمیل شده؛ زمان مازاد طبق قانون پکیج محاسبه می‌شود.</strong>}</div>; })()}<label>نرخ این جلسه (تومان / ساعت)<input autoFocus inputMode="numeric" value={manualRate} onChange={event => setManualRate(event.target.value)} /></label>{activeStation?.zone === 'pc' ? <div className="single-person-note">رایانه برای هر جلسه فقط ۱ نفر دارد.</div> : <div className="person-choice">{[1, 2, 3, 4].map(item => <button key={item} className={persons === item ? 'active' : ''} onClick={() => setPersons(item)}>{money(item)} نفر</button>)}</div>}<div className="person-choice"><button className={paymentMode === 'settle-later' ? 'active' : ''} onClick={() => setPaymentMode('settle-later')}>تسویه بعد از بازی</button><button className={paymentMode === 'prepaid' ? 'active' : ''} onClick={() => setPaymentMode('prepaid')}>پیش‌پرداخت / شارژی</button></div><div className="modal-actions"><button className="btn primary" onClick={startSession}>▶ شروع بازی</button><button className="btn" onClick={() => setModal(null)}>لغو</button></div></>}
      {modal === 'flow' && <><h2>⚡ فلوی سرعت · F1</h2>{flowStep === 1 ? <><label>شناسه مشتری<input autoFocus value={customerCode} onChange={event => setCustomerCode(event.target.value)} onKeyDown={event => event.key === 'Enter' && setFlowStep(2)} placeholder="کد، نام، لقب یا موبایل" /></label><button className="btn primary" onClick={() => setFlowStep(2)}>نمایش پروفایل</button></> : <><p>{customers.find(item => [item.username, item.mobile, item.id, item.name].some(value => value.includes(customerCode)))?.name ?? 'مشتری مهمان'} · {activeStation?.name ?? 'بدون دستگاه'}</p><label>مبلغ (تومان)<input id="flow-amount" inputMode="numeric" value={amount} onChange={event => setAmount(event.target.value)} /></label><div className="modal-actions">{[['F5', 'شارژ مستقیم'], ['F6', 'ثبت بدهی'], ['F7', 'کسر از کیف پول'], ['F8', 'کسر کیف پول + بدهی']].map(([key, label]) => <button key={key} className="btn" onClick={() => applyFlow(key)}>{key} {label}</button>)}</div></>}</>}
      {modal === 'charge' && <><h2>⚡ شارژ سریع · {activeStation?.name}</h2><label>مبلغ شارژ<input autoFocus inputMode="numeric" value={amount} onChange={event => setAmount(event.target.value)} onKeyDown={event => event.key === 'Enter' && applyCharge('cash')} /></label><label>هدف<select value={chargeTarget} onChange={event => setChargeTarget(event.target.value as 'session' | 'wallet' | 'discount')}><option value="session">شارژ زمان همین جلسه</option><option value="wallet">شارژ کیف پول</option><option value="discount">شارژ + تخفیف</option></select></label><div className="modal-actions">{[['cash', 'نقد'], ['card', 'کارت'], ['wallet', 'کیف پول'], ['debt', 'ثبت در بدهی']].map(([key, label]) => <button key={key} className="btn" onClick={() => applyCharge(key)}>{label}</button>)}</div></>}
      {modal === 'settle' && activeStation && (() => {
      const baseTotal = Math.max(0, Math.round((activeStation.sessionRate ?? activeStation.ratePerHour) * duration(activeStation) / 60 + (activeStation.buffetTotal ?? 0)));
      const discount = Math.min(baseTotal, Math.round(baseTotal * Math.max(0, Math.min(100, discountPercent)) / 100));
      const discounted = Math.max(0, baseTotal - discount);
      const finalTotal = roundingEnabled ? Math.round(discounted / 1000) * 1000 : discounted;
      return <><h2>تسویه جلسه · {activeStation.name}</h2><div className="info-row"><span>مدت جلسه</span><strong>{money(Math.floor(duration(activeStation)))} دقیقه</strong></div><div className="info-row"><span>مبلغ زمان</span><strong>{money(Math.round((activeStation.sessionRate ?? activeStation.ratePerHour) * duration(activeStation) / 60))} تومان</strong></div><div className="info-row"><span>مبلغ بوفه</span><strong>{money(activeStation.buffetTotal ?? 0)} تومان</strong></div><div className="modal-grid-2"><label>تخفیف (%)<input type="number" min="0" max="100" value={discountPercent} onChange={event => setDiscountPercent(Number(event.target.value))} /></label><label className="setting-item"><span>رند به ۱۰۰۰ تومان</span><input type="checkbox" checked={roundingEnabled} onChange={event => setRoundingEnabled(event.target.checked)} /></label></div><div className="info-row"><span>تخفیف</span><strong>{money(discount)} تومان</strong></div><div className="info-row"><span>مبلغ نهایی</span><strong>{money(finalTotal)} تومان</strong></div><div className="modal-actions"><button className="btn" onClick={() => finishSession('cash')}>پرداخت نقدی</button><button className="btn" onClick={() => finishSession('card')}>کارتخوان</button><button className="btn" onClick={() => finishSession('wallet')}>کیف پول</button><button className="btn danger" onClick={() => finishSession('debt')}>پرداخت بعداً / ثبت بدهی</button><button className="btn" onClick={() => window.print()}>چاپ فاکتور</button></div></>;
    })()}
    </section></div>}
      {modal === 'reduce' && activeStation && <><h2>↘ کاهش زمان جلسه · {activeStation.name}</h2><div className="info-row"><span>زمان قابل صورتحساب فعلی</span><strong>{money(Math.floor(duration(activeStation)))} دقیقه</strong></div><div className="person-choice">{[[5,'۵ دقیقه'],[10,'۱۰ دقیقه'],[15,'۱۵ دقیقه'],[30,'۳۰ دقیقه'],[-1,'مدت دلخواه']].map(([value,label]) => <button key={String(value)} className={reduceMinutes === value ? 'active' : ''} onClick={() => setReduceMinutes(Number(value))}>{label}</button>)}</div>{reduceMinutes === -1 && <label>مدت دلخواه (دقیقه)<input autoFocus type="number" min="1" value={customReduceMinutes} onChange={event => setCustomReduceMinutes(event.target.value)} /></label>}<div className="modal-actions"><button className="btn primary" onClick={completeReduce}>ثبت کاهش</button><button className="btn" onClick={() => setModal(null)}>لغو</button></div></>}

      {modal === 'extend' && activeStation && <><h2>⏱ تمدید جلسه · {activeStation.name}</h2><div className="info-row"><span>زمان فعلی</span><strong>{money(Math.floor(duration(activeStation)))} دقیقه</strong></div><div className="person-choice">{[[15,'۱۵ دقیقه'],[30,'۳۰ دقیقه'],[60,'۱ ساعت'],[120,'۲ ساعت'],[-1,'مدت دلخواه']].map(([value,label]) => <button key={String(value)} className={extendMinutes === value ? 'active' : ''} onClick={() => setExtendMinutes(Number(value))}>{label}</button>)}</div>{extendMinutes === -1 && <label>مدت دلخواه (دقیقه)<input autoFocus type="number" min="1" value={customExtendMinutes} onChange={event => setCustomExtendMinutes(event.target.value)} /></label>}<div className="modal-actions"><button className="btn primary" onClick={completeExtend}>ثبت تمدید</button><button className="btn" onClick={() => setModal(null)}>لغو</button></div></>}
    {message && <div className="operation-toast" role="status">{message}</div>}
    <div className="status-footer">{snapshot?.generatedAt ? `آخرین به‌روزرسانی ${new Date(snapshot.generatedAt).toLocaleTimeString('fa-IR')}` : 'در انتظار دریافت داده'} · {serverInfo?.environment ?? 'Development'} · {invoices.length} فاکتور ثبت‌شده</div>
  </>;
}
