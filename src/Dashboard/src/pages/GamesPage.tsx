import { useEffect, useMemo, useState } from 'react';
import { mockService } from '../services/mockService';
import type { GameRecord } from '../types';

type GameFilter = 'all' | 'online' | 'offline' | 'program';

const emptyGame = (): GameRecord => ({
  id: crypto.randomUUID(),
  name: '',
  version: '',
  category: 'FPS',
  status: 'offline',
  activeUsers: 0,
  path: '',
  executable: '',
  cover: '',
  trailer: '',
  launchArgs: '',
  connectionType: 'آنلاین',
  active: true,
  targetSystem: 'all',
  target: 'all',
  targetZone: 'pc',
  targetStations: '',
});

const filterNames: Record<GameFilter, string> = { all: 'همه', online: 'آنلاین', offline: 'آفلاین', program: 'برنامه' };

export function GamesPage() {
  const [games, setGames] = useState<GameRecord[]>([]);
  const [filter, setFilter] = useState<GameFilter>('all');
  const [query, setQuery] = useState('');
  const [draft, setDraft] = useState<GameRecord | null>(null);
  const [notice, setNotice] = useState('');

  useEffect(() => {
    void mockService.getGames().then(setGames);
  }, []);

  const visibleGames = useMemo(() => games.filter(game => {
    const matchesFilter = filter === 'all' || game.status === filter;
    const matchesQuery = `${game.name} ${game.category} ${game.path}`.toLowerCase().includes(query.trim().toLowerCase());
    return matchesFilter && matchesQuery;
  }), [games, filter, query]);
  const categories = new Set(games.map(game => game.category)).size;

  function patchDraft(update: Partial<GameRecord>) {
    setDraft(current => current ? { ...current, ...update } : current);
  }

  async function saveGame() {
    if (!draft?.name.trim() || !draft.executable.trim()) {
      setNotice('نام بازی و فایل اجرایی الزامی است');
      return;
    }
    await mockService.saveGame(draft);
    setGames(await mockService.getGames());
    setDraft(null);
    setNotice('بازی ذخیره شد');
  }

  async function deleteGame(game: GameRecord) {
    if (!window.confirm(`بازی «${game.name}» حذف شود؟`)) return;
    await mockService.deleteGame(game.id);
    setGames(await mockService.getGames());
    setNotice('بازی حذف شد');
  }

  async function applyGames(gameIds: string[]) {
    await mockService.applyGamesToClients(gameIds);
    setNotice(`اعمال ${gameIds.length} بازی به کلاینت‌ها در صف قرار گرفت`);
  }

  return (
    <>
      <div className="page-header">
        <div>
          <p>بازی‌ها و حساب‌ها</p>
          <h1>بازی‌ها</h1>
        </div>
      </div>

      <div className="summary-grid">
        <div className="summary-card"><div className="label">بازی‌های تعریف‌شده</div><div className="value blue">{games.length}</div></div>
        <div className="summary-card"><div className="label">فعال در کلاینت‌ها</div><div className="value green">{games.filter(game => game.active).reduce((sum, game) => sum + game.activeUsers, 0)}</div></div>
        <div className="summary-card"><div className="label">آنلاین</div><div className="value orange">{games.filter(game => game.status === 'online').length}</div></div>
        <div className="summary-card"><div className="label">دسته‌ها</div><div className="value">{categories}</div></div>
      </div>
      <div className="toolbar game-management-toolbar">
        <div className="search-box"><input value={query} onChange={event => setQuery(event.target.value)} placeholder="جست‌وجوی بازی…" aria-label="جست‌وجوی بازی" /></div>
        <div className="view-switch">{(Object.keys(filterNames) as GameFilter[]).map(key => <button key={key} className={filter === key ? 'active' : ''} onClick={() => setFilter(key)}>{filterNames[key]}</button>)}</div>
        <button type="button" className="btn" onClick={() => void applyGames(games.map(game => game.id))}>📤 اعمال بازی‌ها به همه کلاینت‌ها</button>
        <button type="button" className="btn primary" onClick={() => setDraft(emptyGame())}>+ بازی جدید</button>
      </div>

      <div className="games-grid">
        {visibleGames.map((game) => (
          <div key={game.id} className="game-card">
            <div className="game-cover">{game.cover || '🎮'}<span className={`status-pill ${game.status === 'online' ? 'online' : game.status === 'offline' ? 'offline' : 'free'}`}>{game.status === 'program' ? 'برنامه' : game.status === 'online' ? 'آنلاین' : 'آفلاین'}</span></div>
            <b>{game.name}</b>
            <div className="meta">دسته: {game.category} · نسخه {game.version}</div>
            <div className="meta ltr">{game.path}</div>
            <div className="meta ltr">{game.executable}</div>
            <div className="meta">کاربران فعال: {game.activeUsers}</div>
            <div className="meta">هدف: {game.targetSystem === 'vip' ? 'VIP' : game.targetSystem === 'standard' ? 'عادی' : 'همه'} · {game.active ? 'فعال' : 'مخفی'}</div>
            <div className="game-card-actions">
              <button className="btn sm" onClick={() => setDraft({ ...game })}>ویرایش</button>
              <button className="btn sm" onClick={() => void applyGames([game.id])}>اعمال</button>
              <button className="btn sm danger" onClick={() => void deleteGame(game)}>حذف</button>
            </div>
          </div>
        ))}
      </div>

      {draft && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setDraft(null)}><section className="operation-modal wide" role="dialog" aria-modal="true"><button className="modal-close" onClick={() => setDraft(null)}>×</button><h2>{games.some(game => game.id === draft.id) ? 'ویرایش بازی' : 'بازی جدید'}</h2>
        <div className="game-form-grid">
          <label>نام بازی<input value={draft.name} onChange={event => patchDraft({ name: event.target.value })} /></label>
          <label>دسته<input value={draft.category} onChange={event => patchDraft({ category: event.target.value })} /></label>
          <label>مسیر نصب<input className="ltr" value={draft.path} onChange={event => patchDraft({ path: event.target.value })} /></label>
          <label>فایل اجرایی (.exe)<input className="ltr" value={draft.executable} onChange={event => patchDraft({ executable: event.target.value })} /></label>
          <label>پارامتر اجرا<input className="ltr" value={draft.launchArgs} onChange={event => patchDraft({ launchArgs: event.target.value })} /></label>
          <label>نوع اتصال<select value={draft.connectionType} onChange={event => patchDraft({ connectionType: event.target.value })}><option>آنلاین</option><option>آفلاین</option><option>برنامه</option></select></label>
          <label>وضعیت<select value={draft.status} onChange={event => patchDraft({ status: event.target.value as GameRecord['status'] })}><option value="online">آنلاین</option><option value="offline">آفلاین</option><option value="program">برنامه</option></select></label>
          <label>نوع سیستم<select value={draft.targetSystem} onChange={event => patchDraft({ targetSystem: event.target.value as GameRecord['targetSystem'] })}><option value="all">همه</option><option value="vip">فقط VIP</option><option value="standard">فقط عادی</option></select></label>
          <label>اعمال به<select value={draft.target} onChange={event => patchDraft({ target: event.target.value as GameRecord['target'] })}><option value="all">همه رایانه‌ها</option><option value="zone">زون خاص</option><option value="stations">ایستگاه‌های منتخب</option></select></label>
          {draft.target === 'zone' && <label>زون<select value={draft.targetZone} onChange={event => patchDraft({ targetZone: event.target.value })}><option value="pc">رایانه‌ها</option><option value="console">کنسول‌ها</option><option value="table">میزها</option></select></label>}
          {draft.target === 'stations' && <label>ایستگاه‌ها (با ویرگول جدا شود)<input value={draft.targetStations} onChange={event => patchDraft({ targetStations: event.target.value })} placeholder="PC-01, PS5-03" /></label>}
          <label>Cover<input type="file" accept="image/*" onChange={event => patchDraft({ cover: event.target.files?.[0]?.name ?? draft.cover })} /></label>
          <label>Trailer<input type="file" accept="video/*" onChange={event => patchDraft({ trailer: event.target.files?.[0]?.name ?? draft.trailer })} /></label>
          <label className="game-active-setting"><input type="checkbox" checked={draft.active} onChange={event => patchDraft({ active: event.target.checked })} /> فعال در کلاینت</label>
        </div>
        <div className="modal-actions"><button className="btn primary" onClick={() => void saveGame()}>ذخیره</button><button className="btn" onClick={() => setDraft(null)}>انصراف</button></div>
      </section></div>}
      {notice && <div className="operation-toast" role="status">{notice}<button onClick={() => setNotice('')}>×</button></div>}
    </>
  );
}
