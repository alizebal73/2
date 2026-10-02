import type { AgentStatusDto } from '../types';

export async function getAgentStatuses(): Promise<AgentStatusDto[]> {
  const response = await fetch('/api/agent/devices');
  if (!response.ok) {
    throw new Error(`Agent status API returned ${response.status}`);
  }

  return (await response.json()) as AgentStatusDto[];
}


export type AgentCommandType = 'ping' | 'lock' | 'unlock';

export async function sendAgentCommand(agentId: string, commandType: AgentCommandType) {
  const response = await fetch(`/api/agent/devices/${agentId}/commands`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ commandType, payloadJson: null }),
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.message || 'ارسال فرمان Agent انجام نشد.');
  }

  return response.json();
}

export async function getAgentCommand(commandId: string) {
  const response = await fetch(`/api/agent/commands/${commandId}`, {
    credentials: 'include',
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.message || 'وضعیت فرمان Agent دریافت نشد.');
  }

  return response.json();
}


export async function sendAgentCommand(agentId: string, commandType: string, payloadJson?: string | null) {
  const response = await fetch('/api/agent/devices/' + agentId + '/commands', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ commandType, payloadJson: payloadJson ?? null }),
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'ارسال فرمان Agent انجام نشد');
  }
  return await response.json() as AgentCommandStatusDto;
}

export async function updateAgentPolicy(agentId: string, policy: { kioskEnabled: boolean; lockOnDisconnect: boolean }) {
  const response = await fetch('/api/agent/devices/' + agentId + '/policy', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(policy),
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'تنظیم Policy Agent انجام نشد');
  }
  return await response.json() as { agentId: string; kioskEnabled: boolean; lockOnDisconnect: boolean };
}
