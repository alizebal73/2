import { useEffect, useMemo, useRef, useState } from 'react';
import { getAgentCommand, getAgentStatuses, requestAgentRollback, requestAgentUpdate, sendAgentCommand, updateAgentPolicy, type AgentCommandType } from '../services/agentService';
import { getServerCustomers } from '../services/customerService';
import { acquireCustomerLogin } from '../services/customerLoginService';
import type { AgentStatusDto, CustomerRecord } from '../types';
import { userErrorMessage } from '../utils/userError';

type ContextMenu = { x: number; y: number; agent: AgentStatusDto } | null;

function isFinal(status: string) {
  return ['Succeeded', 'Failed', 'RolledBack'].includes(status);
}

export function ClientShellPage({ canPower = false }: { canPower?: boolean }) {
  const [agents, setAgents] = useState<AgentStatusDto[]>([]);
  const [customers, setCustomers] = useState<CustomerRecord[]>([]);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [context, setContext] = useState<ContextMenu>(null);
  const [settingsAgent, setSettingsAgent] = useState<AgentStatusDto | null>(null);
  const [policy, setPolicy] = useState({ kioskEnabled: false, lockOnDisconnect: true });
  const [query, setQuery] = useState('');
  const [notice, setNotice] = useState('');
  const [loading, setLoading] = useState(false);
  const dragRef = useRef<{ startX: number; startY: number; dragging: boolean } | null>(null);
  const [selectionRect, setSelectionRect] = useState<{ startX:number; startY:number; endX:number; endY:number } | null>(null);

  async function loadAgents() {
    try {
      setAgents(await getAgentStatuses());
    } catch (error) {
      setNotice(userErrorMessage(error, 'وضعیت Agentها از سرور دریافت نشد'));
    }
  }

  async function loadCustomers() {
    try {
      setCustomers(await getServerCustomers());
    } catch {
      // Customer lookup is only needed for explicit login action.
    }
  }

  useEffect(() => {
    void loadAgents();
    const interval = window.setInterval(() => void loadAgents(), 5000);
    return () => window.clearInterval(interval);
  }, []);

  useEffect(() => { void loadCustomers(); }, []);

  useEffect(() => {
    const onSelect = (event: Event) => {
      const deviceId = (event as CustomEvent<string>).detail;
      if (!deviceId) return;
      const agent = agents.find(item => item.deviceId === deviceId || item.name === deviceId);
      if (agent) openSettings(agent);
    };
    window.addEventListener('gamenet-select-client', onSelect);
    return () => window.removeEventListener('gamenet-select-client', onSelect);
  }, [agents]);

  useEffect(() => {
    const onMove = (event: MouseEvent) => {
      const drag = dragRef.current;
      if (!drag) return;
      const moved = Math.abs(event.clientX - drag.startX) + Math.abs(event.clientY - drag.startY);
      if (!drag.dragging && moved < 6) return;
      drag.dragging = true;
      setSelectionRect({ startX: drag.startX, startY: drag.startY, endX: event.clientX, endY: event.clientY });
    };
    const onUp = () => {
      const drag = dragRef.current;
      if (!drag) return;
      if (drag.dragging && selectionRect) {
        const left = Math.min(selectionRect.startX, selectionRect.endX);
        const right = Math.max(selectionRect.startX, selectionRect.endX);
        const top = Math.min(selectionRect.startY, selectionRect.endY);
        const bottom = Math.max(selectionRect.startY, selectionRect.endY);
        const ids = Array.from(document.querySelectorAll<HTMLElement>('[data-agent-id]'))
          .filter(node => {
            const rect = node.getBoundingClientRect();
            return rect.right >= left && rect.left <= right && rect.bottom >= top && rect.top <= bottom;
          })
          .map(node => node.dataset.agentId)
          .filter((id): id is string => Boolean(id));
        setSelected(new Set(ids));
      }
      setSelectionRect(null);
      dragRef.current = null;
    };
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
    return () => { window.removeEventListener('mousemove', onMove); window.removeEventListener('mouseup', onUp); };
  }, [selectionRect]);

  const visible = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase('fa-IR');
    return agents.filter(agent => !needle
      || agent.name.toLocaleLowerCase('fa-IR').includes(needle)
      || agent.deviceId.toLocaleLowerCase('fa-IR').includes(needle)
      || (agent.stationName ?? '').toLocaleLowerCase('fa-IR').includes(needle));
  }, [agents, query]);

  const onlineCount = agents.filter(agent => agent.isOnline).length;
  const busyCount = agents.filter(agent => agent.stationId && agent.lifecycleState === 'Running').length;
  const pendingCount = agents.filter(agent => agent.pendingUpdateVersion).length;

  async function waitForCommand(commandId: string) {
    for (let attempt = 0; attempt < 30; attempt += 1) {
      const status = await getAgentCommand(commandId);
      if (isFinal(status.status)) return status;
      await new Promise(resolve => window.setTimeout(resolve, 500));
    }
    throw new Error('نتیجه نهایی فرمان Agent در زمان مورد انتظار تأیید نشد.');
  }

  async function runCommand(agent: AgentStatusDto, type: AgentCommandType) {
    setLoading(true);
    setContext(null);
    try {
      const command = type === 'update'
        ? await requestAgentUpdate(agent.agentId)
        : type === 'rollback'
          ? await requestAgentRollback(agent.agentId)
          : await sendAgentCommand(agent.agentId, type);
      const final = await waitForCommand(command.commandId);
      if (final.status !== 'Succeeded') {
        throw new Error(final.resultMessage || 'فرمان Agent موفق نشد.');
      }
      setNotice(final.resultMessage || 'فرمان Agent با موفقیت اجرا شد.');
      await loadAgents();
    } catch (error) {
      setNotice(userErrorMessage(error, 'اجرای فرمان Agent انجام نشد'));
    } finally {
      setLoading(false);
    }
  }

  async function loginCustomer(agent: AgentStatusDto) {
    const code = window.prompt('کد یا شناسه مشتری را وارد کنید');
    if (!code) return;
    const customer = customers.find(item => item.code === code.trim() || item.username === code.trim());
    if (!customer) {
      setNotice('مشتری پیدا نشد؛ ابتدا اطلاعات مشتری را از سرور تازه کنید.');
      return;
    }

    setLoading(true);
    try {
      const result = await acquireCustomerLogin(customer.id, agent.deviceId);
      if (result) setNotice('ورود مشتری روی سرور ثبت شد · ' + result.activeCount + ' از ' + result.limit);
      await loadAgents();
    } catch (error) {
      setNotice(userErrorMessage(error, 'ورود مشتری روی Agent ثبت نشد'));
    } finally {
      setLoading(false);
      setContext(null);
    }
  }

  function openSettings(agent: AgentStatusDto) {
    setContext(null);
    setSettingsAgent(agent);
    setPolicy({ kioskEnabled: agent.kioskEnabled, lockOnDisconnect: agent.lockOnDisconnect });
  }

  async function savePolicy() {
    if (!settingsAgent) return;
    try {
      await updateAgentPolicy(settingsAgent.agentId, policy);
      setSettingsAgent(null);
      setNotice('Policy روی Server ذخیره شد و به Agent ارسال شد.');
      await loadAgents();
    } catch (error) {
      setNotice(userErrorMessage(error, 'ذخیره Policy Agent انجام نشد'));
    }
  }

  function selectAgent(event: React.MouseEvent, agent: AgentStatusDto) {
    if (event.shiftKey) {
      setSelected(current => new Set([...current, agent.agentId]));
      return;
    }
    if (event.ctrlKey || event.metaKey) {
      setSelected(current => {
        const next = new Set(current);
        if (next.has(agent.agentId)) next.delete(agent.agentId); else next.add(agent.agentId);
        return next;
      });
      return;
    }
    setSelected(new Set([agent.agentId]));
  }

  function showContext(event: React.MouseEvent, agent: AgentStatusDto) {
    event.preventDefault();
    if (!selected.has(agent.agentId)) setSelected(new Set([agent.agentId]));
    setContext({
      x: Math.min(event.clientX, window.innerWidth - 260),
      y: Math.min(event.clientY, window.innerHeight - 430),
      agent,
    });
  }

  const menuAgent = context?.agent;
  const selectedAgents = agents.filter(agent => selected.has(agent.agentId));

  return (
    <>
      <div className="page-header">
        <div><p>مدیریت واقعی Agentها و سیستم‌های متصل</p><h1>کلاینت‌ها · {agents.length.toLocaleString('fa-IR')} دستگاه</h1></div>
      </div>

      <div className="summary-grid">
        <div className="summary-card"><div className="label">آنلاین</div><div className="value green">{onlineCount}</div></div>
        <div className="summary-card"><div className="label">آفلاین</div><div className="value red">{agents.length - onlineCount}</div></div>
        <div className="summary-card"><div className="label">در حال Agent</div><div className="value blue">{busyCount}</div></div>
        <div className="summary-card"><div className="label">آپدیت معلق</div><div className="value orange">{pendingCount}</div></div>
      </div>

      <div className="toolbar client-toolbar">
        <div className="search-box"><input aria-label="جستجوی کلاینت" value={query} onChange={event => setQuery(event.target.value)} placeholder="نام Agent، DeviceId یا ایستگاه…" /></div>
        <button className="btn" disabled={!selectedAgents.length || loading} onClick={() => selectedAgents.forEach(agent => void runCommand(agent, 'ping'))}>📡 Ping</button>
        <button className="btn" disabled={!selectedAgents.length || loading} onClick={() => selectedAgents.forEach(agent => void runCommand(agent, 'lock'))}>🔒 قفل</button>
        <button className="btn primary" disabled={!selectedAgents.length || loading} onClick={() => selectedAgents.forEach(agent => void runCommand(agent, 'unlock'))}>🔓 بازکردن</button>
      </div>

      <div className="client-hint">کلیک: انتخاب · Ctrl+کلیک: چندانتخاب · Shift+کلیک: افزودن · راست‌کلیک: عملیات واقعی Agent · دابل‌کلیک: Policy</div>

      <div className="client-grid">
        {visible.map(agent => (
          <article
            key={agent.agentId}
            data-agent-id={agent.agentId}
            className={\`client-card \${selected.has(agent.agentId) ? 'selected' : ''} \${agent.isOnline ? '' : 'offline'}\`}
            onMouseDown={event => {
              if (event.button !== 0) return;
              dragRef.current = { startX: event.clientX, startY: event.clientY, dragging: false };
            }}
            onClick={event => selectAgent(event, agent)}
            onDoubleClick={() => openSettings(agent)}
            onContextMenu={event => showContext(event, agent)}
          >
            <div className="client-card-head">
              <b>{agent.name}</b>
              <span className={\`status-pill \${agent.isOnline ? 'online' : 'offline'}\`}>{agent.isOnline ? 'آنلاین' : 'آفلاین'}</span>
            </div>
            <div className="meta ltr">{agent.deviceId}</div>
            <div className="meta">{agent.stationName ? \`ایستگاه: \${agent.stationName}\` : 'بدون ایستگاه'}</div>
            <div className="meta">{agent.lifecycleState} · {agent.agentVersion || 'نسخه نامشخص'}</div>
            <div className="meta">Kiosk: {agent.kioskEnabled ? 'فعال' : 'غیرفعال'} · Lock on disconnect: {agent.lockOnDisconnect ? 'فعال' : 'غیرفعال'}</div>
            <div className="meta">{agent.lastHealthyAt ? \`آخرین سلامت: \${new Date(agent.lastHealthyAt).toLocaleString('fa-IR')}\` : 'سلامت هنوز ثبت نشده'}</div>
          </article>
        ))}
      </div>

      {selectionRect && <div className="selection-rect" style={{ left: Math.min(selectionRect.startX, selectionRect.endX), top: Math.min(selectionRect.startY, selectionRect.endY), width: Math.abs(selectionRect.endX - selectionRect.startX), height: Math.abs(selectionRect.endY - selectionRect.startY) }} />}

      {menuAgent && (
        <div
          className="context-menu client-context"
          style={{ left: Math.max(8, Math.min(context?.x ?? 8, window.innerWidth - 260)), top: Math.max(8, Math.min(context?.y ?? 8, window.innerHeight - 430)) }}
          onClick={event => event.stopPropagation()}
        >
          <strong>{selectedAgents.length > 1 ? \`\${selectedAgents.length} Agent انتخابی\` : menuAgent.name}</strong>
          <button onClick={() => void runCommand(menuAgent, 'lock')}>🔒 قفل Agent</button>
          <button onClick={() => void runCommand(menuAgent, 'unlock')}>🔓 بازکردن Agent</button>
          <button onClick={() => void runCommand(menuAgent, 'logout-lock')}>🚪 خروج مشتری و قفل</button>
          <button onClick={() => void loginCustomer(menuAgent)}>🔑 ورود مشتری</button>
          {canPower && <button onClick={() => void runCommand(menuAgent, 'restart')}>🔄 Restart Windows</button>}
          {canPower && <button onClick={() => void runCommand(menuAgent, 'shutdown')}>⛔ Shutdown Windows</button>}
          <button onClick={() => void runCommand(menuAgent, 'update')}>⬆ Update Agent</button>
          <button onClick={() => void runCommand(menuAgent, 'rollback')}>↩ Rollback Agent</button>
          <button onClick={() => openSettings(menuAgent)}>⚙ Policy و تنظیمات</button>
        </div>
      )}

      {settingsAgent && (
        <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setSettingsAgent(null)}>
          <section className="operation-modal wide" role="dialog" aria-modal="true">
            <button className="modal-close" onClick={() => setSettingsAgent(null)}>×</button>
            <h2>Policy · {settingsAgent.name}</h2>
            <div className="settings-form-grid">
              <label>DeviceId<input value={settingsAgent.deviceId} readOnly /></label>
              <label>ایستگاه<input value={settingsAgent.stationName || 'تخصیص‌داده‌نشده'} readOnly /></label>
              <label>نسخه Agent<input value={settingsAgent.agentVersion || ''} readOnly /></label>
              <label>Lifecycle<input value={settingsAgent.lifecycleState} readOnly /></label>
              <label>وضعیت ارتباط<input value={settingsAgent.isOnline ? 'آنلاین' : 'آفلاین'} readOnly /></label>
              <label>حالت Kiosk<select value={policy.kioskEnabled ? 'on' : 'off'} onChange={event => setPolicy({ ...policy, kioskEnabled: event.target.value === 'on' })}><option value="on">فعال</option><option value="off">غیرفعال</option></select></label>
              <label>قفل هنگام قطع ارتباط<select value={policy.lockOnDisconnect ? 'on' : 'off'} onChange={event => setPolicy({ ...policy, lockOnDisconnect: event.target.value === 'on' })}><option value="on">فعال</option><option value="off">غیرفعال</option></select></label>
            </div>
            <div className="modal-actions">
              <button className="btn primary" onClick={() => void savePolicy()}>ذخیره Policy روی Server</button>
              <button className="btn" onClick={() => setSettingsAgent(null)}>انصراف</button>
            </div>
          </section>
        </div>
      )}

      {notice && <div className="operation-toast" role="status">{notice}<button onClick={() => setNotice('')}>×</button></div>}
    </>
  );
}
