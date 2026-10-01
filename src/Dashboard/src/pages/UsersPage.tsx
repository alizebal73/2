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

  async function refresh() {
    const [userRows, shift, history, saved] = await Promise.all([mockService.getUsers(), mockService.getCurrentShift(), mockService.getShifts(), mockService.getPermissions()]);
    setUsers(userRows);
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
      const result = await mockService.startShift('علی محمدی');
      setCurrentShift(result);
      setNotice('شیفت جدید باز شد');
    } catch (error) { setNotice(error instanceof Error ? error.message : 'باز کردن شیفت ناموفق بود'); }
  }

  async function closeShift() {
    if (!currentShift) return;
    const value = window.prompt('وجه نقد شمارش‌شده (تومان)', '0');
    if (value === null) return;
    const counted = Number(value.replace(/[۰-۹]/g, d => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g, '')) || 0;
    const result = await mockService.closeShift(counted);
    await refresh();
    setNotice('شیفت بسته شد؛ اختلاف صندوق ' + money(result.difference ?? 0) + ' تومان');
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
      <button className="btn" onClick={() => void (currentShift ? closeShift() : openShift())}>{currentShift ? '🕘 شیفت باز فعلی: ' + currentShift.operator + ' · ' + new Date(currentShift.openedAt).toLocaleTimeString('fa-IR') : '▶ باز کردن شیفت'}</button>
      <button className="btn danger" onClick={() => void closeShift()} disabled={!currentShift}>بستن شیفت</button>
      <button className="btn primary" onClick={() => setDraft({ id: crypto.randomUUID(), name: '', role: 'operator', shift: 'عصر', sales: 0, permissions: [] })}>+ کاربر جدید</button>
    </div>
    <div className="summary-grid">
      <div className="summary-card"><div className="label">کاربران</div><div className="value blue">{users.length}</div></div>
      <div className="summary-card"><div className="label">شیفت فعلی</div><div className="value green">{currentShift ? 'باز' : 'بسته'}</div></div>
      <div className="summary-card"><div className="label">بیشترین فروش ثبت‌شده</div><div className="value orange">{money(users.reduce((s,u) => Math.max(s,u.sales),0))} ت</div></div>
    </div>
    <div className="customer-layout">
      <section className="card-panel" style={{ padding: 14 }}>
        <h3>کاربران سیستم</h3>
        <div className="bullet-grid">{users.map(user => <div className="user-card" key={user.id}><b>{user.name}</b><div className="meta">نقش: {user.role === 'owner' ? 'صاحب' : user.role === 'admin' ? 'مدیر' : 'اپراتور'}</div><div className="meta">شیفت: {user.shift}</div><div className="meta">فروش: {money(user.sales)} تومان</div><div style={{display:'flex',gap:5,flexWrap:'wrap',marginTop:10}}>{user.permissions.map(permission => <span className="status-pill free" key={permission}>{permission}</span>)}</div></div>)}</div>
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
    {draft && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setDraft(null)}><section className="operation-modal"><button className="modal-close" onClick={() => setDraft(null)}>×</button><h2>کاربر جدید</h2><label>نام<input value={draft.name} onChange={event => setDraft({...draft,name:event.target.value})} /></label><label>نقش<select value={draft.role} onChange={event => setDraft({...draft,role:event.target.value as UserRecord['role']})}><option value="owner">صاحب</option><option value="admin">مدیر</option><option value="operator">اپراتور</option></select></label><label>شیفت<input value={draft.shift} onChange={event => setDraft({...draft,shift:event.target.value})} /></label><div className="modal-actions"><button className="btn primary" onClick={() => void saveUser()}>ذخیره</button><button className="btn" onClick={() => setDraft(null)}>انصراف</button></div></section></div>}
    {notice && <div className="operation-toast">{notice}<button onClick={() => setNotice('')}>×</button></div>}
  </>;
}
