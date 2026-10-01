import { useEffect, useState } from 'react';
import { mockService } from '../services/mockService';
import type { ProductRecord } from '../types';

function money(value: number) {
  return new Intl.NumberFormat('fa-IR').format(value);
}

export function ReportsPage() {
  const [products, setProducts] = useState<ProductRecord[]>([]);

  useEffect(() => {
    void mockService.getProducts().then(setProducts);
  }, []);

  return (
    <>
      <div className="page-header">
        <div>
          <p>نمایش داده‌ها</p>
          <h1>گزارش‌ها</h1>
        </div>
      </div>

      <div className="toolbar">
        <button type="button" className="btn primary">۷ روز</button>
        <button type="button" className="btn">ماهانه</button>
        <button type="button" className="btn">سالانه</button>
      </div>

      <div className="report-grid">
        <div className="chart-box">
          <h3>درآمد نقدی</h3>
          <div className="bar-chart">
            {['10', '17', '24', '18', '31', '22', '28'].map((value, index) => (
              <div key={`${value}-${index}`} className="bar" style={{ height: `${value}%` }} />
            ))}
          </div>
        </div>

        <div className="chart-box">
          <h3>درآمد کارت</h3>
          <div className="bar-chart">
            {['14', '16', '20', '25', '21', '18', '26'].map((value, index) => (
              <div key={`${value}-${index}`} className="bar" style={{ height: `${value}%` }} />
            ))}
          </div>
        </div>
      </div>

      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>ایستگاه</th>
              <th>ساعات</th>
              <th>درآمد</th>
              <th>درصد استفاده</th>
            </tr>
          </thead>
          <tbody>
            {products.slice(0, 4).map((product, index) => (
              <tr key={product.id}>
                <td>{`ایستگاه ${index + 1}`}</td>
                <td>{index * 4 + 8} ساعت</td>
                <td>{money((index + 1) * 1800000)} تومان</td>
                <td>{(index + 2) * 17}%</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
