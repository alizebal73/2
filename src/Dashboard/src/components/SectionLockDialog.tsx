import { useEffect, useState } from 'react';
import type { PageKey } from '../types';
import { protectedPageLabels, readPageLocks, verifyPagePin } from '../services/securityService';

type Props = {
  page: PageKey | null;
  onClose: () => void;
  onUnlock: (page: PageKey) => void;
};

export function SectionLockDialog({ page, onClose, onUnlock }: Props) {
  const [pin, setPin] = useState('');
  const [notice, setNotice] = useState('');
  useEffect(() => {
    setPin('');
    setNotice('');
  }, [page]);

  const protectedPage = page;
  if (!protectedPage) return null;
  const rule = readPageLocks()[protectedPage];
  if (!rule?.enabled) return null;
  const pinHash = rule.pinHash;

  async function submit() {
    if (!pin.trim()) {
      setNotice('رمز این بخش را وارد کنید.');
      return;
    }
    const ok = await verifyPagePin(pin.trim(), pinHash);
    if (!ok) {
      setNotice('رمز اشتباه است.');
      return;
    }
    onUnlock(protectedPage);
  }

  return (
    <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && onClose()}>
      <section className="operation-modal section-lock-dialog" role="dialog" aria-modal="true" aria-label="قفل بخش">
        <div className="approval-icon">🔐</div>
        <span className="approval-kicker">دسترسی محافظت‌شده</span>
        <h2>ورود به {protectedPageLabels[protectedPage] ?? protectedPage}</h2>
        <p>برای مشاهده یا تغییر این بخش، رمز اختصاصی آن را وارد کنید.</p>
        <label>رمز بخش<input autoFocus type="password" inputMode="numeric" value={pin} onChange={event => setPin(event.target.value)} onKeyDown={event => event.key === 'Enter' && void submit()} /></label>
        {notice && <div className="user-inline-error">{notice}</div>}
        <div className="modal-actions">
          <button type="button" className="btn primary" onClick={() => void submit()}>باز کردن بخش</button>
          <button type="button" className="btn" onClick={onClose}>انصراف</button>
        </div>
        <small className="security-footnote">این قفل فعلاً لایهٔ UX است؛ مجوز واقعی باید در Server/Permission نیز اعمال شود.</small>
      </section>
    </div>
  );
}
