import { useEffect, useMemo, useState } from 'react';
import { archiveTariff, getTariffs, saveTariff, type ServerTariff } from '../services/tariffService';
import { userErrorMessage } from '../utils/userError';

function money(value: number) {
  return new Intl.NumberFormat('fa-IR').format(value);
}

export function TariffsPage() {
  const [tariffs, setTariffs] = useState<ServerTariff[]>([]);
  const [showInactive, setShowInactive] = useState(false);
  const [draft, setDraft] = useState<ServerTariff | null>(null);
  const [notice, setNotice] = useState('');

  async function load() {
    try {
      setTariffs(await getTariffs());
    } catch (error) {
      setNotice(userErrorMessage(error, 'تعرفه‌ها از سرور دریافت نشد'));
    }
  }

  useEffect(() => { void load(); }, []);

  const visibleTariffs = useMemo(
    () => tariffs.filter(item => showInactive || item.isActive),
    [tariffs, showInactive],
  );

  function createTariff() {
    setDraft({
      id: '',
      name: '',
      description: '',
      hourlyRate: 0,
      dailyRate: 0,
      isActive: true,
    });
  }

  async function save() {
    if (!draft?.name.trim() || draft.hourlyRate <= 0) {
      setNotice('نام تعرفه و قیمت ساعتی معتبر وارد کنید');
      return;
    }

    try {
      const saved = await saveTariff(draft.id || null, draft);
      setTariffs(current => {
        const exists = current.some(item => item.id === saved.id);
        return exists ? current.map(item => item.id === saved.id ? saved : item) : [...current, saved];
      });
      setDraft(null);
      setNotice('تعرفه روی سرور ذخیره شد');
    } catch (error) {
      setNotice(userErrorMessage(error, 'ذخیره تعرفه انجام نشد'));
    }
  }

  async function disable(tariff: ServerTariff) {
    if (!window.confirm(\`تعرفه «\${tariff.name}» غیرفعال شود؟\`)) return;

    try {
      await archiveTariff(tariff.id);
      setTariffs(current => current.map(item => item.id === tariff.id ? { ...item, isActive: false } : item));
      setNotice('تعرفه غیرفعال شد؛ حذف فیزیکی انجام نمی‌شود');
    } catch (error) {
      setNotice(userErrorMessage(error, 'غیرفعال‌سازی تعرفه انجام نشد'));
    }
  }

  return (
    <>
      <div className="page-header">
        <div>
          <p>منبع رسمی قیمت‌گذاری Server</p>
          <h1>تعرفه‌ها</h1>
        </div>
      </div>

      <div className="toolbar">
        <label className="inline-toggle">
          <input type="checkbox" checked={showInactive} onChange={event => setShowInactive(event.target.checked)} />
          نمایش تعرفه‌های غیرفعال
        </label>
        <button type="button" className="btn primary" onClick={createTariff}>+ تعرفه جدید</button>
      </div>

      <div className="tariff-grid">
        {visibleTariffs.map(tariff => (
          <article key={tariff.id} className="tariff-card">
            <div className="tariff-card-heading">
              <b>{tariff.name}</b>
              <span className="status-pill">{tariff.isActive ? 'فعال' : 'غیرفعال'}</span>
            </div>
            <div className="meta">{tariff.description || 'بدون توضیح'}</div>
            <div className="info-row"><span>ساعتی</span><strong>{money(tariff.hourlyRate)} تومان</strong></div>
            <div className="info-row"><span>روزانه</span><strong>{tariff.dailyRate ? money(tariff.dailyRate) + ' تومان' : 'تعریف نشده'}</strong></div>
            <div className="tariff-actions">
              <button className="btn sm" onClick={() => setDraft({ ...tariff })}>ویرایش</button>
              {tariff.isActive && <button className="btn sm danger" onClick={() => void disable(tariff)}>غیرفعال‌سازی</button>}
            </div>
          </article>
        ))}
      </div>

      {draft && (
        <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setDraft(null)}>
          <section className="operation-modal" role="dialog" aria-modal="true">
            <button className="modal-close" onClick={() => setDraft(null)}>×</button>
            <h2>{draft.id ? 'ویرایش تعرفه' : 'تعرفه جدید'}</h2>
            <label>نام تعرفه
              <input value={draft.name} onChange={event => setDraft({ ...draft, name: event.target.value })} />
            </label>
            <label>توضیح
              <input value={draft.description ?? ''} onChange={event => setDraft({ ...draft, description: event.target.value })} />
            </label>
            <label>قیمت ساعتی (تومان)
              <input type="number" min="0" value={draft.hourlyRate} onChange={event => setDraft({ ...draft, hourlyRate: Number(event.target.value) })} />
            </label>
            <label>قیمت روزانه (تومان)
              <input type="number" min="0" value={draft.dailyRate} onChange={event => setDraft({ ...draft, dailyRate: Number(event.target.value) })} />
            </label>
            <label>وضعیت
              <select value={draft.isActive ? 'active' : 'inactive'} onChange={event => setDraft({ ...draft, isActive: event.target.value === 'active' })}>
                <option value="active">فعال</option>
                <option value="inactive">غیرفعال</option>
              </select>
            </label>
            <div className="modal-actions">
              <button className="btn primary" onClick={() => void save()}>ذخیره روی سرور</button>
              <button className="btn" onClick={() => setDraft(null)}>انصراف</button>
            </div>
          </section>
        </div>
      )}

      {notice && <div className="operation-toast" role="status">{notice}<button onClick={() => setNotice('')}>×</button></div>}
    </>
  );
}
