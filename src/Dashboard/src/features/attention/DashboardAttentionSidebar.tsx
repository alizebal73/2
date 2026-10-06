import type { ReactNode } from 'react';
import type { PendingSettlementAccount } from '../../types';
import { PendingPaymentsPanel } from '../payment/PendingPaymentsPanel';

export type SidebarAttentionItem = {
  id: string;
  kind: 'action' | 'warning' | 'info';
  title: string;
  detail: string;
  actionLabel: string;
};

type Props = {
  payments: PendingSettlementAccount[];
  attentions: SidebarAttentionItem[];
  recentActions: Array<{ id: string; title: string; station: string; detail: string; createdAt: string; kind: string; canReverse?: boolean }>;
  money: (value: number) => string;
  onPay: (invoiceId: string, method: 'cash' | 'card' | 'wallet') => void;
  onAttention: (id: string) => void;
  onReverse: (id: string) => void;
  children?: ReactNode;
};

export function DashboardAttentionSidebar({
  payments,
  attentions,
  recentActions,
  money,
  onPay,
  onAttention,
  onReverse,
}: Props) {
  return (
    <aside className="dashboard-attention-sidebar" aria-label="مرکز پیگیری">
      <div className="dashboard-sidebar-head">
        <div>
          <span>مرکز پیگیری</span>
          <strong>توجه و پرداخت</strong>
        </div>
        <span className={'dashboard-sidebar-count ' + ((payments.length + attentions.length) ? 'has' : '')}>
          {(payments.length + attentions.length).toLocaleString('fa-IR')}
        </span>
      </div>

      <PendingPaymentsPanel payments={payments} money={money} onPay={onPay} />

      <section className="sidebar-section">
        <div className="sidebar-section-title">
          <strong>نیازمند توجه</strong>
          <span>{attentions.length.toLocaleString('fa-IR')}</span>
        </div>
        {attentions.length === 0 ? (
          <div className="sidebar-empty">فعلاً مورد فوری وجود ندارد.</div>
        ) : (
          <div className="sidebar-attention-list">
            {attentions.slice(0, 12).map(item => (
              <button type="button" className={'sidebar-attention-item ' + item.kind} key={item.id} onClick={() => onAttention(item.id)}>
                <span className="sidebar-attention-dot" />
                <span className="sidebar-attention-copy">
                  <strong>{item.title}</strong>
                  <small>{item.detail}</small>
                </span>
                <span className="sidebar-attention-action">{item.actionLabel}</span>
              </button>
            ))}
          </div>
        )}
      </section>

      <section className="sidebar-section sidebar-recent-section">
        <div className="sidebar-section-title">
          <strong>آخرین عملیات</strong>
          <span>{Math.min(9, recentActions.length).toLocaleString('fa-IR')}</span>
        </div>
        {recentActions.length === 0 ? (
          <div className="sidebar-empty">هنوز عملیاتی ثبت نشده است.</div>
        ) : (
          <div className="sidebar-recent-list">
            {recentActions.slice(0, 9).map(item => (
              <div className="sidebar-recent-item" key={item.id}>
                <span className={'sidebar-recent-dot ' + item.kind} />
                <div>
                  <strong>{item.title}</strong>
                  <small>{item.station} · {item.detail}</small>
                </div>
                <div className="sidebar-recent-side">
                  <time>{new Date(item.createdAt).toLocaleTimeString('fa-IR', { hour: '2-digit', minute: '2-digit' })}</time>
                  {item.canReverse && <button type="button" className="sidebar-reverse-button" aria-label="برگشت این عملیات" onClick={() => onReverse(item.id)}>↩</button>}
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </aside>
  );
}
