import { useEffect, useState } from 'react';
import { mockService } from '../services/mockService';
import type { ProductRecord } from '../types';

function money(value: number) {
  return new Intl.NumberFormat('fa-IR').format(value);
}

export function BuffetPage() {
  const [products, setProducts] = useState<ProductRecord[]>([]);
  const [category, setCategory] = useState('همه');
  const [cart, setCart] = useState<Record<string, number>>({});
  const [target, setTarget] = useState<'session' | 'standalone'>('session');
  const [notice, setNotice] = useState('');

  useEffect(() => {
    void mockService.getProducts().then(setProducts);
  }, []);

  const categoryValue = category === 'خوراکی' ? 'غذا' : category;
  const visibleProducts = category === 'همه' ? products : products.filter((item) => item.category === categoryValue);
  const cartTotal = products.reduce((total, product) => total + product.price * (cart[product.id] ?? 0), 0);
  const cartItems = products.filter(product => (cart[product.id] ?? 0) > 0);

  function changeQuantity(id: string, delta: number) {
    setCart(current => {
      const next = Math.max(0, (current[id] ?? 0) + delta);
      return { ...current, [id]: next };
    });
  }

  function checkout(destination: 'session' | 'standalone') {
    if (cartTotal <= 0) { setNotice('سبد فروش خالی است'); return; }
    if (destination === 'session') {
      window.dispatchEvent(new CustomEvent('gamenet-buffet-sale', { detail: { total: cartTotal, items: cartItems.map(item => ({ name: item.name, quantity: cart[item.id] })) } }));
    }
    setProducts(current => current.map(product => ({ ...product, stock: Math.max(0, product.stock - (cart[product.id] ?? 0)) })));
    setCart({});
    setTarget(destination);
    setNotice(destination === 'session' ? 'اقلام به فاکتور جلسه اضافه شدند' : 'فروش مستقل ثبت شد');
  }

  return (
    <>
      <div className="page-header">
        <div>
          <p>بوفه و فروش</p>
          <h1>بوفه</h1>
        </div>
      </div>
      <div className="toolbar"><button type="button" className="btn primary" onClick={() => setNotice('فرم محصول جدید در حالت دمو آماده است')}>+ محصول جدید</button></div>

      <div className="summary-grid">
        <div className="summary-card"><div className="label">فروش امروز</div><div className="value orange">{money(860000)} تومان</div></div>
        <div className="summary-card"><div className="label">سود امروز</div><div className="value green">{money(310000)} تومان</div></div>
        <div className="summary-card"><div className="label">کالای رو به اتمام</div><div className="value red">{products.filter(item => item.stock / item.maxStock <= 0.3).length}</div></div>
        <div className="summary-card"><div className="label">پرفروش امروز</div><div className="value blue">نوشابه · ۱۸ عدد</div></div>
      </div>
      <div className="buffet-layout">
        <section className="panel-box buffet-products">
          <div className="category-tabs">{['همه', 'نوشیدنی', 'غذا', 'تنقلات', 'لوازم جانبی'].map(item => <button key={item} className={category === item ? 'active' : ''} onClick={() => setCategory(item)}>{item}</button>)}</div>
          <div className="product-grid">
            {visibleProducts.map(product => <article key={product.id} className="product-card">
              <div className="icon">🧃</div><b>{product.name}</b><div className="price">{money(product.price)} تومان</div>
              <div className="stock">موجودی: {product.stock} / {product.maxStock}</div>
              <div className="progress-bar"><span style={{ width: `${product.stock / product.maxStock * 100}%` }} /></div>
              {product.stock / product.maxStock <= 0.3 && <small className="low-stock">هشدار موجودی کم</small>}
              <button className="btn sm" disabled={product.stock === 0} onClick={() => changeQuantity(product.id, 1)}>افزودن به سبد</button>
            </article>)}
          </div>
        </section>
        <section className="panel-box cart-panel">
          <h3>🛒 سبد فروش سریع</h3>
          <div className="target-switch"><button className={target === 'session' ? 'active' : ''} onClick={() => setTarget('session')}>افزودن به فاکتور جلسه</button><button className={target === 'standalone' ? 'active' : ''} onClick={() => setTarget('standalone')}>فروش مستقل</button></div>
          <div className="cart-items">{cartItems.length ? cartItems.map(item => <div className="cart-item" key={item.id}><span>{item.name} · {money(item.price)}</span><div><button onClick={() => changeQuantity(item.id, -1)} aria-label="کاهش تعداد">−</button><b>{cart[item.id]}</b><button onClick={() => changeQuantity(item.id, 1)} aria-label="افزایش تعداد">+</button></div></div>) : <p className="empty-state">از فهرست کالا انتخاب کنید</p>}</div>
          <div className="cart-total"><span>جمع سبد</span><b>{money(cartTotal)} تومان</b></div>
          <div className="modal-actions"><button className="btn primary" onClick={() => checkout('session')}>افزودن به فاکتور</button><button className="btn" onClick={() => checkout('standalone')}>ثبت فروش مستقل</button></div>
        </section>
      </div>
      {notice && <div className="operation-toast" role="status">{notice}<button onClick={() => setNotice('')}>×</button></div>}
    </>
  );
}
