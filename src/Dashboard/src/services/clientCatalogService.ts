export type ClientCatalogGame = {
  id: string;
  name: string;
  version?: string | null;
  genre?: string | null;
  status?: string | null;
  cover?: string | null;
  trailer?: string | null;
  connectionType?: string | null;
  icon: string;
  description: string;
  hasPoolAccount: boolean;
};

export type ClientCatalogBuffetItem = {
  id: string;
  name: string;
  category: string;
  price: number;
  unit: string;
  available: boolean;
  icon: string;
};

export type ClientCatalog = {
  deviceId: string;
  stationId?: string | null;
  stationName?: string | null;
  games: ClientCatalogGame[];
  buffet: ClientCatalogBuffetItem[];
};

export async function getClientCatalog(): Promise<ClientCatalog> {
  const response = await fetch('/api/client/catalog', {
    credentials: 'include',
  });

  if (response.ok) {
    return await response.json() as ClientCatalog;
  }

  const payload = await response.json().catch(() => null) as { message?: string } | null;
  throw new Error(payload?.message || 'اطلاعات بازی و بوفه این رایانه دریافت نشد.');
}
