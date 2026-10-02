import { FormEvent, useState } from 'react';
import { login } from '../services/authService';
import type { AppUserRecord } from '../types';

type Props = { onLoggedIn: (user: AppUserRecord) => void };

export function LoginPage({ onLoggedIn }: Props) {
  const [userName, setUserName] = useState('admin');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError('');
    setBusy(true);
    try {
      onLoggedIn(await login(userName, password));
    } catch {
      setError('نام کاربری یا رمز عبور نادرست است.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="app-shell" dir="rtl" style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', padding: 24 }}>
      <form onSubmit={submit} className="card-panel" style={{ width: 'min(420px, 100%)', padding: 28, display: 'grid', gap: 14 }}>
        <div>
          <strong style={{ fontSize: 22 }}>گیم‌نت منیجر</strong>
          <p style={{ margin: '6px 0 0', opacity: .7 }}>ورود اپراتور و مدیر سیستم</p>
        </div>
        {error && <div className="user-error-banner" role="alert"><div className="user-error-copy"><strong>ورود ناموفق</strong><span>{error}</span></div></div>}
        <label>نام کاربری<input autoFocus value={userName} onChange={event => setUserName(event.target.value)} autoComplete="username" /></label>
        <label>رمز عبور<input type="password" value={password} onChange={event => setPassword(event.target.value)} autoComplete="current-password" /></label>
        <button className="btn primary" type="submit" disabled={busy}>{busy ? 'در حال ورود…' : 'ورود به داشبورد'}</button>
      </form>
    </main>
  );
}
