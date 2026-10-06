import type { AgentCommandStatusDto, AgentStatusDto } from '../types';

export type AgentCommandType = 'ping' | 'lock' | 'unlock' | 'logout-lock' | 'update' | 'rollback' | 'restart' | 'shutdown';

export async function getAgentStatuses(): Promise<AgentStatusDto[]> {
  const response = await fetch('/api/agent/devices', {
    credentials: 'include',
  });
  if (!response.ok) {
    throw new Error('دریافت وضعیت Agentها انجام نشد.');
  }
  return await response.json() as AgentStatusDto[];
}

export async function sendAgentCommand(
  agentId: string,
  commandType: AgentCommandType,
  payloadJson?: string | null,
): Promise<AgentCommandStatusDto> {
  const response = await fetch('/api/agent/devices/' + agentId + '/commands', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ commandType, payloadJson: payloadJson ?? null }),
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(body?.message || 'ارسال فرمان Agent انجام نشد.');
  }

  return await response.json() as AgentCommandStatusDto;
}

export async function getAgentCommand(commandId: string): Promise<AgentCommandStatusDto> {
  const response = await fetch('/api/agent/commands/' + commandId, {
    credentials: 'include',
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(body?.message || 'وضعیت فرمان Agent دریافت نشد.');
  }

  return await response.json() as AgentCommandStatusDto;
}

export async function updateAgentPolicy(
  agentId: string,
  policy: { kioskEnabled: boolean; lockOnDisconnect: boolean },
): Promise<{ agentId: string; kioskEnabled: boolean; lockOnDisconnect: boolean }> {
  const response = await fetch('/api/agent/devices/' + agentId + '/policy', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(policy),
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(body?.message || 'تنظیم Policy Agent انجام نشد.');
  }

  return await response.json() as { agentId: string; kioskEnabled: boolean; lockOnDisconnect: boolean };
}


export async function requestAgentUpdate(agentId: string): Promise<AgentCommandStatusDto> {
  const response = await fetch('/api/agent/devices/' + agentId + '/update', {
    method: 'POST',
    credentials: 'include',
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(body?.message || 'درخواست به‌روزرسانی Agent انجام نشد.');
  }

  return await response.json() as AgentCommandStatusDto;
}

export async function requestAgentRollback(agentId: string): Promise<AgentCommandStatusDto> {
  const response = await fetch('/api/agent/devices/' + agentId + '/rollback', {
    method: 'POST',
    credentials: 'include',
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(body?.message || 'درخواست Rollback Agent انجام نشد.');
  }

  return await response.json() as AgentCommandStatusDto;
}

export async function createAgentPairingCode(): Promise<{ code: string; expiresAt: string; maxUses: number }> {
  const response = await fetch('/api/agent/pairing-code', {
    method: 'POST',
    credentials: 'include',
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(body?.message || 'ساخت کد اتصال Agent انجام نشد.');
  }

  return await response.json() as { code: string; expiresAt: string; maxUses: number };
}
