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
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);

  useEffect(() => {
    void mockService.getGames().then(setGames);
  }, []);

  const visibleGames = useMemo(() => games.filter(game => {
    const matchesFilter = filter === 'all' || game.status === filter;
    const matchesQuery = `${game.name} ${game.category} ${game.path}`.toLowerCase().includes(query.trim().toLowerCase());
    return matchesFilter && matchesQuery;
  }), [games, filter, query]);
  const categories = new Set(games.map(game => game.category)).size;
  const selectedGame = games.find(game => game.id === selectedGameId) ?? visibleGames[0] ?? null;

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

      <div className="games-master-detail">
        <section className="games-list-panel">
          <div className="games-list-head"><strong>فهرست بازی‌ها</strong><span>{visibleGames.length.toLocaleString('fa-IR')} مورد</span></div>
          <div className="games-list">
            {visibleGames.map(game => (
              <button type="button" key={game.id} className={'game-list-row ' + (selectedGame?.id === game.id ? 'active' : '')} onClick={() => setSelectedGameId(game.id)}>
                <span className="game-list-cover">{game.cover || '🎮'}</span>
                <span><strong>{game.name}</strong><small>{game.category} · {game.activeUsers.toLocaleString('fa-IR')} کاربر فعال</small></span>
                <em>{game.active ? 'فعال' : 'مخفی'}</em>
              </button>
            ))}
          </div>
        </section>
        <section className="game-detail-panel">
          {selectedGame ? <>
            <div className="game-detail-hero"><div className="game-detail-cover">{selectedGame.cover || '🎮'}</div><div><span className="game-detail-kicker">{selectedGame.category}</span><h2>{selectedGame.name}</h2><p>{selectedGame.version || 'بدون نسخه ثبت‌شده'} · {selectedGame.status === 'online' ? 'آنلاین' : selectedGame.status === 'offline' ? 'آفلاین' : 'برنامه'}</p></div></div>
            <div className="game-detail-grid">
              <div className="info-row"><span>مسیر نصب</span><strong className="ltr">{selectedGame.path || '—'}</strong></div>
              <div className="info-row"><span>فایل اجرایی</span><strong className="ltr">{selectedGame.executable || '—'}</strong></div>
              <div className="info-row"><span>پارامتر اجرا</span><strong className="ltr">{selectedGame.launchArgs || '—'}</strong></div>
              <div className="info-row"><span>نوع سیستم</span><strong>{selectedGame.targetSystem === 'vip' ? 'VIP' : selectedGame.targetSystem === 'standard' ? 'عادی' : 'همه'}</strong></div>
              <div className="info-row"><span>اعمال به</span><strong>{selectedGame.target === 'all' ? 'همه رایانه‌ها' : selectedGame.target === 'zone' ? selectedGame.targetZone : selectedGame.targetStations || 'ایستگاه‌های منتخب'}</strong></div>
              <div className="info-row"><span>کاربران فعال</span><strong>{selectedGame.activeUsers.toLocaleString('fa-IR')}</strong></div>
            </div>
            <div className="game-detail-actions"><button className="btn primary" onClick={() => setDraft({ ...selectedGame })}>ویرایش تنظیمات</button><button className="btn" onClick={() => void applyGames([selectedGame.id])}>اعمال به کلاینت‌ها</button><button className="btn danger" onClick={() => void deleteGame(selectedGame)}>حذف بازی</button></div>
          </> : <div className="games-empty">بازی‌ای برای نمایش انتخاب نشده است.</div>}
        </section>
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
