import { useEffect, useState } from 'react';
import { mockService } from '../services/mockService';
import type { SettingGroup } from '../types';

export function SettingsPage() {
  const [settings, setSettings] = useState<SettingGroup[]>([]);

  useEffect(() => {
    void mockService.getSettings().then(setSettings);
  }, []);

  return (
    <>
      <div className="page-header">
        <div>
          <p>تنظیمات سیستم</p>
          <h1>تنظیمات</h1>
        </div>
      </div>

      <div className="setting-list">
        {settings.map((item) => (
          <div key={item.id} className="setting-item">
            <div>
              <h4>{item.title}</h4>
              <p>{item.description}</p>
            </div>
            <div className={item.enabled ? 'switch on' : 'switch'} />
          </div>
        ))}
      </div>
    </>
  );
}
