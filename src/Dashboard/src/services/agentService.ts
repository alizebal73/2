import type { AgentStatusDto } from '../types';

export async function getAgentStatuses(): Promise<AgentStatusDto[]> {
  const response = await fetch('/api/agent/devices');
  if (!response.ok) {
    throw new Error(`Agent status API returned ${response.status}`);
  }

  return (await response.json()) as AgentStatusDto[];
}
