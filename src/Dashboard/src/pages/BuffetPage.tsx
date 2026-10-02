import { useEffect, useMemo, useState } from 'react';
import type { ProductRecord } from '../types';
import { adjustServerStock, createServerProduct, getServerInventoryTransactions, getServerProducts, recordServerBuffetSale, updateServerProduct } from '../services/buffetService';
import { getServerActiveSessions, type ActiveServerSession } from '../services/sessionService';
import { userErrorMessage } from '../utils/userError';

function money(value: number) {
  return new Intl.NumberFormat('fa-IR').format(Math.round(value));
}

export function BuffetPage() {
  const [products, setProducts] = useState<ProductRecord[]>([]);
  const [inventoryHistory, setInventoryHistory] = useState<import('../types').InventoryTransactionRecord[]>([]);
  const [activeSessions, setActiveSessions] = useState<ActiveServerSession[]>([]);
  const [sessionTargetId, setSessionTargetId] = useState('');
  const [editingProductId, setEditingProductId] = useState<string | null>(null);
  const [category, setCategory] = useState('همه');
  const [cart, setCart] = useState<Record<string, number>>({});
  const [target, setTarget] = useState<'session' | 'standalone'>('session');
  const [notice, setNotice] = useState('');
  const [productFormOpen, setProductFormOpen] = useState(false);
  const [draft, setDraft] = useState({ name: '', category: 'نوشیدنی', price: '', buyPrice: '', stock: '0', minimumStock: '0', unit: 'عدد' });
  const [busy, setBusy] = useState(false);

  async function refresh() {
    try {
      const [serverProducts, serverHistory, serverSessions] = await Promise.all([
        getServerProducts(),
        getServerInventoryTransactions(),
        getServerActiveSessions(),
      ]);
      setProducts(serverProducts);
      setInventoryHistory(serverHistory);
      setActiveSessions(serverSessions);
      setSessionTargetId(current => {
        if (current && serverSessions.some(item => item.id === current)) return current;
        return serverSessions[0]?.id ?? '';
      });
    } catch (error) {
      setNotice(userErrorMessage(error, 'دریافت موجودی بوفه انجام نشد'));
    }
  }

  useEffect(() => { void refresh(); }, []);

  const categoryValue = category === 'خوراکی' ? 'غذا' : category;
  const visibleProducts = category === 'همه' ? products : products.filter(item => item.category === categoryValue);
  const cartTotal = products.reduce((total, product) => total + product.price * (cart[product.id] ?? 0), 0);
  const cartItems = products.filter(product => (cart[product.id] ?? 0) > 0);
  const lowStockCount = useMemo(() => products.filter(item => item.lowStock).length, [products]);

  const numberValue = (value: string) => Number(value.replace(/[۰-۹]/g, d => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g, '')) || 0;

  function changeQuantity(id: string, delta: number) {
    const product = products.find(item => item.id === id);
    if (!product) return;
    setCart(current => ({ ...current, [id]: Math.max(0, Math.min(product.stock, (current[id] ?? 0) + delta)) }));
  }

  async function adjustStock(product: ProductRecord, direction: 'in' | 'out', kind: 'Adjustment' | 'Waste' | 'Return' = 'Adjustment', notesOverride?: string) {
    setBusy(true);
    try {
      const result = await adjustServerStock(product.id, 1, direction, notesOverride ?? (direction === 'in' ? 'ورود بوفه' : 'خروج دستی بوفه'), kind);
      setProducts(current => current.map(item => item.id === product.id ? { ...item, stock: result.stock, maxStock: Math.max(item.maxStock, result.stock) } : item));
      await refresh();
      setNotice(kind === 'Waste' ? 'ضایعات ثبت شد' : kind === 'Return' ? 'مرجوعی ثبت شد' : direction === 'in' ? 'یک عدد به موجودی اضافه شد' : 'یک عدد از موجودی کم شد');
    } catch (error) {
      setNotice(userErrorMessage(error, 'اصلاح موجودی انجام نشد'));
    } finally {
      setBusy(false);
    }
  }

  async function saveProduct() {
    const price = numberValue(draft.price);
    const buyPrice = numberValue(draft.buyPrice);
    const stock = numberValue(draft.stock);
    const minimumStock = numberValue(draft.minimumStock);
    if (!draft.name.trim()) {
      setNotice('نام محصول را وارد کنید');
      return;
    }
    setBusy(true);
    try {
      if (editingProductId) {
        const updated = await updateServerProduct(editingProductId, {
          name: draft.name.trim(),
          category: draft.category,
          price,
          buyPrice,
          minimumStock,
          unit: draft.unit.trim() || 'عدد',
          active: true,
        });
        setProducts(current => current.map(item => item.id === updated.id ? updated : item));
        setNotice('محصول ویرایش شد');
      } else {
        const created = await createServerProduct({
          name: draft.name.trim(),
          category: draft.category,
          price,
          buyPrice,
          initialStock: stock,
          minimumStock,
          unit: draft.unit.trim() || 'عدد',
        });
        setProducts(current => [created, ...current]);
        setNotice('محصول جدید ثبت شد');
      }
      setDraft({ name: '', category: 'نوشیدنی', price: '', buyPrice: '', stock: '0', minimumStock: '0', unit: 'عدد' });
      setEditingProductId(null);
      setProductFormOpen(false);
      await refresh();
    } catch (error) {
      setNotice(userErrorMessage(error, editingProductId ? 'ویرایش محصول انجام نشد' : 'ثبت محصول انجام نشد'));
    } finally {
      setBusy(false);
    }
  }

  function startEdit(product: ProductRecord) {
    setEditingProductId(product.id);
    setDraft({
      name: product.name,
      category: product.category,
      price: String(product.price),
      buyPrice: String(product.buyPrice),
      stock: String(product.stock),
      minimumStock: String(product.minimumStock),
      unit: product.unit,
    });
    setProductFormOpen(true);
  }

  async function checkout(destination: 'session' | 'standalone') {
    if (cartTotal <= 0) { setNotice('سبد فروش خالی است'); return; }
    if (destination === 'session' && !sessionTargetId) {
      setNotice('ابتدا جلسه فعال مقصد را انتخاب کنید');
      return;
    }
    setBusy(true);
    try {
      const sale = await recordServerBuffetSale(
        cartItems.map(item => ({ productId: item.id, quantity: cart[item.id] ?? 0 })),
        destination,
        destination === 'session' ? sessionTargetId : undefined,
      );
      if (destination === 'session') {
        window.dispatchEvent(new CustomEvent('gamenet-buffet-sale', {
          detail: {
            total: sale.total,
            sessionId: sale.sessionId,
            buffetTotal: sale.buffetTotal,
            items: cartItems.map(item => ({ name: item.name, quantity: cart[item.id] ?? 0 })),
          },
        }));
      }
      setCart({});
      await refresh();
      setNotice(destination === 'session' ? 'فروش بوفه ثبت و موجودی سرور به‌روزرسانی شد' : 'فروش مستقل ثبت و موجودی سرور به‌روزرسانی شد');
    } catch (error) {
      setNotice(userErrorMessage(error, 'ثبت فروش بوفه انجام نشد'));
    } finally {
      setBusy(false);
    }
  }

  return <>
    <div className="page-header">
      <div><p>فروش و انبار</p><h1>بوفه</h1></div>
      <div className="page-meta"><span>{products.length} کالا</span><span>{lowStockCount} مورد نیازمند بررسی</span></div>
    </div>

    <div className="toolbar">
      <button type="button" className="btn primary" onClick={() => { setEditingProductId(null); setDraft({ name: '', category: 'نوشیدنی', price: '', buyPrice: '', stock: '0', minimumStock: '0', unit: 'عدد' }); setProductFormOpen(current => !current); }}>+ محصول جدید</button>
      <span className="status-pill free">موجودی از Server</span>
    </div>

    {productFormOpen && <section className="card-panel buffet-product-form">
      <h3>{editingProductId ? 'ویرایش محصول' : 'ثبت محصول'}</h3>
      <div className="modal-grid-2">
        <label>نام محصول<input value={draft.name} onChange={event => setDraft(current => ({ ...current, name: event.target.value }))} /></label>
        <label>دسته<select value={draft.category} onChange={event => setDraft(current => ({ ...current, category: event.target.value }))}>{['نوشیدنی','غذا','تنقلات','لوازم جانبی','سایر'].map(item => <option key={item}>{item}</option>)}</select></label>
        <label>قیمت فروش<input inputMode="numeric" value={draft.price} onChange={event => setDraft(current => ({ ...current, price: event.target.value }))} /></label>
        <label>قیمت خرید<input inputMode="numeric" value={draft.buyPrice} onChange={event => setDraft(current => ({ ...current, buyPrice: event.target.value }))} /></label>
        <label>موجودی اولیه<input inputMode="numeric" value={draft.stock} onChange={event => setDraft(current => ({ ...current, stock: event.target.value }))} /></label>
        <label>حداقل موجودی<input inputMode="numeric" value={draft.minimumStock} onChange={event => setDraft(current => ({ ...current, minimumStock: event.target.value }))} /></label>
        <label>واحد شمارش<input value={draft.unit} onChange={event => setDraft(current => ({ ...current, unit: event.target.value }))} /></label>
      </div>
      <div className="modal-actions"><button className="btn primary" disabled={busy} onClick={() => void saveProduct()}>{editingProductId ? 'ذخیره تغییرات' : 'ثبت محصول'}</button><button className="btn" onClick={() => { setEditingProductId(null); setProductFormOpen(false); }}>انصراف</button></div>
    </section>}

    <div className="summary-grid">
      <div className="summary-card"><div className="label">تعداد کالا</div><div className="value blue">{products.length}</div></div>
      <div className="summary-card"><div className="label">کالای کم‌موجود</div><div className="value red">{lowStockCount}</div></div>
      <div className="summary-card"><div className="label">ارزش فروش سبد</div><div className="value orange">{money(cartTotal)} تومان</div></div>
      <div className="summary-card"><div className="label">سبد فعال</div><div className="value green">{cartItems.length} کالا</div></div>
    </div>

    <div className="buffet-layout">
      <section className="panel-box buffet-products">
        <div className="category-tabs">{['همه','نوشیدنی','غذا','تنقلات','لوازم جانبی','سایر'].map(item => <button key={item} className={category === item ? 'active' : ''} onClick={() => setCategory(item)}>{item}</button>)}</div>
        <div className="product-grid">
          {visibleProducts.map(product => {
            const low = product.lowStock;
            const width = product.maxStock ? Math.min(100, product.stock / product.maxStock * 100) + '%' : '0%';
            return <article key={product.id} className="product-card">
              <div className="icon">🧃</div><b>{product.name}</b><div className="price">{money(product.price)} تومان</div>
              <div className="stock">موجودی: {product.stock} {product.unit} · حداقل {product.minimumStock}</div>
              <div className="progress-bar"><span style={{ width }} /></div>
              {low && <small className="low-stock">هشدار موجودی کم</small>}
              <button className="btn sm" disabled={busy || product.stock === 0} onClick={() => changeQuantity(product.id, 1)}>افزودن به سبد</button>
              <button className="btn sm" disabled={busy} onClick={() => startEdit(product)}>ویرایش</button>
              <div className="product-stock-actions">
                <button className="btn sm" disabled={busy} onClick={() => void adjustStock(product, 'in')}>+ موجودی</button>
                <button className="btn sm" disabled={busy || product.stock === 0} onClick={() => void adjustStock(product, 'out')}>− موجودی</button>
                <button className="btn sm danger" disabled={busy || product.stock === 0} onClick={() => void adjustStock(product, 'out', 'Waste', 'ضایعات بوفه')}>− ضایعات</button>
                <button className="btn sm" disabled={busy} onClick={() => void adjustStock(product, 'in', 'Return', 'مرجوعی بوفه')}>+ مرجوعی</button>
              </div>
            </article>;
          })}
        </div>
      </section>

      <section className="panel-box cart-panel">
        <h3>🛒 سبد فروش سریع</h3>
        <div className="target-switch">
          <button className={target === 'session' ? 'active' : ''} onClick={() => setTarget('session')}>افزودن به فاکتور جلسه</button>
          <button className={target === 'standalone' ? 'active' : ''} onClick={() => setTarget('standalone')}>فروش مستقل</button>
        </div>
        {target === 'session' && (
          <label style={{ marginTop: 10 }}>
            جلسه مقصد
            <select value={sessionTargetId} onChange={event => setSessionTargetId(event.target.value)} disabled={busy}>
              {activeSessions.length === 0 && <option value="">جلسه فعال وجود ندارد</option>}
              {activeSessions.map(session => (
                <option key={session.id} value={session.id}>
                  {session.stationName} · {session.customerName} · بوفه {money(session.buffetTotal)}
                </option>
              ))}
            </select>
          </label>
        )}
        <div className="cart-items">{cartItems.length ? cartItems.map(item => <div className="cart-item" key={item.id}><span>{item.name} · {money(item.price)}</span><div><button onClick={() => changeQuantity(item.id, -1)} aria-label="کاهش تعداد">−</button><b>{cart[item.id]}</b><button onClick={() => changeQuantity(item.id, 1)} aria-label="افزایش تعداد">+</button></div></div>) : <p className="empty-state">از فهرست کالا انتخاب کنید</p>}</div>
        <div className="cart-total"><span>جمع سبد</span><b>{money(cartTotal)} تومان</b></div>
        <div className="modal-actions"><button className="btn primary" disabled={busy} onClick={() => void checkout('session')}>افزودن به فاکتور</button><button className="btn" disabled={busy} onClick={() => void checkout('standalone')}>ثبت فروش مستقل</button></div>
      </section>
    </div>

    <section className="panel-box" style={{ marginTop: 16 }}>
      <div className="profile-section-head"><h3>گردش موجودی</h3><span>{inventoryHistory.length} رویداد</span></div>
      <div className="customer-history-list">
        {inventoryHistory.slice(0, 20).map(item => (
          <div className="customer-history-item" key={item.id}>
            <span className="customer-history-dot" />
            <div>
              <strong>{item.productName} · {item.direction === 'In' ? 'ورود' : 'خروج'} {item.quantity}</strong>
              <small>{item.notes || 'بدون توضیح'} · {new Date(item.createdAt).toLocaleString('fa-IR')}</small>
            </div>
          </div>
        ))}
        {!inventoryHistory.length && <div className="customer-ledger-empty">هنوز گردش موجودی ثبت نشده است.</div>}
      </div>
    </section>

    {notice && <div className="operation-toast" role="status">{notice}<button onClick={() => setNotice('')}>×</button></div>}
  </>;
}
