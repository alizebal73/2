import type { AgentCommandStatusDto, AgentStatusDto } from '../types';

export type AgentCommandType = 'ping' | 'lock' | 'unlock' | 'logout-lock';

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
