import { useEffect, useState } from 'react';
import type { PageKey, PageLockMap } from '../types';
import { hashPin, protectedPageLabels, readPageLocks, writePageLocks } from '../services/securityService';

type AppSettings = {
  viewMode:'v-card'|'v-compact'|'v-list';
  payrollMode?: 'hourly'|'monthly'; shortagePolicy?: 'approval'|'payroll'|'expense'; autoPayrollDeduction?: boolean; zones:boolean; liveCost:boolean; progress:boolean; largeFont:boolean;
  alarmEnd:boolean; alarmFive:boolean; repeatAlarm:boolean; sound:boolean; popup:boolean;
  sessionMode:'settle'|'prepaid'; autoRound:boolean; confirmDelete:boolean; autoPrint:boolean; operatorDiscount:number;
  backupAuto:boolean; backupHour:string; backupKeep:number; backupTarget:string;
  dns:string; serverAddress:string; offlineMode:boolean; wol:boolean; theme:string; accent:string; calendar:string; currency:string;
};
const defaults:AppSettings={viewMode:'v-card',payrollMode:'hourly',shortagePolicy:'approval',autoPayrollDeduction:false,zones:true,liveCost:true,progress:true,largeFont:false,alarmEnd:true,alarmFive:true,repeatAlarm:true,sound:true,popup:true,sessionMode:'settle',autoRound:true,confirmDelete:true,autoPrint:false,operatorDiscount:10,backupAuto:true,backupHour:'04:00',backupKeep:30,backupTarget:'App_Data/Backups',dns:'178.22.122.100',serverAddress:'192.168.1.10:5080',offlineMode:true,wol:true,theme:'تیره',accent:'سبز',calendar:'شمسی',currency:'تومان'};
const hotkeyDefaults={flow:'F1',amount:'F4',walletAdd:'F5',debtAdd:'F6',walletDeduct:'F7',walletDebt:'F8',buffet:'F3',reports:'F2',closeShift:'F9'};
type SettingsCategory = 'dashboard'|'sessions'|'alerts'|'network'|'backup'|'security'|'staff'|'client'|'appearance'|'hotkeys';
const settingsCategories: Array<{id:SettingsCategory,title:string,keywords:string}> = [
 ['dashboard','داشبورد و نمایش','گرید زون هزینه پیشرفت فونت کارت'],
 ['sessions','جلسه و تسویه','جلسه فاکتور تسویه تخفیف چاپ حذف'],
 ['alerts','هشدارها و اعلان‌ها','هشدار صدا اعلان پایان وقت'],
 ['network','شبکه و اتصال','DNS سرور آفلاین Wake-on-LAN'],
 ['backup','داده و پشتیبان‌گیری','بکاپ پشتیبان بازیابی دیتابیس'],
 ['security','امنیت و دسترسی','قفل رمز PIN Permission'],
 ['staff','کاربران و شیفت','حقوق پرسنل Payroll کسری صندوق'],
 ['client','بازی و کلاینت','بازی کلاینت Agent Kiosk'],
 ['appearance','ظاهر و محلی‌سازی','تم رنگ تقویم تومان'],
 ['hotkeys','میانبرها','هات‌کی کلید F1 F2 F3 F4 F5 F6 F7 F8 F9'],
].map(([id,title,keywords])=>({id:id as SettingsCategory,title,keywords}));

