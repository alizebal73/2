import { useEffect, useState } from 'react';
import { mockService } from '../services/mockService';

type AppSettings = {
  viewMode:'v-card'|'v-compact'|'v-list'; zones:boolean; liveCost:boolean; progress:boolean; largeFont:boolean;
  alarmEnd:boolean; alarmFive:boolean; repeatAlarm:boolean; sound:boolean; popup:boolean;
  sessionMode:'settle'|'prepaid'; autoRound:boolean; confirmDelete:boolean; autoPrint:boolean; operatorDiscount:number;
  backupAuto:boolean; backupHour:string; backupKeep:number; backupTarget:string;
  dns:string; serverAddress:string; offlineMode:boolean; wol:boolean; theme:string; accent:string; calendar:string; currency:string;
};
const defaults:AppSettings={viewMode:'v-card',zones:true,liveCost:true,progress:true,largeFont:false,alarmEnd:true,alarmFive:true,repeatAlarm:true,sound:true,popup:true,sessionMode:'settle',autoRound:true,confirmDelete:true,autoPrint:false,operatorDiscount:10,backupAuto:true,backupHour:'04:00',backupKeep:30,backupTarget:'App_Data/Backups',dns:'178.22.122.100',serverAddress:'192.168.1.10:5080',offlineMode:true,wol:true,theme:'تیره',accent:'سبز',calendar:'شمسی',currency:'تومان'};
const hotkeyDefaults={flow:'F1',amount:'F4',walletAdd:'F5',debtAdd:'F6',walletDeduct:'F7',walletDebt:'F8',buffet:'F3',reports:'F2',closeShift:'F9'};
function read<T>(key:string,fallback:T):T{try{const x=localStorage.getItem(key);return x?JSON.parse(x) as T:fallback}catch{return fallback}}
export function SettingsPage(){
 const [settings,setSettings]=useState<AppSettings>(()=>read('gamenet-settings-v1',defaults));
 const [hotkeys,setHotkeys]=useState<Record<string,string>>(()=>read('gamenet-hotkeys-v1',hotkeyDefaults));
 const [notice,setNotice]=useState('');
 useEffect(()=>{localStorage.setItem('gamenet-settings-v1',JSON.stringify(settings));window.dispatchEvent(new CustomEvent('gamenet-settings-changed',{detail:settings}))},[settings]);
 useEffect(()=>{localStorage.setItem('gamenet-hotkeys-v1',JSON.stringify(hotkeys));window.dispatchEvent(new CustomEvent('gamenet-hotkeys-changed',{detail:hotkeys}))},[hotkeys]);
 function update<K extends keyof AppSettings>(key:K,value:AppSettings[K]){setSettings(current=>({...current,[key]:value}))}
 function changeHotkey(key:string){const next=window.prompt('کلید جدید را وارد کنید',hotkeys[key]);if(next?.trim())setHotkeys(current=>({...current,[key]:next.trim()}))}
 function backupNow(){void mockService.createBackup().then(payload=>{const blob=new Blob([JSON.stringify({settings,hotkeys,payload},null,2)],{type:'application/json'});const link=document.createElement('a');link.href=URL.createObjectURL(blob);link.download='gamenet-backup.json';link.click();URL.revokeObjectURL(link.href);setNotice('نسخه پشتیبان ایجاد شد')})}
 function restoreBackup(){const input=document.createElement('input');input.type='file';input.accept='.json,application/json';input.onchange=async()=>{try{const file=input.files?.[0];if(!file) return;const backup=JSON.parse((await file.text()).replace(/^\\uFEFF/,''));if(!backup.payload)throw new Error('invalid');await mockService.restoreBackup(backup.payload);if(backup.settings)setSettings(backup.settings);if(backup.hotkeys)setHotkeys(backup.hotkeys);setNotice('پشتیبان بازیابی شد؛ یکبار صفحه را تازه‌سازی کنید')}catch{setNotice('فایل پشتیبان معتبر نیست')}};input.click()}
 function reset(){setSettings(defaults);setHotkeys(hotkeyDefaults);setNotice('تنظیمات به حالت پیش‌فرض بازگشت')}
 const groups=[
  ['🖥 نوع نمایش داشبورد',[['zones','زون‌بندی گرید'],['liveCost','نمایش هزینه لحظه‌ای'],['progress','نمایش نوار پیشرفت'],['largeFont','فونت بزرگ‌تر']]],
  ['🔊 هشدارها و صدا',[['alarmEnd','هشدار پایان وقت'],['alarmFive','هشدار ۵ دقیقه مانده'],['repeatAlarm','تکرار زنگ هر ۳۰ ثانیه'],['sound','صدای هشدار'],['popup','اعلان پاپ‌آپ']]],
  ['🧾 رفتار فاکتور و تسویه',[['autoRound','رند خودکار به ۱۰۰۰ تومان'],['confirmDelete','تأیید دو مرحله‌ای حذف'],['autoPrint','چاپ خودکار فاکتور']]],
  ['🌐 شبکه و اتصال',[['offlineMode','حالت آفلاین کامل'],['wol','Wake-on-LAN']]],
 ];
 return <>
  <div className="page-header"><div><p>تنظیمات و زیرساخت</p><h1>تنظیمات</h1></div></div>
  <section className="card-panel" style={{margin:'0 22px 14px',padding:14}}><h3>⌨️ هات‌کی‌ها (قابل تغییر)</h3><div style={{display:'grid',gridTemplateColumns:'repeat(auto-fit,minmax(190px,1fr))',gap:7}}>{Object.entries(hotkeys).map(([key,value])=><button className="btn" key={key} onClick={()=>changeHotkey(key)} style={{justifyContent:'space-between'}}><span>{({flow:'فلوی سرعت',amount:'رفتن به مبلغ',walletAdd:'شارژ مستقیم',debtAdd:'ثبت بدهی',walletDeduct:'کسر از کیف پول',walletDebt:'کسر کیف پول + بدهی',buffet:'رفتن به بوفه',reports:'رفتن به گزارش‌ها',closeShift:'بستن صندوق'} as Record<string,string>)[key]||key}</span><kbd>{value}</kbd></button>)}</div></section>
  <div style={{display:'grid',gridTemplateColumns:'repeat(auto-fit,minmax(330px,1fr))',gap:14,padding:'0 22px 30px'}}>
   {groups.map(([title,items])=><section className="card-panel" key={title} style={{padding:14}}><h3>{title}</h3>{(items as [keyof AppSettings,string][]).map(([key,label])=><label className="setting-item" key={String(key)}><span><b>{label}</b></span><input type="checkbox" checked={Boolean(settings[key])} onChange={event=>update(key,event.target.checked as AppSettings[typeof key])}/></label>)}</section>)}
   <section className="card-panel" style={{padding:14}}><h3>🧾 رفتار جلسه</h3><label>حالت پیش‌فرض<select value={settings.sessionMode} onChange={e=>update('sessionMode',e.target.value as AppSettings['sessionMode'])}><option value="settle">تسویه بعد از بازی</option><option value="prepaid">پیش‌پرداخت</option></select></label><label>سقف تخفیف آزاد اپراتور<input type="number" value={settings.operatorDiscount} onChange={e=>update('operatorDiscount',Number(e.target.value))}/></label></section>
   <section className="card-panel" style={{padding:14}}><h3>💾 داده و پشتیبان‌گیری</h3><label>بکاپ خودکار<input type="checkbox" checked={settings.backupAuto} onChange={e=>update('backupAuto',e.target.checked)}/></label><label>ساعت بکاپ<input type="time" value={settings.backupHour} onChange={e=>update('backupHour',e.target.value)}/></label><label>تعداد نسخه<input type="number" min="1" value={settings.backupKeep} onChange={e=>update('backupKeep',Number(e.target.value))}/></label><label>مقصد<input value={settings.backupTarget} onChange={e=>update('backupTarget',e.target.value)}/></label><div className="modal-actions"><button className="btn primary" onClick={backupNow}>📦 بکاپ دستی الان</button><button className="btn" onClick={restoreBackup}>♻️ بازیابی از نسخه</button></div></section>
   <section className="card-panel" style={{padding:14}}><h3>🛠 مدیریت بازی‌ها و کلاینت‌ها</h3><button className="btn" onClick={()=>window.dispatchEvent(new CustomEvent('gamenet-navigate',{detail:'games'}))}>🎮 صفحه بازی‌ها</button><button className="btn" onClick={()=>window.dispatchEvent(new CustomEvent('gamenet-navigate',{detail:'client-shell'}))}>🖧 صفحه کلاینت‌ها</button></section>
   <section className="card-panel" style={{padding:14}}><h3>🌐 شبکه</h3><label>DNS پیش‌فرض<input value={settings.dns} onChange={e=>update('dns',e.target.value)}/></label><label>آدرس سرور<input className="ltr" value={settings.serverAddress} onChange={e=>update('serverAddress',e.target.value)}/></label><div className="setting-item"><span>تعداد صندوق هم‌زمان</span><b>نامحدود</b></div></section>
   <section className="card-panel" style={{padding:14}}><h3>🎨 ظاهر</h3><label>تم<select value={settings.theme} onChange={e=>update('theme',e.target.value)}><option>تیره</option></select></label><label>رنگ تأکید<select value={settings.accent} onChange={e=>update('accent',e.target.value)}><option>سبز</option><option>آبی</option><option>بنفش</option></select></label><label>تقویم<select value={settings.calendar} onChange={e=>update('calendar',e.target.value)}><option>شمسی</option><option>میلادی</option></select></label><label>واحد پول<select value={settings.currency} onChange={e=>update('currency',e.target.value)}><option>تومان</option></select></label><button className="btn danger" onClick={reset}>بازگردانی پیش‌فرض</button></section>
  </div>
  {notice&&<div className="operation-toast">{notice}<button onClick={()=>setNotice('')}>×</button></div>}
 </>;
}
