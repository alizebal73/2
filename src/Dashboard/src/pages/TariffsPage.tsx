import { useEffect, useMemo, useState } from 'react';
import { mockService } from '../services/mockService';
import type { TariffRecord } from '../types';

function money(value: number) {
  return new Intl.NumberFormat('fa-IR').format(value);
}

export function TariffsPage() {
  const [tariffs, setTariffs] = useState<TariffRecord[]>([]);
  const [filter, setFilter] = useState<'all' | 'normal' | 'vip'>('all');
  const [draft, setDraft] = useState<TariffRecord | null>(null);
  const [nightOnly, setNightOnly] = useState(false);
  const [notice, setNotice] = useState('');

  useEffect(() => {
    void mockService.getTariffs().then(setTariffs);
  }, []);

  const visibleTariffs = useMemo(() => tariffs.filter(item => filter === 'all' || item.tier === filter), [tariffs, filter]);

  function createTariff() {
    setNightOnly(false);
    setDraft({ id: crypto.randomUUID(), title: '', stationType: 'PC', tier: 'normal', pricePerHour: 0, daily: 0, vipDiscount: 0, nightRate: 0, nightHours: '۲۲:۰۰ تا ۰۶:۰۰', active: true });
  }

  async function saveTariff() {
    if (!draft?.title.trim() || draft.pricePerHour <= 0) { setNotice('نام تعرفه و قیمت ساعتی معتبر وارد کنید'); return; }
    await mockService.saveTariff(draft);
    setTariffs(await mockService.getTariffs());
    setDraft(null); setNotice('تعرفه ذخیره شد');
  }

  async function deleteTariff(tariff: TariffRecord) {
    if (!window.confirm(`تعرفه «${tariff.title}» حذف شود؟`)) return;
    await mockService.deleteTariff(tariff.id);
    setTariffs(await mockService.getTariffs());
    setNotice('تعرفه حذف شد');
  }

  return (
    <>
      <div className="page-header">
        <div>
          <p>تعرفه‌ها و نرخ‌ها</p>
          <h1>تعرفه‌ها</h1>
        </div>
      </div>

      <div className="toolbar">
        <div className="view-switch">{([['all', 'همه'], ['normal', 'عادی'], ['vip', 'VIP']] as const).map(([key, label]) => <button key={key} className={filter === key ? 'active' : ''} onClick={() => setFilter(key)}>{label}</button>)}</div>
        <button type="button" className="btn" onClick={() => {
          const tariff = visibleTariffs[0] ?? tariffs[0];
          if (tariff) { setDraft({ ...tariff }); setNightOnly(true); }
          else setNotice('ابتدا یک تعرفه بسازید');
        }}>⏰ تغییر تعرفه ساعت خاص</button>
        <button type="button" className="btn primary" onClick={createTariff}>+ تعرفه جدید</button>
      </div>

      <div className="tariff-grid">
        {visibleTariffs.map((tariff) => (
          <div key={tariff.id} className="tariff-card">
            <div className="tariff-card-heading"><b>{tariff.title}</b><span className="vip-tag">{tariff.tier === 'vip' ? 'VIP' : 'عادی'}</span></div>
            <div className="meta">دستگاه: {tariff.stationType}</div>
            <div className="info-row"><span>ساعتی</span><strong>{money(tariff.pricePerHour)} تومان</strong></div>
            <div className="info-row"><span>روزانه</span><strong>{tariff.daily ? `${money(tariff.daily)} تومان` : 'تعریف نشده'}</strong></div>
            <div className="info-row"><span>تخفیف VIP</span><strong>{tariff.vipDiscount}%</strong></div>
            <div className="info-row"><span>نرخ ساعت خاص</span><strong>{money(tariff.nightRate)} تومان</strong></div>
            <div className="meta">بازه ساعت خاص: {tariff.nightHours}</div>
            <div className="tariff-actions">
              <button className="btn sm" onClick={() => { setDraft({ ...tariff }); setNightOnly(false); }}>ویرایش</button>
              <button className="btn sm" onClick={() => { setDraft({ ...tariff }); setNightOnly(true); }}>ساعت خاص</button>
              <button className="btn sm danger" onClick={() => void deleteTariff(tariff)}>حذف</button>
            </div>
          </div>
        ))}
      </div>
      {draft && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setDraft(null)}><section className="operation-modal" role="dialog" aria-modal="true"><button className="modal-close" onClick={() => setDraft(null)}>×</button><h2>{nightOnly ? 'تعرفه ساعت خاص' : tariffs.some(item => item.id === draft.id) ? 'ویرایش تعرفه' : 'تعرفه جدید'}</h2>
        {!nightOnly && <><label>نام تعرفه<input value={draft.title} onChange={event => setDraft({ ...draft, title: event.target.value })} /></label><label>دستگاه<select value={draft.stationType} onChange={event => setDraft({ ...draft, stationType: event.target.value as TariffRecord['stationType'] })}><option>PC</option><option>PS5</option><option>PS4</option><option>فوتبال‌دستی</option></select></label><label>گروه مشتری<select value={draft.tier} onChange={event => setDraft({ ...draft, tier: event.target.value as TariffRecord['tier'] })}><option value="normal">عادی</option><option value="vip">VIP</option></select></label><label>قیمت ساعتی (تومان)<input type="number" min="0" value={draft.pricePerHour} onChange={event => setDraft({ ...draft, pricePerHour: Number(event.target.value) })} /></label><label>قیمت روزانه (تومان)<input type="number" min="0" value={draft.daily} onChange={event => setDraft({ ...draft, daily: Number(event.target.value) })} /></label><label>تخفیف VIP (%)<input type="number" min="0" max="100" value={draft.vipDiscount} onChange={event => setDraft({ ...draft, vipDiscount: Number(event.target.value) })} /></label></>}
        <label>قیمت ساعت خاص (تومان)<input type="number" min="0" value={draft.nightRate} onChange={event => setDraft({ ...draft, nightRate: Number(event.target.value) })} /></label><label>بازه ساعت خاص<input value={draft.nightHours} onChange={event => setDraft({ ...draft, nightHours: event.target.value })} placeholder="۲۲:۰۰ تا ۰۶:۰۰" /></label>
        <div className="modal-actions"><button className="btn primary" onClick={() => void saveTariff()}>ذخیره تعرفه</button><button className="btn" onClick={() => setDraft(null)}>انصراف</button></div>
      </section></div>}
      {notice && <div className="operation-toast" role="status">{notice}<button onClick={() => setNotice('')}>×</button></div>}
    </>
  );
}
