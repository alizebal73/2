import { useEffect, useState } from 'react';
import { decideApproval, getApprovals, getPermissions, getUsers, hasPermission, setUserPermissions } from '../services/authService';
import type { AppUserRecord } from '../types';
import { userErrorMessage } from '../utils/userError';
import { closeServerShift, getCurrentShift, getShiftHistory, startServerShift } from '../services/shiftService';
import type { ApprovalRecord, PayrollUserRecord, UserRecord } from '../types';
import { createPayrollEntry, getPayrollUsers, updatePayrollProfile } from '../services/payrollService';


function money(value: number) { return new Intl.NumberFormat('fa-IR').format(value); }

type UsersPageProps = { user: AppUserRecord };

export function UsersPage({ user }: UsersPageProps) {
  const canManageUsers = hasPermission(user, 'user.manage');
  const canManageShift = hasPermission(user, 'shift.manage');
  const canViewPayroll = hasPermission(user, 'payroll.view');
  const canManagePayroll = hasPermission(user, 'payroll.manage');
  const canDecideApproval = hasPermission(user, 'approval.decide');
  const [users, setUsers] = useState<UserRecord[]>([]);
  const [serverUsers, setServerUsers] = useState<AppUserRecord[]>([]);
  const [permissionCatalog, setPermissionCatalog] = useState<Array<{ id: string; name: string; description?: string }>>([]);
  const [payrollUsers, setPayrollUsers] = useState<PayrollUserRecord[]>([]);
  const [pendingPayrollApprovals, setPendingPayrollApprovals] = useState<ApprovalRecord[]>([]);
  const [selectedUserId, setSelectedUserId] = useState('');
  const [selectedPermissions, setSelectedPermissions] = useState<string[]>([]);
  const [currentShift, setCurrentShift] = useState<any>(null);
  const [shifts, setShifts] = useState<any[]>([]);
  const [notice, setNotice] = useState('');
  const [draft, setDraft] = useState<UserRecord | null>(null);
  const [payUserId, setPayUserId] = useState<string | null>(null);
  const [payAmount, setPayAmount] = useState('');
  const [payReason, setPayReason] = useState('');
  const [payMode, setPayMode] = useState<'salary' | 'bonus' | 'deduction' | 'damage' | 'advance'>('salary');
  const [manualCash, setManualCash] = useState('');
  const [shiftNote, setShiftNote] = useState('');
  const [shiftOperator, setShiftOperator] = useState('');
  const [closeShiftOpen, setCloseShiftOpen] = useState(false);
  const [countedCash, setCountedCash] = useState('');
  const [handoverNote, setHandoverNote] = useState('');
  const [shiftOpeningCash, setShiftOpeningCash] = useState('0');

  async function refresh() {
    try {
      if (canManageUsers) {
        const [serverRows, catalog] = await Promise.all([getUsers(), getPermissions()]);
        setServerUsers(serverRows);
        setPermissionCatalog(catalog);
        const userRows: UserRecord[] = serverRows.map(serverUser => ({
          id: serverUser.id,
          name: serverUser.fullName,
          role: serverUser.role.toLowerCase() === 'owner' ? 'owner' : serverUser.role.toLowerCase() === 'admin' || serverUser.role.toLowerCase() === 'manager' ? 'admin' : 'operator',
          shift: 'سرور',
          sales: 0,
          permissions: serverUser.permissions,
        }));
        setUsers(userRows);
        const nextUserId = selectedUserId && serverRows.some(row => row.id === selectedUserId) ? selectedUserId : (serverRows[0]?.id ?? '');
        setSelectedUserId(nextUserId);
        const selected = serverRows.find(row => row.id === nextUserId);
        setSelectedPermissions(selected?.permissions ?? []);
        if (!shiftOperator && userRows.length) setShiftOperator(userRows.find(row => row.role !== 'owner')?.name ?? userRows[0].name);
      } else {
        setServerUsers([]);
        setPermissionCatalog([]);
        setUsers([]);
        setSelectedUserId('');
        setSelectedPermissions([]);
        setShiftOperator('کاربر جاری');
      }

      if (canViewPayroll) {
        const rows = await getPayrollUsers();
        setPayrollUsers(rows);
        if (canManagePayroll && rows.length && !draft) {
          const currentDraft = rows.find(row => row.userId === selectedUserId);
          if (!currentDraft) {
            // Profile remains closed until the operator explicitly opens it.
          }
        }
      } else {
        setPayrollUsers([]);
      }

      if (canDecideApproval) {
        const rows = await getApprovals();
        setPendingPayrollApprovals(rows.filter(row => row.action === 'payroll.entry' && row.status === 'Pending'));
      } else {
        setPendingPayrollApprovals([]);
      }
    } catch (error) {
      setNotice(userErrorMessage(error, 'اطلاعات کاربران/حقوق از سرور دریافت نشد'));
    }

    if (canManageShift) {
      try {
        const [serverShift, serverHistory] = await Promise.all([getCurrentShift(), getShiftHistory()]);
        setCurrentShift(serverShift);
        setShifts(serverHistory.map(row => ({ ...row, sales: row.cashSales })));
      } catch (error) {
        setCurrentShift(null);
        setShifts([]);
        setNotice(userErrorMessage(error, 'اطلاعات شیفت از سرور دریافت نشد'));
      }
    } else {
      setCurrentShift(null);
      setShifts([]);
    }
  }

  useEffect(() => { void refresh(); }, [canManageUsers, canManageShift, canViewPayroll, canDecideApproval]);

  async function saveUser() {
    if (!draft?.id || !canManagePayroll) { setNotice('دسترسی مدیریت حقوق ندارید'); return; }
    try {
      const schedule = [draft.workStart, draft.workEnd].filter(Boolean).join('-') || null;
      await updatePayrollProfile(draft.id, {
        payType: draft.payType ?? 'hourly',
        hourlyRate: draft.hourlyRate ?? 0,
        monthlySalary: draft.monthlySalary ?? 0,
        overtimeRate: draft.overtimeRate ?? 0,
        workSchedule: schedule,
        isActive: true,
      });
      await refresh();
      setDraft(null);
      setNotice('پروفایل حقوقی روی سرور ذخیره شد');
    } catch (error) {
      setNotice(userErrorMessage(error, 'ذخیره پروفایل حقوقی ناموفق بود'));
    }
  }

  async function openShift() {
    if (!canManageShift) { setNotice('دسترسی مدیریت شیفت ندارید'); return; }
    try {
      if (!shiftOperator) { setNotice('اپراتور شیفت را انتخاب کنید'); return; }
      const opening = Number(shiftOpeningCash.replace(/[۰-۹]/g, d => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,s]/g, '')) || 0;
      const result = await startServerShift({ operatorName: shiftOperator, cashOpening: opening, note: shiftNote.trim() || undefined });
      setCurrentShift(result);
      setShiftOpeningCash('0');
      setShiftNote('');
      setNotice('شیفت روی سرور باز شد');
    } catch (error) { setNotice(userErrorMessage(error, 'باز کردن شیفت ناموفق بود')); }
  }

  function openCloseShift() {
    if (!canManageShift) { setNotice('دسترسی مدیریت شیفت ندارید'); return; }
    if (!currentShift) { setNotice('شیفت بازی برای بستن وجود ندارد'); return; }
    setCountedCash('');
    setHandoverNote('');
    setCloseShiftOpen(true);
  }

  async function closeShift() {
    if (!canManageShift || !currentShift) return;
    const counted = Number(countedCash.replace(/[۰-۹]/g, d => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g, '')) || 0;
    const adjusted = Number(manualCash.replace(/[۰-۹]/g, d => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g, '')) || 0;
    try {
      if (!currentShift?.id || !/^[0-9a-f-]{36}$/i.test(currentShift.id)) {
        setNotice('شیفت فعال از سرور شناخته نشد؛ ابتدا صفحه را تازه کنید.');
        return;
      }
      const result = await closeServerShift(currentShift.id, {
        cashClosing: counted,
        externalCash: adjusted,
        note: [shiftNote.trim(), handoverNote.trim()].filter(Boolean).join(' · ') || undefined,
      });
      await refresh();
      setManualCash(''); setShiftNote(''); setHandoverNote(''); setCountedCash(''); setCloseShiftOpen(false);
      setNotice('شیفت بسته شد؛ اختلاف ثبت‌شده ' + money(result.difference ?? 0) + ' تومان');
    } catch (error) {
      setNotice(userErrorMessage(error, 'بستن شیفت ناموفق بود'));
    }
  }

  async function applyPayAction() {
    const value = Number(payAmount.replace(/[۰-۹]/g, d => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g, '')) || 0;
    if (!payUserId || !value || !payReason.trim() || !canManagePayroll) {
      setNotice('مبلغ، دلیل و دسترسی مدیریت حقوق الزامی است');
      return;
    }
    const kind = payMode === 'salary'
      ? 'SalaryPayment'
      : payMode === 'bonus'
        ? 'Bonus'
        : payMode === 'deduction'
          ? 'Deduction'
          : payMode === 'damage'
            ? 'Damage'
            : 'Advance';
    try {
      const result = await createPayrollEntry(payUserId, {
        kind,
        amount: value,
        reason: payReason.trim(),
        paymentMethod: payMode === 'salary' ? 'cash' : undefined,
      });
      await refresh();
      setPayUserId(null);
      setPayAmount('');
      setPayReason('');
      setNotice(result.status === 'Pending'
        ? 'عملیات ثبت شد و برای تأیید مسئول مجاز ارسال شد.'
        : 'عملیات حقوقی ثبت شد.');
    } catch (error) {
      setNotice(userErrorMessage(error, 'ثبت عملیات حقوقی ناموفق بود'));
    }
  }

  async function decidePayrollApproval(id: string, approved: boolean) {
    if (!canDecideApproval) return;
    try {
      await decideApproval(id, approved, approved ? 'تأیید عملیات حقوقی' : 'رد عملیات حقوقی');
      await refresh();
      setNotice(approved ? 'عملیات حقوقی تأیید و اجرا شد.' : 'عملیات حقوقی رد شد.');
    } catch (error) {
      setNotice(userErrorMessage(error, 'تصمیم‌گیری درباره عملیات حقوقی ناموفق بود'));
    }
  }

  async function savePermissions() {
    if (!canManageUsers) { setNotice('دسترسی مدیریت کاربران ندارید'); return; }
    if (!selectedUserId) { setNotice('کاربری برای تغییر دسترسی انتخاب نشده است'); return; }
    try {
      await setUserPermissions(selectedUserId, selectedPermissions);
      await refresh();
      setNotice('دسترسی‌های کاربر روی سرور ذخیره شد');
    } catch (error) {
      setNotice(userErrorMessage(error, 'ذخیره دسترسی‌ها ناموفق بود'));
    }
  }

  function selectPermissionUser(id: string) {
    if (!canManageUsers) return;
    setSelectedUserId(id);
    setSelectedPermissions(serverUsers.find(user => user.id === id)?.permissions ?? []);
  }

  function togglePermission(name: string, checked: boolean) {
    setSelectedPermissions(current => checked
      ? Array.from(new Set([...current, name]))
      : current.filter(item => item !== name));
  }

  return <>
    <div className="page-header"><div><p>کاربران، دسترسی و شیفت</p><h1>کاربران و شیفت</h1></div></div>
    <div className="toolbar">
      {!currentShift && <label className="shift-operator-select">اپراتور شیفت<select value={shiftOperator} onChange={event => setShiftOperator(event.target.value)}>{users.filter(user => user.role !== 'owner').map(user => <option key={user.id} value={user.name}>{user.name} · {user.shift}</option>)}</select></label>}
      {!currentShift && <label className="shift-operator-select">صندوق اولیه<input inputMode="numeric" value={shiftOpeningCash} onChange={event => setShiftOpeningCash(event.target.value)} placeholder="۰" /></label>}
      {canManageShift && <button className="btn" onClick={() => void (currentShift ? openCloseShift() : openShift())}>{currentShift ? '🕘 شیفت باز فعلی: ' + currentShift.operator + ' · ' + new Date(currentShift.openedAt).toLocaleTimeString('fa-IR') : '▶ باز کردن شیفت'}</button>}
      {canManageShift && <button className="btn danger" onClick={() => openCloseShift()} disabled={!currentShift}>بستن شیفت</button>}
      <span className="status-pill free">{canManageUsers ? 'کاربران و Permission از Server' : 'دسترسی این حساب فقط به عملیات مجاز محدود شده است'}</span>
    </div>
    <div className="summary-grid">
      {currentShift && <div className="card-panel shift-adjust-panel" style={{gridColumn:'1 / -1',padding:12}}><strong>تطبیق نقدی خارج از سیستم</strong><small>اگر بخشی از وجه نقد گرفته شده اما در نرم‌افزار ثبت نشده، آن را جدا ثبت کن؛ این مبلغ خودکار از حقوق اپراتور کم نمی‌شود.</small><div className="modal-grid-2"><label>مبلغ نقدی ثبت‌نشده<input inputMode="numeric" value={manualCash} onChange={event => setManualCash(event.target.value)} placeholder="۰" /></label><label>توضیح/شماره رسید<input value={shiftNote} onChange={event => setShiftNote(event.target.value)} placeholder="مثلاً رسید دستی صندوق" /></label></div></div>}
      <div className="summary-card"><div className="label">کاربران</div><div className="value blue">{users.length}</div></div>
      <div className="summary-card"><div className="label">شیفت فعلی</div><div className="value green">{currentShift ? 'باز' : 'بسته'}</div></div>
      <div className="summary-card"><div className="label">بیشترین فروش ثبت‌شده</div><div className="value orange">{money(users.reduce((s,u) => Math.max(s,u.sales),0))} ت</div></div>
      <div className="summary-card"><div className="label">طلب پرسنل</div><div className="value red">{money(users.reduce((s,u) => s + (u.employeePayable ?? 0), 0))} ت</div></div>
      <div className="summary-card"><div className="label">طلب مالک</div><div className="value purple">{money(users.reduce((s,u) => s + (u.ownerReceivable ?? 0), 0))} ت</div></div>
    </div>
    <div className="customer-layout">
      {canManageUsers && <section className="card-panel" style={{ padding: 14 }}>
        <h3>کاربران سیستم</h3>
        <div className="bullet-grid">{users.map(user => <div className="user-card" key={user.id}><b>{user.name}</b><div className="meta">نقش: {user.role === 'owner' ? 'صاحب' : user.role === 'admin' ? 'مدیر' : 'اپراتور'}</div><div className="meta">شیفت: {user.shift}</div><div className="meta">فروش: {money(user.sales)} تومان</div><div className="user-pay-summary"><span>{user.payType === 'monthly' ? 'حقوق ماهانه' : 'ساعتی'} · {money(user.payType === 'monthly' ? (user.monthlySalary ?? 0) : (user.hourlyRate ?? 0))} تومان</span><span>پرداخت‌شده {money(user.paidSalaryTotal ?? 0)} · مانده حقوق {money(user.employeePayable ?? 0)}</span><span>طلب مالک {money(user.ownerReceivable ?? 0)} · خسارت {money(user.damageTotal ?? 0)}</span></div><div style={{display:'flex',gap:5,flexWrap:'wrap',marginTop:10}}>{user.permissions.map(permission => <span className="status-pill free" key={permission}>{permission}</span>)}</div><button type="button" className="btn sm" onClick={() => selectPermissionUser(user.id)}>دسترسی‌ها</button>{canViewPayroll && <button type="button" className="btn sm" onClick={() => {
  const row = payrollUsers.find(item => item.userId === user.id);
  setDraft({ ...user, payType: row?.payType === 'monthly' ? 'monthly' : 'hourly', hourlyRate: row?.hourlyRate ?? 0, monthlySalary: row?.monthlySalary ?? 0, overtimeRate: row?.overtimeRate ?? 0, workStart: row?.workSchedule?.split('-')[0] ?? '', workEnd: row?.workSchedule?.split('-')[1] ?? '' });
}}>پروفایل حقوق</button>}{canManagePayroll && <button type="button" className="btn sm" onClick={() => { setPayUserId(user.id); setPayAmount(''); setPayReason(''); setPayMode('salary'); }}>ثبت عملیات</button>}</div>)}</div>
      </section>}
      {canManageUsers && <section className="card-panel" style={{ padding: 14, overflow: 'auto' }}>
        <h3>🔐 دسترسی سروری کاربر</h3>
        <label>کاربر<select value={selectedUserId} onChange={event => selectPermissionUser(event.target.value)}>
          <option value="">انتخاب کاربر</option>
          {serverUsers.map(user => <option key={user.id} value={user.id}>{user.fullName} · {user.role}</option>)}
        </select></label>
        <div className="data-table" style={{display:'grid',gap:8,marginTop:12}}>
          {permissionCatalog.map(permission => <label key={permission.name} style={{display:'flex',alignItems:'center',gap:10,padding:'8px 10px',borderBottom:'1px solid rgba(255,255,255,.06)'}}>
            <input type="checkbox" checked={selectedPermissions.includes(permission.name)} disabled={!selectedUserId} onChange={event => togglePermission(permission.name,event.target.checked)} />
            <span><strong>{permission.name}</strong><small style={{display:'block',opacity:.65}}>{permission.description}</small></span>
          </label>)}
        </div>
        <button className="btn primary" disabled={!selectedUserId} onClick={() => void savePermissions()}>💾 ذخیره دسترسی‌های کاربر</button>
      </section>}
    </div>
    {canViewPayroll && <section className="card-panel" style={{ margin:'0 22px 20px', padding:14 }}>
      <div style={{display:'flex',justifyContent:'space-between',alignItems:'center',gap:12}}>
        <div><h3 style={{marginBottom:4}}>حساب حقوق و Ledger پرسنل</h3><small>مانده حقوق پرسنل و طلب مالک جدا از اختلاف صندوق نگهداری می‌شود.</small></div>
        <span className="status-pill free">{payrollUsers.length} نفر</span>
      </div>
      <div className="data-table" style={{marginTop:12}}>
        {payrollUsers.map(row => <div className="info-row" key={row.userId}>
          <span><strong>{row.fullName}</strong> · {row.payType === 'monthly' ? 'ماهانه' : 'ساعتی'} · حقوق این ماه {money(row.accruedThisMonth)} ت</span>
          <strong>طلب پرسنل {money(row.employeePayable)} ت · طلب مالک {money(row.ownerReceivable)} ت · پرداخت این ماه {money(row.paidThisMonth)} ت</strong>
          {canManagePayroll && <button className="btn sm" onClick={() => { setPayUserId(row.userId); setPayAmount(''); setPayReason(''); setPayMode('salary'); }}>عملیات</button>}
        </div>)}
      </div>
    </section>}
    {canDecideApproval && pendingPayrollApprovals.length > 0 && <section className="card-panel" style={{ margin:'0 22px 20px', padding:14 }}>
      <h3>عملیات حقوق در انتظار تأیید</h3>
      {pendingPayrollApprovals.map(row => <div className="info-row" key={row.id}>
        <span><strong>{row.action}</strong> · {row.reason}</span>
        <span>{row.requestedBy} · {new Date(row.createdAt).toLocaleString('fa-IR')}</span>
        <div style={{display:'flex',gap:6}}>
          <button className="btn sm primary" onClick={() => void decidePayrollApproval(row.id, true)}>تأیید</button>
          <button className="btn sm danger" onClick={() => void decidePayrollApproval(row.id, false)}>رد</button>
        </div>
      </div>)}
    </section>}
    <section className="card-panel" style={{ margin:'0 22px 20px', padding:14 }}>
      <h3>🕘 شیفت‌های اخیر</h3>
      {shifts.map(shift => <div className="info-row" key={shift.id}><span>{shift.operator} · {new Date(shift.openedAt).toLocaleString('fa-IR')} تا {shift.closedAt ? new Date(shift.closedAt).toLocaleString('fa-IR') : 'باز'}</span><strong>{money(shift.sales ?? 0)} ت · اختلاف {money(shift.difference ?? 0)} ت</strong></div>)}
    </section>
    {closeShiftOpen && currentShift && (() => {
      const expectedCash = currentShift.expectedCash ?? 0;
      const adjusted = expectedCash + (Number(manualCash.replace(/[۰-۹]/g, d => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g, '')) || 0);
      const counted = Number(countedCash.replace(/[۰-۹]/g, d => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g, '')) || 0;
      const difference = counted - adjusted;
      return <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setCloseShiftOpen(false)}>
        <section className="operation-modal wide" role="dialog" aria-modal="true">
          <button className="modal-close" onClick={() => setCloseShiftOpen(false)} aria-label="بستن">×</button>
          <h2>تسویه و تحویل شیفت</h2>
          <div className="shift-close-grid">
            <div className="info-row"><span>اپراتور</span><strong>{currentShift.operator}</strong></div>
            <div className="info-row"><span>شروع شیفت</span><strong>{new Date(currentShift.openedAt).toLocaleString('fa-IR')}</strong></div>
            <div className="info-row"><span>وجه مورد انتظار</span><strong>{money(adjusted)} تومان</strong></div>
            <div className="info-row"><span>وجه شمارش‌شده</span><strong className={difference < 0 ? 'debt-value' : difference > 0 ? 'value green' : ''}>{money(counted)} تومان</strong></div>
            <div className="info-row"><span>اختلاف صندوق</span><strong className={difference === 0 ? 'value green' : difference < 0 ? 'debt-value' : 'value orange'}>{money(difference)} تومان</strong></div>
          </div>
          <div className="modal-grid-2">
            <label>وجه شمارش‌شده (تومان)<input autoFocus inputMode="numeric" value={countedCash} onChange={event => setCountedCash(event.target.value)} placeholder="مثلاً ۳٬۲۰۰٬۰۰۰" /></label>
            <label>تطبیق نقدی خارج از سیستم<input inputMode="numeric" value={manualCash} onChange={event => setManualCash(event.target.value)} placeholder="۰" /></label>
          </div>
          <label>یادداشت تحویل شیفت<textarea rows={4} value={handoverNote} onChange={event => setHandoverNote(event.target.value)} placeholder="مشکل دستگاه، بدهی پیگیری‌نشده، سفارش باز، وجه دستی یا هر نکته‌ای که باید به شیفت بعد منتقل شود…" /></label>
          <div className="modal-actions">
            <button className="btn primary" onClick={() => void closeShift()}>ثبت تسویه و بستن شیفت</button>
            <button className="btn" onClick={() => setCloseShiftOpen(false)}>انصراف</button>
          </div>
        </section>
      </div>;
    })()}
    {payUserId && <div className="modal-backdrop"><section className="operation-modal"><button className="modal-close" onClick={() => setPayUserId(null)}>×</button><h2>حساب پرسنل · {users.find(item => item.id === payUserId)?.name}</h2><label>عملیات<select value={payMode} onChange={event => setPayMode(event.target.value as typeof payMode)}><option value="salary">پرداخت حقوق</option><option value="bonus">پاداش</option><option value="deduction">کسری مصوب</option><option value="advance">مساعده</option><option value="damage">ثبت خسارت</option></select></label><label>مبلغ (تومان)<input autoFocus inputMode="numeric" value={payAmount} onChange={event => setPayAmount(event.target.value)} /></label><label>دلیل<input value={payReason} onChange={event => setPayReason(event.target.value)} placeholder="دلیل و توضیح عملیات…" /></label><div className="modal-actions"><button className="btn primary" onClick={applyPayAction}>ثبت عملیات</button><button className="btn" onClick={() => setPayUserId(null)}>انصراف</button></div><small className="security-footnote">Ledger حقوقی روی Server ثبت می‌شود؛ عملیات حساس تا تأیید مجاز وارد مانده حساب نمی‌شود.</small></section></div>}
    {draft && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setDraft(null)}><section className="operation-modal"><button className="modal-close" onClick={() => setDraft(null)}>×</button><h2>{users.some(user => user.id === draft.id) ? 'ویرایش کاربر و حقوق' : 'کاربر جدید'}</h2><label>نام<input value={draft.name} onChange={event => setDraft({...draft,name:event.target.value})} /></label><label>نقش<select value={draft.role} onChange={event => setDraft({...draft,role:event.target.value as UserRecord['role']})}><option value="owner">صاحب</option><option value="admin">مدیر</option><option value="operator">اپراتور</option></select></label><label>شیفت<input value={draft.shift} onChange={event => setDraft({...draft,shift:event.target.value})} /></label><div className="modal-grid-2"><label>نوع حقوق<select value={draft.payType ?? 'hourly'} onChange={event => setDraft({...draft,payType:event.target.value as UserRecord['payType']})}><option value="hourly">ساعتی</option><option value="monthly">ماهانه</option></select></label><label>نرخ ساعتی<input inputMode="numeric" value={draft.hourlyRate ?? 0} onChange={event => setDraft({...draft,hourlyRate:Number(event.target.value.replace(/[۰-۹]/g,d=>String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g,''))||0})} /></label></div><label>حقوق ماهانه<input inputMode="numeric" value={draft.monthlySalary ?? 0} onChange={event => setDraft({...draft,monthlySalary:Number(event.target.value.replace(/[۰-۹]/g,d=>String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g,''))||0})} /></label><div className="modal-grid-2"><label>شروع کار<input type="time" value={draft.workStart ?? ''} onChange={event => setDraft({...draft,workStart:event.target.value})} /></label><label>پایان کار<input type="time" value={draft.workEnd ?? ''} onChange={event => setDraft({...draft,workEnd:event.target.value})} /></label></div><div className="modal-actions"><button className="btn primary" onClick={() => void saveUser()}>ذخیره</button><button className="btn" onClick={() => setDraft(null)}>انصراف</button></div></section></div>}
    {notice && <div className="operation-toast">{notice}<button onClick={() => setNotice('')}>×</button></div>}
  </>;
}
