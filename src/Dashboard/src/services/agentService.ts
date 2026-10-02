import type { AgentStatusDto } from '../types';

export async function getAgentStatuses(): Promise<AgentStatusDto[]> {
  const response = await fetch('/api/agent/devices');
  if (!response.ok) {
    throw new Error(`Agent status API returned ${response.status}`);
  }

  return (await response.json()) as AgentStatusDto[];
}


export async function sendAgentCommand(
  agentId: string,
  commandType: 'ping' | 'lock' | 'unlock',
): Promise<import('../types').AgentCommandStatusDto> {
  const response = await fetch('/api/agent/devices/' + agentId + '/commands', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ commandType, payloadJson: null }),
  });

  const payload = await response.json().catch(() => null) as { message?: string } | import('../types').AgentCommandStatusDto | null;
  if (!response.ok) {
    throw new Error(
      payload && 'message' in payload && payload.message
        ? payload.message
        : 'ارسال فرمان به Agent انجام نشد',
    );
  }

  const command = payload as import('../types').AgentCommandStatusDto;
  for (let attempt = 0; attempt < 32; attempt += 1) {
    if (command.status === 'Succeeded' || command.status === 'Failed') return command;

    await new Promise<void>(resolve => window.setTimeout(resolve, 250));
    const statusResponse = await fetch('/api/agent/commands/' + command.commandId);
    if (!statusResponse.ok) {
      const statusPayload = await statusResponse.json().catch(() => null) as { message?: string } | null;
      throw new Error(statusPayload?.message || 'وضعیت فرمان Agent دریافت نشد');
    }

    Object.assign(command, await statusResponse.json());
  }

  throw new Error('فرمان Agent در زمان مجاز تکمیل نشد');
}
