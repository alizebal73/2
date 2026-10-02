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
