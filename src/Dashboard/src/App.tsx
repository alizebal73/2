import { useEffect, useState } from 'react'
import { HubConnectionBuilder } from '@microsoft/signalr'
import './App.css'

type StationDto = {
  id: string
  name: string
  zone: string
  type: string
  ratePerHour: number
  state: string
}

type DashboardSnapshotDto = {
  totalStations: number
  stations: StationDto[]
  generatedAt: string
}

type ServerInfoDto = {
  name: string
  environment: string
  utcNow: string
}

type HubState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

const zoneLabels: Record<string, string> = {
  all: 'همه ایستگاه‌ها',
  pc: 'رایانه‌ها',
  console: 'کنسول‌ها',
  table: 'میزها',
}

const stateLabels: Record<string, string> = {
  free: 'آزاد',
  busy: 'در حال بازی',
  reserved: 'رزرو',
  off: 'خارج از سرویس',
}

function formatMoney(amount: number) {
  return new Intl.NumberFormat('fa-IR').format(amount)
}

function App() {
  const [snapshot, setSnapshot] = useState<DashboardSnapshotDto | null>(null)
  const [serverInfo, setServerInfo] = useState<ServerInfoDto | null>(null)
  const [apiState, setApiState] = useState<'loading' | 'online' | 'offline'>('loading')
  const [hubState, setHubState] = useState<HubState>('connecting')
  const [error, setError] = useState('')
  const [zone, setZone] = useState('all')
  const [retry, setRetry] = useState(0)

  useEffect(() => {
    let active = true
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/dashboard')
      .withAutomaticReconnect()
      .build()

    connection.on('ServerReady', (info: ServerInfoDto) => {
      if (active) setServerInfo(info)
    })
    connection.onreconnecting(() => { if (active) setHubState('reconnecting') })
    connection.onreconnected(() => { if (active) setHubState('connected') })
    connection.onclose(() => { if (active) setHubState('disconnected') })

    const loadSnapshot = async () => {
      setApiState('loading')
      setError('')
      try {
        const response = await fetch('/api/dashboard')
        if (!response.ok) throw new Error(`API returned ${response.status}`)
        const data = (await response.json()) as DashboardSnapshotDto
        if (active) {
          setSnapshot(data)
          setApiState('online')
        }
      } catch (cause) {
        if (active) {
          setApiState('offline')
          setError(cause instanceof Error ? cause.message : 'ارتباط با سرور ناموفق بود')
        }
      }
    }

    void loadSnapshot()
    const startTimer = window.setTimeout(() => {
      startPromise = connection.start()
        .then(() => { if (active) setHubState('connected') })
        .catch(() => { if (active) setHubState('disconnected') })
    }, 0)
    let startPromise: Promise<void> | undefined

    return () => {
      active = false
      window.clearTimeout(startTimer)
      if (startPromise) {
        void startPromise.finally(() => connection.stop())
      } else {
        void connection.stop()
      }
    }
  }, [retry])

  const stations = snapshot?.stations ?? []
  const visibleStations = zone === 'all' ? stations : stations.filter((station) => station.zone === zone)
  const count = (state: string) => stations.filter((station) => station.state === state).length

  return (
    <main className="dashboard" dir="rtl">
      <header className="topbar">
        <div className="brand-mark">گ</div>
        <div className="brand-copy"><strong>گیم‌نت منیجر</strong><span>داشبورد مدیریت</span></div>
        <div className="connection-list" aria-live="polite">
          <span className={`connection ${apiState}`}><i /> API {apiState === 'online' ? 'متصل' : apiState === 'loading' ? 'در حال اتصال' : 'قطع'}</span>
          <span className={`connection ${hubState}`}><i /> SignalR {hubState === 'connected' ? 'متصل' : hubState === 'connecting' ? 'در حال اتصال' : hubState === 'reconnecting' ? 'اتصال مجدد' : 'قطع'}</span>
        </div>
        <button className="refresh-button" type="button" onClick={() => setRetry((value) => value + 1)}>تلاش مجدد</button>
      </header>

      <section className="page-heading">
        <div><p className="eyebrow">وضعیت زنده</p><h1>ایستگاه‌ها</h1></div>
        <div className="server-meta"><span>{serverInfo?.environment ?? '—'}</span><span>{snapshot ? `آخرین دریافت ${new Date(snapshot.generatedAt).toLocaleTimeString('fa-IR')}` : 'در انتظار دریافت داده'}</span></div>
      </section>

      {error && <div className="error-banner" role="alert">ارتباط API برقرار نشد: {error}. سرور را روی پورت ۵۰۸۰ اجرا کنید.</div>}

      <section className="summary" aria-label="خلاصه ایستگاه‌ها">
        <div className="summary-item"><span>کل ایستگاه‌ها</span><strong>{formatMoney(snapshot?.totalStations ?? 0)}</strong></div>
        <div className="summary-item"><span>آزاد</span><strong className="free-text">{formatMoney(count('free'))}</strong></div>
        <div className="summary-item"><span>در حال بازی</span><strong className="busy-text">{formatMoney(count('busy'))}</strong></div>
        <div className="summary-item"><span>خارج از سرویس</span><strong className="off-text">{formatMoney(count('off'))}</strong></div>
      </section>

      <nav className="zone-filter" aria-label="فیلتر زون">
        {Object.entries(zoneLabels).map(([key, label]) => <button key={key} className={zone === key ? 'selected' : ''} type="button" onClick={() => setZone(key)}>{label}</button>)}
        <span className="result-count">{formatMoney(visibleStations.length)} ایستگاه</span>
      </nav>

      {apiState === 'loading' && <p className="empty-state">در حال دریافت DTO از سرور…</p>}
      {apiState === 'online' && visibleStations.length === 0 && <p className="empty-state">ایستگاهی برای نمایش وجود ندارد.</p>}
      <section className="station-grid" aria-label="ایستگاه‌های گیم‌نت">
        {visibleStations.map((station) => <article className={`station ${station.state}`} key={station.id}>
          <div className="station-top"><strong>{station.name}</strong><span className={`state-badge ${station.state}`}>{stateLabels[station.state] ?? station.state}</span></div>
          <span className="station-type">{station.type}</span>
          <div className="station-rate">{formatMoney(station.ratePerHour)} <small>تومان / ساعت</small></div>
        </article>)}
      </section>
      <footer className="status-footer">{snapshot ? `${formatMoney(snapshot.stations.length)} DTO از سرور دریافت شد` : 'اتصال به سرور لازم است'} · {serverInfo?.name ?? 'GameNet Manager'}</footer>
    </main>
  )
}

export default App
