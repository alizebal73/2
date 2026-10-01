import { useEffect, useState } from 'react';
import { mockService } from '../services/mockService';
import type { UserRecord } from '../types';

function money(value: number) {
  return new Intl.NumberFormat('fa-IR').format(value);
}

export function UsersPage() {
  const [users, setUsers] = useState<UserRecord[]>([]);

  useEffect(() => {
    void mockService.getUsers().then(setUsers);
  }, []);

  return (
    <>
      <div className="page-header">
        <div>
          <p>کاربران و نقش‌ها</p>
          <h1>کاربران</h1>
        </div>
      </div>

      <div className="toolbar">
        <button type="button" className="btn primary">+ افزودن کاربر</button>
      </div>

      <div className="bullet-grid">
        {users.map((user) => (
          <div key={user.id} className="user-card">
            <b>{user.name}</b>
            <div className="meta">نقش: {user.role}</div>
            <div className="meta">شیفت: {user.shift}</div>
            <div className="meta">فروش: {money(user.sales)} تومان</div>
            <div style={{ display: 'flex', gap: '5px', flexWrap: 'wrap', marginTop: '10px' }}>
              {user.permissions.map((permission) => (
                <span key={`${user.id}-${permission}`} className="status-pill free" style={{ display: 'inline-block' }}>{permission}</span>
              ))}
            </div>
          </div>
        ))}
      </div>
    </>
  );
}