function read<T>(key:string,fallback:T):T{try{const x=localStorage.getItem(key);return x?JSON.parse(x) as T:fallback}catch{return fallback}}
export function SettingsPage(){
 const [settings,setSettings]=useState<AppSettings>(()=>read('gamenet-settings-v1',defaults));
 const [hotkeys,setHotkeys]=useState<Record<string,string>>(()=>read('gamenet-hotkeys-v1',hotkeyDefaults));
 const [notice,setNotice]=useState('');
 const [pageLocks,setPageLocks]=useState<PageLockMap>(()=>readPageLocks());
 const [lockPins,setLockPins]=useState<Record<string,string>>({});
 const [settingsSearch,setSettingsSearch]=useState('');
 useEffect(()=>{localStorage.setItem('gamenet-settings-v1',JSON.stringify(settings));window.dispatchEvent(new CustomEvent('gamenet-settings-changed',{detail:settings}))},[settings]);
 useEffect(()=>{localStorage.setItem('gamenet-hotkeys-v1',JSON.stringify(hotkeys));window.dispatchEvent(new CustomEvent('gamenet-hotkeys-changed',{detail:hotkeys}))},[hotkeys]);
 function update<K extends keyof AppSettings>(key:K,value:AppSettings[K]){setSettings(current=>({...current,[key]:value}))}
 function changeHotkey(key:string){const next=window.prompt('کلید جدید را وارد کنید',hotkeys[key]);if(next?.trim())setHotkeys(current=>({...current,[key]:next.trim()}))}
 function reset(){setSettings(defaults);setHotkeys(hotkeyDefaults);setNotice('تنظیمات به حالت پیش‌فرض بازگشت')}
 async function saveSectionLocks(){
  const next: PageLockMap = {};
  for(const [page, rule] of Object.entries(pageLocks) as [PageKey, NonNullable<PageLockMap[PageKey]>][]){
   if(!rule?.enabled) continue;
   const newPin = (lockPins[page] ?? '').trim();
   let pinHash = rule.pinHash;
   if(newPin){ if(newPin.length < 4){ setNotice('رمز هر بخش باید حداقل ۴ رقم یا نویسه داشته باشد'); return; } pinHash = await hashPin(newPin); }
   if(pinHash) next[page] = { enabled: true, pinHash, label: rule.label || protectedPageLabels[page] || page };
  }
  setPageLocks(next); writePageLocks(next); setLockPins({}); setNotice('قفل بخش‌ها ذخیره شد');
 }
 const groups: Array<[string, Array<[keyof AppSettings,string]>]>=[
  ['🖥 نوع نمایش داشبورد',[['zones','زون‌بندی گرید'],['liveCost','نمایش هزینه لحظه‌ای'],['progress','نمایش نوار پیشرفت'],['largeFont','فونت بزرگ‌تر']]],
  ['🔊 هشدارها و صدا',[['alarmEnd','هشدار پایان وقت'],['alarmFive','هشدار ۵ دقیقه مانده'],['repeatAlarm','تکرار زنگ هر ۳۰ ثانیه'],['sound','صدای هشدار'],['popup','اعلان پاپ‌آپ']]],
  ['🧾 رفتار فاکتور و تسویه',[['autoRound','رند خودکار به ۱۰۰۰ تومان'],['confirmDelete','تأیید دو مرحله‌ای حذف'],['autoPrint','چاپ خودکار فاکتور']]],
  ['🌐 شبکه و اتصال',[['offlineMode','حالت آفلاین کامل'],['wol','Wake-on-LAN']]],
 ];
 return <>
  <div className="page-header"><div><p>تنظیمات و زیرساخت</p><h1>تنظیمات</h1></div></div>
  <section className="settings-ia-toolbar" data-testid="settings-category-nav">
   <div className="settings-search-wrap">
    <label htmlFor="settings-search">جست‌وجوی تنظیمات</label>
    <div className="settings-search-row">
      <input id="settings-search" value={settingsSearch} onChange={event=>setSettingsSearch(event.target.value)} placeholder="مثلاً بکاپ، تخفیف، DNS، قفل یا هات‌کی" />
      {settingsSearch && <button type="button" className="btn sm" onClick={()=>setSettingsSearch('')}>پاک کردن</button>}
    </div>
   </div>
   <div className="settings-category-nav" aria-label="دسته‌بندی تنظیمات">
    {settingsCategories
      .filter(category=>!settingsSearch.trim() || (category.title+' '+category.keywords).includes(settingsSearch.trim()))
      .map(category=>(
        <button
          type="button"
          className="settings-category-button"
          key={category.id}
          onClick={()=>{
            document.getElementById('settings-section-'+category.id)?.scrollIntoView({behavior:'smooth',block:'start'});
          }}
        >
          <strong>{category.title}</strong>
          <span>{category.keywords.split(' ')[0]}</span>
        </button>
      ))}
   </div>
   <small className="settings-local-notice">تنظیمات فعلی این صفحه در localStorage این داشبورد نگهداری می‌شوند؛ تنظیمات عملیاتی Server-backed در مسیر hardening بعدی قرار دارند.</small>
  </section>
  <section id="settings-section-hotkeys" className="card-panel" style={{margin:'0 22px 14px',padding:14}}><h3>⌨️ هات‌کی‌ها (قابل تغییر)</h3><div style={{display:'grid',gridTemplateColumns:'repeat(auto-fit,minmax(190px,1fr))',gap:7}}>{Object.entries(hotkeys).map(([key,value])=><button className="btn" key={key} onClick={()=>changeHotkey(key)} style={{justifyContent:'space-between'}}><span>{({flow:'فلوی سرعت',amount:'رفتن به مبلغ',walletAdd:'شارژ مستقیم',debtAdd:'ثبت بدهی',walletDeduct:'کسر از کیف پول',walletDebt:'کسر کیف پول + بدهی',buffet:'رفتن به بوفه',reports:'رفتن به گزارش‌ها',closeShift:'بستن صندوق'} as Record<string,string>)[key]||key}</span><kbd>{value}</kbd></button>)}</div></section>
  <div style={{display:'grid',gridTemplateColumns:'repeat(auto-fit,minmax(330px,1fr))',gap:14,padding:'0 22px 30px'}}>
   {groups.map(([title,items])=><section id={title.includes('نمایش')?'settings-section-dashboard':title.includes('هشدار')?'settings-section-alerts':title.includes('فاکتور')?'settings-section-settlement':'settings-section-network-options'} className="card-panel" key={title} style={{padding:14}}><h3>{title}</h3>{(items as [keyof AppSettings,string][]).map(([key,label])=><label className="setting-item" key={String(key)}><span><b>{label}</b></span><input type="checkbox" checked={Boolean(settings[key])} onChange={event=>update(key,event.target.checked as AppSettings[typeof key])}/></label>)}</section>)}
   <section id="settings-section-security" className="card-panel settings-security-panel" style={{padding:14}}>
    <div className="settings-section-head"><div><h3>🔐 قفل بخش‌ها</h3><small>برای هر بخش می‌توانی رمز جدا تعیین کنی؛ باز شدن بخش در همان نشست مدیریتی معتبر می‌ماند.</small></div></div>
    <div className="section-lock-grid">
      {Object.entries(protectedPageLabels).map(([page,label]) => {
        const key = page as PageKey;
        const rule = pageLocks[key];
        return <div className="section-lock-row" key={page}>
          <label className="setting-item"><span><b>{label}</b><small>{rule?.enabled ? 'قفل فعال' : 'بدون قفل'}</small></span><input type="checkbox" checked={Boolean(rule?.enabled)} onChange={event => setPageLocks(current => ({ ...current, [key]: { enabled: event.target.checked, pinHash: rule?.pinHash ?? '', label: label as string } }))} /></label>
          {rule?.enabled && <input className="section-lock-pin" type="password" inputMode="numeric" value={lockPins[key] ?? ''} onChange={event => setLockPins(current => ({ ...current, [key]: event.target.value }))} placeholder={rule.pinHash ? 'رمز فعلی محفوظ است؛ در صورت تغییر وارد کنید' : 'رمز اختصاصی این بخش'} />}
        </div>;
      })}
    </div>
    <div className="modal-actions"><button className="btn primary" onClick={() => void saveSectionLocks()}>ذخیره قفل‌ها</button></div>
    <small className="security-footnote">این قفل در حال حاضر لایهٔ UX است؛ در نسخه نهایی Permission و Server Command نیز باید همین دسترسی را کنترل کنند.</small>
   </section>
   <section id="settings-section-sessions" className="card-panel" style={{padding:14}}><h3>🧾 رفتار جلسه</h3><label>حالت پیش‌فرض<select value={settings.sessionMode} onChange={e=>update('sessionMode',e.target.value as AppSettings['sessionMode'])}><option value="settle">تسویه بعد از بازی</option><option value="prepaid">پیش‌پرداخت</option></select></label><label>سقف تخفیف آزاد اپراتور<input type="number" value={settings.operatorDiscount} onChange={e=>update('operatorDiscount',Number(e.target.value))}/></label></section>
   <section id="settings-section-backup" className="card-panel" style={{padding:14}}>
    <h3>💾 داده و پشتیبان‌گیری</h3>
    <label className="setting-item"><span><b>بکاپ خودکار</b><small>تا زمان آماده‌شدن سرویس Backup سرور، این گزینه اجرایی نیست.</small></span><input type="checkbox" checked={false} disabled /></label>
    <label>ساعت بکاپ<input type="time" value={settings.backupHour} disabled /></label>
    <label>تعداد نسخه<input type="number" min="1" value={settings.backupKeep} disabled /></label>
    <label>مقصد<input value={settings.backupTarget} disabled /></label>
    <div className="modal-actions">
      <button className="btn primary" disabled title="پشتیبان واقعی Server-side هنوز در Release Gate پیاده‌سازی نشده است">📦 بکاپ دستی الان</button>
      <button className="btn" disabled title="بازیابی واقعی Server-side هنوز در Release Gate پیاده‌سازی نشده است">♻️ بازیابی از نسخه</button>
    </div>
    <small className="security-footnote">پشتیبان واقعی باید شامل دیتابیس و DataProtection Keys باشد و قبل از Migration قابل‌بازیابی تست شود؛ این کنترل‌ها تا آماده‌شدن مسیر سروری عمداً غیرفعال‌اند.</small>
   </section>
   <section id="settings-section-staff" className="card-panel" style={{padding:14}}><h3>👥 حقوق و شیفت</h3><label>روش محاسبه حقوق پیش‌فرض<select value={settings.payrollMode ?? 'hourly'} onChange={e=>update('payrollMode',e.target.value as AppSettings['payrollMode'])}><option value="hourly">ساعتی</option><option value="monthly">ماهانه</option></select></label><label>رفتار اختلاف صندوق<select value={settings.shortagePolicy ?? 'approval'} onChange={e=>update('shortagePolicy',e.target.value as AppSettings['shortagePolicy'])}><option value="approval">نیازمند تأیید</option><option value="payroll">قابل انتقال به حقوق</option><option value="expense">ثبت به‌عنوان هزینه/کسری</option></select></label><label className="setting-item"><span>کسر خودکار از حقوق</span><input type="checkbox" checked={Boolean(settings.autoPayrollDeduction)} onChange={e=>update('autoPayrollDeduction',e.target.checked)}/></label></section>
   <section id="settings-section-client" className="card-panel" style={{padding:14}}><h3>🛠 مدیریت بازی‌ها و کلاینت‌ها</h3><button className="btn" onClick={()=>window.dispatchEvent(new CustomEvent('gamenet-navigate',{detail:'games'}))}>🎮 صفحه بازی‌ها</button><button className="btn" onClick={()=>window.dispatchEvent(new CustomEvent('gamenet-navigate',{detail:'client-shell'}))}>🖧 صفحه کلاینت‌ها</button></section>
   <section id="settings-section-network" className="card-panel" style={{padding:14}}><h3>🌐 شبکه</h3><label>DNS پیش‌فرض<input value={settings.dns} onChange={e=>update('dns',e.target.value)}/></label><label>آدرس سرور<input className="ltr" value={settings.serverAddress} onChange={e=>update('serverAddress',e.target.value)}/></label><div className="setting-item"><span>تعداد صندوق هم‌زمان</span><b>نامحدود</b></div></section>
   <section id="settings-section-appearance" className="card-panel" style={{padding:14}}><h3>🎨 ظاهر</h3><label>تم<select value={settings.theme} onChange={e=>update('theme',e.target.value)}><option>تیره</option></select></label><label>رنگ تأکید<select value={settings.accent} onChange={e=>update('accent',e.target.value)}><option>سبز</option><option>آبی</option><option>بنفش</option></select></label><label>تقویم<select value={settings.calendar} onChange={e=>update('calendar',e.target.value)}><option>شمسی</option><option>میلادی</option></select></label><label>واحد پول<select value={settings.currency} onChange={e=>update('currency',e.target.value)}><option>تومان</option></select></label><button className="btn danger" onClick={reset}>بازگردانی پیش‌فرض</button></section>
  </div>
  {notice&&<div className="operation-toast">{notice}<button onClick={()=>setNotice('')}>×</button></div>}
 </>;
}
