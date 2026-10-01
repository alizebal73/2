import { useMemo } from 'react';
import type { CustomerRecord, SessionTimelineEvent, StationDto } from '../../types';

type Props = {
  station: StationDto;
  customer?: CustomerRecord;
  durationMinutes: number;
  now: number;
  onClose: () => void;
  onPause: () => void;
  onResume: () => void;
  onCharge: () => void;
  onExtend: () => void;
  onReduce: () => void;
  onSettle: () => void;
  timeline: SessionTimelineEvent[];
};

const money = (value: number) => new Intl.NumberFormat('fa-IR').format(Math.round(value));

export function SessionCenter({
  station,
  customer,
  durationMinutes,
  now,
  onClose,
  onPause,
  onResume,
  onCharge,
  onExtend,
  onReduce,
  onSettle,
  timeline,
}: Props) {
  const duration = Math.max(0, durationMinutes);
  const timeAmount = Math.max(0, Math.round((station.sessionRate ?? station.ratePerHour) * duration / 60));
  const buffetAmount = station.buffetTotal ?? 0;
  const grossAmount = timeAmount + buffetAmount;
  const prepaid = Math.min(grossAmount, station.sessionCredit ?? 0);
  const remaining = Math.max(0, grossAmount - prepaid);
  const prepaidEnds = station.prepaidEndsAt ? new Date(station.prepaidEndsAt).getTime() : 0;
  const prepaidMinutes = prepaidEnds ? Math.ceil((prepaidEnds - now) / 60000) : null;

  const timer = useMemo(() => {
    const total = Math.floor(duration);
    return String(Math.floor(total / 60)).padStart(2, '۰') + ':' + String(total % 60).padStart(2, '۰');
  }, [duration]);

  return (
    <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && onClose()}>
      <section className="session-center operation-modal wide" role="dialog" aria-modal="true" aria-label="مرکز جلسه">
        <button type="button" className="modal-close" onClick={onClose} aria-label="بستن">×</button>

        <div className="session-center-head">
          <div>
            <span className="session-center-kicker">مرکز جلسه</span>
            <h2>{station.name}</h2>
            <div className="session-center-sub">
              {customer ? (customer.code ?? customer.username) + ' · ' + customer.name : 'جلسه مهمان'}
              <span>·</span>
              {station.state === 'paused' ? 'متوقف' : 'در حال بازی'}
            </div>
          </div>
          <div className="session-timer-box">
            <small>زمان استفاده</small>
            <strong>{timer}</strong>
          </div>
        </div>

        <div className="session-center-grid">
          <section className="session-info-panel">
            <div className="session-info-panel-head">
              <strong>وضعیت جلسه</strong>
              <span className={station.state === 'paused' ? 'session-state paused' : 'session-state live'}>
                {station.state === 'paused' ? 'متوقف' : 'فعال'}
              </span>
            </div>
            <div className="session-facts">
              <div><span>نرخ جلسه</span><strong>{money(station.sessionRate ?? station.ratePerHour)} تومان / ساعت</strong></div>
              <div><span>نفرات</span><strong>{station.persons ?? 1} نفر</strong></div>
              <div><span>هزینه زمان</span><strong>{money(timeAmount)} تومان</strong></div>
              <div><span>بوفه</span><strong>{money(buffetAmount)} تومان</strong></div>
            </div>
          </section>

          <section className="session-info-panel financial">
            <div className="session-info-panel-head"><strong>وضعیت مالی</strong><span>تا این لحظه</span></div>
            <div className="session-total-row"><span>جمع جلسه</span><strong>{money(grossAmount)} تومان</strong></div>
            {prepaid > 0 && <div className="session-total-row prepaid"><span>از شارژ قبلی</span><strong>− {money(prepaid)} تومان</strong></div>}
            <div className="session-total-row final"><span>مبلغ قابل دریافت</span><strong>{money(remaining)} تومان</strong></div>
            {station.sessionCredit !== undefined && (
              <div className={'session-credit ' + (prepaidMinutes !== null && prepaidMinutes <= 0 ? 'danger' : prepaidMinutes !== null && prepaidMinutes <= 10 ? 'warning' : '')}>
                <span>شارژ ثبت‌شده</span>
                <strong>{money(station.sessionCredit)} تومان</strong>
                {prepaidMinutes !== null && <small>{prepaidMinutes <= 0 ? 'اعتبار زمانی تمام شده است' : money(prepaidMinutes) + ' دقیقه اعتبار باقی مانده'}</small>}
              </div>
            )}
          </section>
        </div>

        <section className="session-timeline-panel">
          <div className="session-info-panel-head">
            <strong>آخرین رویدادهای جلسه</strong>
            <span>{timeline.length.toLocaleString('fa-IR')} مورد</span>
          </div>
          {timeline.length === 0 ? (
            <div className="session-timeline-empty">برای این جلسه هنوز رویداد ثبت‌شده‌ای در رابط فعلی وجود ندارد.</div>
          ) : (
            <div className="session-timeline">
              {timeline.slice(-8).reverse().map(item => (
                <div className="session-timeline-item" key={item.id}>
                  <span className={'session-timeline-dot ' + item.kind} />
                  <div>
                    <strong>{item.title}</strong>
                    <small>{item.detail}</small>
                  </div>
                  <time>{new Date(item.createdAt).toLocaleTimeString('fa-IR', { hour: '2-digit', minute: '2-digit' })}</time>
                </div>
              ))}
            </div>
          )}
        </section>

        <section className="session-customer-panel">
          <div>
            <span className="session-customer-label">مشتری</span>
            <strong>{customer?.name ?? 'مهمان'}</strong>
            <small>{customer ? 'کد ' + (customer.code ?? customer.username) + ' · کیف پول ' + money(customer.wallet) + ' تومان · بدهی ' + money(customer.debt) + ' تومان' : 'بدون حساب مشتری'}</small>
          </div>
          {customer && customer.debt > 0 && <span className="session-debt-chip">بدهی {money(customer.debt)} تومان</span>}
        </section>

        <div className="session-action-strip">
          {station.state === 'busy'
            ? <button type="button" className="btn" onClick={onPause}>⏸ توقف موقت</button>
            : <button type="button" className="btn primary" onClick={onResume}>▶ ادامه جلسه</button>}
          <button type="button" className="btn" onClick={onCharge}>＋ شارژ جلسه</button>
          <button type="button" className="btn" onClick={onExtend}>⏱ تمدید</button>
          <button type="button" className="btn" onClick={onReduce}>↘ کاهش زمان</button>
          <button type="button" className="btn danger" onClick={onSettle}>🧾 تسویه</button>
        </div>
      </section>
    </div>
  );
}
