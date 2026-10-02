import type { BuffetProfitReport, InventoryTransactionRecord, ProductRecord } from '../types';

type ProductDto = {
  id: string;
  name: string;
  category: string;
  price: number;
  buyPrice: number;
  stock: number;
  minimumStock: number;
  unit: string;
  lowStock: boolean;
  active: boolean;
};

async function readError(response: Response, fallback: string) {
  const payload = await response.json().catch(() => null) as { message?: string } | null;
  return payload?.message || fallback;
}

function mapProduct(row: ProductDto): ProductRecord {
  return {
    id: row.id,
    name: row.name,
    category: row.category,
    price: row.price,
    buyPrice: row.buyPrice,
    stock: row.stock,
    minimumStock: row.minimumStock,
    unit: row.unit,
    lowStock: row.lowStock,
    maxStock: Math.max(row.stock, row.minimumStock * 3, 10),
  };
}

export async function getServerProducts(): Promise<ProductRecord[]> {
  const response = await fetch('/api/buffet/products');
  if (!response.ok) throw new Error(await readError(response, 'دریافت کالاهای بوفه انجام نشد'));
  return (await response.json() as ProductDto[]).map(mapProduct);
}

export async function createServerProduct(input: {
  name: string;
  category: string;
  price: number;
  buyPrice: number;
  initialStock: number;
  minimumStock: number;
  unit: string;
}) {
  const response = await fetch('/api/buffet/products', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      name: input.name,
      category: input.category,
      unitPrice: input.price,
      costPrice: input.buyPrice,
      initialStock: input.initialStock,
      minimumStock: input.minimumStock,
      unit: input.unit,
    }),
  });
  if (!response.ok) throw new Error(await readError(response, 'ثبت محصول انجام نشد'));
  return mapProduct(await response.json() as ProductDto);
}

export async function adjustServerStock(productId: string, quantity: number, direction: 'in' | 'out', notes: string, kind: 'Adjustment' | 'Purchase' | 'Sale' | 'Waste' | 'Return' = 'Adjustment', unitCost?: number) {
  const response = await fetch('/api/buffet/products/' + productId + '/stock', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      quantity,
      direction,
      notes,
      kind,
      unitCost,
    }),
  });
  if (!response.ok) throw new Error(await readError(response, 'اصلاح موجودی انجام نشد'));
  return await response.json() as { id: string; stock: number; lowStock: boolean; kind: string };
}

export async function recordServerBuffetSale(
  items: Array<{ productId: string; quantity: number }>,
  target: 'session' | 'standalone',
  sessionId?: string,
) {
  const response = await fetch('/api/buffet/sales', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ items, target, sessionId: sessionId || null }),
  });
  if (!response.ok) throw new Error(await readError(response, 'ثبت فروش بوفه انجام نشد'));
  return await response.json() as {
    total: number;
    target: string;
    sessionId?: string;
    invoiceId?: string;
    buffetTotal: number;
  };
}


export async function updateServerProduct(productId: string, input: {
  name: string;
  category: string;
  price: number;
  buyPrice: number;
  minimumStock: number;
  unit: string;
  active: boolean;
}) {
  const response = await fetch('/api/buffet/products/' + productId, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      name: input.name,
      category: input.category,
      unitPrice: input.price,
      costPrice: input.buyPrice,
      minimumStock: input.minimumStock,
      unit: input.unit,
      isActive: input.active,
    }),
  });
  if (!response.ok) throw new Error(await readError(response, 'ویرایش محصول انجام نشد'));
  return mapProduct(await response.json() as ProductDto);
}

export async function getServerInventoryTransactions(): Promise<InventoryTransactionRecord[]> {
  const response = await fetch('/api/buffet/inventory-transactions');
  if (!response.ok) throw new Error(await readError(response, 'دریافت گردش موجودی انجام نشد'));
  return await response.json() as InventoryTransactionRecord[];
}


export async function getServerBuffetProfit(from?: string, to?: string): Promise<BuffetProfitReport> {
  const query = new URLSearchParams();
  if (from) query.set('from', from);
  if (to) query.set('to', to);
  const response = await fetch('/api/buffet/reports/profit' + (query.size ? '?' + query.toString() : ''));
  if (!response.ok) throw new Error(await readError(response, 'دریافت گزارش سود بوفه انجام نشد'));
  return await response.json() as BuffetProfitReport;
}
