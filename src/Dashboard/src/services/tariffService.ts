export type ServerTariff = {
  id: string;
  name: string;
  description?: string | null;
  hourlyRate: number;
  dailyRate: number;
  isActive: boolean;
};

async function readJson<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error((body as { message?: string } | null)?.message || 'عملیات تعرفه انجام نشد.');
  }
  return body as T;
}

export async function getTariffs(): Promise<ServerTariff[]> {
  return readJson<ServerTariff[]>(await fetch('/api/tariffs', { credentials: 'include' }));
}

export async function saveTariff(
  id: string | null,
  request: Omit<ServerTariff, 'id'>,
): Promise<ServerTariff> {
  const response = await fetch(
    id ? '/api/tariffs/' + encodeURIComponent(id) : '/api/tariffs',
    {
      method: id ? 'PUT' : 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: request.name,
        description: request.description || null,
        hourlyRate: request.hourlyRate,
        dailyRate: request.dailyRate,
        isActive: request.isActive,
      }),
    },
  );
  return readJson<ServerTariff>(response);
}

export async function archiveTariff(id: string): Promise<void> {
  await readJson<{ archived: boolean }>(await fetch(
    '/api/tariffs/' + encodeURIComponent(id),
    { method: 'DELETE', credentials: 'include' },
  ));
}
