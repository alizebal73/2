import { useEffect, useState } from 'react';
import { mockService } from '../services/mockService';
import type { UserRecord } from '../types';

const permissionRows = ['شروع/پایان جلسه','شارژ مستقیم','ثبت بدهی/هدیه','بوفه','مشتریان','گزارش کامل','تعرفه‌ها','کاربران','تنظیمات','کنترل کلاینت','Account Pool','تخفیف','بستن شیفت','مدیریت بازی‌ها'];
const defaultPermissions: Record<string, boolean[]> = Object.fromEntries(permissionRows.map((name, index) => [name, index < 4 ? [true, true, true] : [true, true, index !== 7 && index !== 8]]));

function money(value: number) { return new Intl.NumberFormat('fa-IR').format(value); }

export function UsersPage() {
  const [users, setUsers] = useState<UserRecord[]>([]);
  const [permissions, setPermissions] = useState(defaultPermissions);
  const [currentShift, setCurrentShift] = useState<any>(null);
  const [shifts, setShifts] = useState<any[]>([]);
  const [notice, setNotice] = useState('');
  const [draft, setDraft] = useState<UserRecord | null>(null);
  const [payUserId, setPayUserId] = useState<string | null>(null);
  const [payAmount, setPayAmount] = useState('');
  const [payReason, setPayReason] = useState('');
  const [manualCash, setManualCash] = useState('');
  const [shiftNote, setShiftNote] = useState('');
  const [shiftOperator, setShiftOperator] = useState('');

  async function refresh() {
    const [userRows, shift, history, saved] = await Promise.all([mockService.getUsers(), mockService.getCurrentShift(), mockService.getShifts(), mockService.getPermissions()]);
    setUsers(userRows);
    if (!shiftOperator && userRows.length) setShiftOperator(userRows.find(user => user.role === 'operator')?.name ?? userRows[0].name);
    setCurrentShift(shift);
    setShifts(history);
    const next = { ...defaultPermissions };
    Object.entries(saved).forEach(([key, value]) => {
      const split = key.lastIndexOf(':');
      const name = split >= 0 ? key.slice(0, split) : key;
      const index = split >= 0 ? Number(key.slice(split + 1)) : -1;
      if (next[name] && index >= 0) next[name] = next[name].map((item, i) => i === index ? Boolean(value) : item);
    });
    setPermissions(next);
  }
  useEffect(() => { void refresh(); }, []);

  async function saveUser() {
    if (!draft?.name.trim()) { setNotice('نام کاربر را وارد کنید'); return; }
    await mockService.saveUser(draft);
    await refresh();
    setDraft(null);
    setNotice('کاربر ذخیره شد');
  }

  async function openShift() {
    try {
      if (!shiftOperator) { setNotice('اپراتور شیفت را انتخاب کنید'); return; }
      const result = await mockService.startShift(shiftOperator);
      setCurrentShift(result);
      setNotice('شیفت جدید باز شد');
    } catch (error) { setNotice(error instanceof Error ? error.message : 'باز کردن شیفت ناموفق بود'); }
  }

  async function closeShift() {
    if (!currentShift) return;
    const value = window.prompt('وجه نقد شمارش‌شده (تومان)', '0');
    if (value === null) return;
    const counted = Number(value.replace(/[۰-۹]/g, d => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g, '')) || 0;
    const adjusted = Number(manualCash.replace(/[۰-۹]/g, d => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g, '')) || 0;
    const result = await mockService.closeShift(counted, adjusted, shiftNote.trim());
    await refresh();
    setManualCash(''); setShiftNote('');
    setNotice('شیفت بسته شد؛ اختلاف ثبت‌شده ' + money(result.difference ?? 0) + ' تومان');
  }

  async function savePermissions() {
    const flattened: Record<string, boolean> = {};
    Object.entries(permissions).forEach(([name, values]) => values.forEach((value, index) => { flattened[name + ':' + index] = value; }));
    await mockService.savePermissions(flattened);
    setNotice('ماتریس دسترسی ذخیره شد');
  }

  function setPermission(row: string, column: number, checked: boolean) {
    setPermissions(current => ({ ...current, [row]: current[row].map((value, index) => index === column ? checked : value) }));
  }

  return <>
    <div className="page-header"><div><p>کاربران، دسترسی و شیفت</p><h1>کاربران و شیفت</h1></div></div>
    <div className="toolbar">
      {!currentShift && <label className="shift-operator-select">اپراتور شیفت<select value={shiftOperator} onChange={event => setShiftOperator(event.target.value)}>{users.filter(user => user.role !== 'owner').map(user => <option key={user.id} value={user.name}>{user.name} · {user.shift}</option>)}</select></label>}
      <button className="btn" onClick={() => void (currentShift ? closeShift() : openShift())}>{currentShift ? '🕘 شیفت باز فعلی: ' + currentShift.operator + ' · ' + new Date(currentShift.openedAt).toLocaleTimeString('fa-IR') : '▶ باز کردن شیفت'}</button>
      <button className="btn danger" onClick={() => void closeShift()} disabled={!currentShift}>بستن شیفت</button>
      <button className="btn primary" onClick={() => setDraft({ id: crypto.randomUUID(), name: '', role: 'operator', shift: 'عصر', sales: 0, permissions: [], payType: 'hourly', hourlyRate: 0, monthlySalary: 0, overtimeRate: 0, workStart: '16:00', workEnd: '00:00', bonusTotal: 0, deductionTotal: 0 })}>+ کاربر جدید</button>
    </div>
    <div className="summary-grid">
      {currentShift && <div className="card-panel shift-adjust-panel" style={{gridColumn:'1 / -1',padding:12}}><strong>تطبیق نقدی خارج از سیستم</strong><small>اگر بخشی از وجه نقد گرفته شده اما در نرم‌افزار ثبت نشده، آن را جدا ثبت کن؛ این مبلغ خودکار از حقوق اپراتور کم نمی‌شود.</small><div className="modal-grid-2"><label>مبلغ نقدی ثبت‌نشده<input inputMode="numeric" value={manualCash} onChange={event => setManualCash(event.target.value)} placeholder="۰" /></label><label>توضیح/شماره رسید<input value={shiftNote} onChange={event => setShiftNote(event.target.value)} placeholder="مثلاً رسید دستی صندوق" /></label></div></div>}
      <div className="summary-card"><div className="label">کاربران</div><div className="value blue">{users.length}</div></div>
      <div className="summary-card"><div className="label">شیفت فعلی</div><div className="value green">{currentShift ? 'باز' : 'بسته'}</div></div>
      <div className="summary-card"><div className="label">بیشترین فروش ثبت‌شده</div><div className="value orange">{money(users.reduce((s,u) => Math.max(s,u.sales),0))} ت</div></div>
    </div>
    <div className="customer-layout">
      <section className="card-panel" style={{ padding: 14 }}>
        <h3>کاربران سیستم</h3>
        <div className="bullet-grid">{users.map(user => <div className="user-card" key={user.id}><b>{user.name}</b><div className="meta">نقش: {user.role === 'owner' ? 'صاحب' : user.role === 'admin' ? 'مدیر' : 'اپراتور'}</div><div className="meta">شیفت: {user.shift}</div><div className="meta">فروش: {money(user.sales)} تومان</div><div className="user-pay-summary"><span>{user.payType === 'monthly' ? 'حقوق ماهانه' : 'ساعتی'} · {money(user.payType === 'monthly' ? (user.monthlySalary ?? 0) : (user.hourlyRate ?? 0))} تومان</span><span>پاداش {money(user.bonusTotal ?? 0)} · کسری حقوق {money(user.deductionTotal ?? 0)}</span></div><div style={{display:'flex',gap:5,flexWrap:'wrap',marginTop:10}}>{user.permissions.map(permission => <span className="status-pill free" key={permission}>{permission}</span>)}</div><button type="button" className="btn sm" onClick={() => { setDraft({ ...user }); }}>ویرایش / حقوق</button><button type="button" className="btn sm" onClick={() => { setPayUserId(user.id); setPayAmount(''); setPayReason(''); }}>پاداش/کسری</button></div>)}</div>
      </section>
      <section className="card-panel" style={{ padding: 14, overflow: 'auto' }}>
        <h3>🔐 ماتریس دسترسی‌ها</h3>
        <table className="data-table"><thead><tr><th>دسترسی</th><th>صاحب</th><th>مدیر</th><th>اپراتور</th></tr></thead><tbody>
          {permissionRows.map(name => <tr key={name}><td>{name}</td>{[0,1,2].map(column => <td key={column}><input type="checkbox" checked={permissions[name]?.[column] ?? false} disabled={column === 0} onChange={event => setPermission(name,column,event.target.checked)} /></td>)}</tr>)}
        </tbody></table>
        <button className="btn primary" onClick={() => void savePermissions()}>💾 ذخیره دسترسی‌ها</button>
      </section>
    </div>
    <section className="card-panel" style={{ margin:'0 22px 20px', padding:14 }}>
      <h3>🕘 شیفت‌های اخیر</h3>
      {shifts.map(shift => <div className="info-row" key={shift.id}><span>{shift.operator} · {new Date(shift.openedAt).toLocaleString('fa-IR')} تا {shift.closedAt ? new Date(shift.closedAt).toLocaleString('fa-IR') : 'باز'}</span><strong>{money(shift.sales ?? 0)} ت · اختلاف {money(shift.difference ?? 0)} ت</strong></div>)}
    </section>
    {payUserId && <div className="modal-backdrop"><section className="operation-modal"><button className="modal-close" onClick={() => setPayUserId(null)}>×</button><h2>تغییر حقوقی · {users.find(item => item.id === payUserId)?.name}</h2><label>مبلغ (تومان)<input autoFocus inputMode="numeric" value={payAmount} onChange={event => setPayAmount(event.target.value)} /></label><label>دلیل<input value={payReason} onChange={event => setPayReason(event.target.value)} placeholder="پاداش، جریمه، اصلاح محاسبه…" /></label><div className="modal-actions"><button className="btn primary" onClick={() => { const value = Number(payAmount.replace(/[۰-۹]/g,d=>String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g,''))||0; if(!value || !payReason.trim()){ setNotice('مبلغ و دلیل را وارد کنید'); return; } setUsers(current => current.map(user => user.id === payUserId ? { ...user, bonusTotal: (user.bonusTotal ?? 0) + value } : user)); setPayUserId(null); setNotice('پاداش ثبت شد؛ کسری‌ها باید با عملیات جداگانه و تأیید ثبت شوند.'); }}>ثبت پاداش</button><button className="btn danger" onClick={() => { const value = Number(payAmount.replace(/[۰-۹]/g,d=>String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g,''))||0; if(!value || !payReason.trim()){ setNotice('مبلغ و دلیل را وارد کنید'); return; } setUsers(current => current.map(user => user.id === payUserId ? { ...user, deductionTotal: (user.deductionTotal ?? 0) + value } : user)); setPayUserId(null); setNotice('کسری حقوق ثبت شد؛ در مرحله مالی باید به تأیید مجاز برسد.'); }}>ثبت کسری حقوق</button><button className="btn" onClick={() => setPayUserId(null)}>انصراف</button></div></section></div>}
    {draft && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setDraft(null)}><section className="operation-modal"><button className="modal-close" onClick={() => setDraft(null)}>×</button><h2>{users.some(user => user.id === draft.id) ? 'ویرایش کاربر و حقوق' : 'کاربر جدید'}</h2><label>نام<input value={draft.name} onChange={event => setDraft({...draft,name:event.target.value})} /></label><label>نقش<select value={draft.role} onChange={event => setDraft({...draft,role:event.target.value as UserRecord['role']})}><option value="owner">صاحب</option><option value="admin">مدیر</option><option value="operator">اپراتور</option></select></label><label>شیفت<input value={draft.shift} onChange={event => setDraft({...draft,shift:event.target.value})} /></label><div className="modal-grid-2"><label>نوع حقوق<select value={draft.payType ?? 'hourly'} onChange={event => setDraft({...draft,payType:event.target.value as UserRecord['payType']})}><option value="hourly">ساعتی</option><option value="monthly">ماهانه</option></select></label><label>نرخ ساعتی<input inputMode="numeric" value={draft.hourlyRate ?? 0} onChange={event => setDraft({...draft,hourlyRate:Number(event.target.value.replace(/[۰-۹]/g,d=>String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g,''))||0})} /></label></div><label>حقوق ماهانه<input inputMode="numeric" value={draft.monthlySalary ?? 0} onChange={event => setDraft({...draft,monthlySalary:Number(event.target.value.replace(/[۰-۹]/g,d=>String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g,''))||0})} /></label><div className="modal-grid-2"><label>شروع کار<input type="time" value={draft.workStart ?? ''} onChange={event => setDraft({...draft,workStart:event.target.value})} /></label><label>پایان کار<input type="time" value={draft.workEnd ?? ''} onChange={event => setDraft({...draft,workEnd:event.target.value})} /></label></div><div className="modal-actions"><button className="btn primary" onClick={() => void saveUser()}>ذخیره</button><button className="btn" onClick={() => setDraft(null)}>انصراف</button></div></section></div>}
    {notice && <div className="operation-toast">{notice}<button onClick={() => setNotice('')}>×</button></div>}
  </>;
}
