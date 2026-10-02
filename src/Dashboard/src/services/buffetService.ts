import type { ProductRecord } from '../types';

type ProductDto = {
  id: string;
  name: string;
  category: string;
  price: number;
  buyPrice: number;
  stock: number;
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
    maxStock: Math.max(row.stock, 10),
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
    }),
  });
  if (!response.ok) throw new Error(await readError(response, 'ثبت محصول انجام نشد'));
  return mapProduct(await response.json() as ProductDto);
}

export async function adjustServerStock(productId: string, quantity: number, direction: 'in' | 'out', notes: string) {
  const response = await fetch('/api/buffet/products/' + productId + '/stock', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      quantity,
      direction,
      notes,
    }),
  });
  if (!response.ok) throw new Error(await readError(response, 'اصلاح موجودی انجام نشد'));
  return await response.json() as { id: string; stock: number };
}

export async function recordServerBuffetSale(items: Array<{ productId: string; quantity: number }>, target: 'session' | 'standalone') {
  const response = await fetch('/api/buffet/sales', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ items, target }),
  });
  if (!response.ok) throw new Error(await readError(response, 'ثبت فروش بوفه انجام نشد'));
  return await response.json() as { total: number; target: string };
}
