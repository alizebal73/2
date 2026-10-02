type Props = {
  open: boolean;
  title: string;
  detail: string;
  requestLabel?: string;
  onApprove: () => void;
  onReject: () => void;
};

export function ApprovalDialog({
  open,
  title,
  detail,
  requestLabel = 'درخواست تأیید',
  onApprove,
  onReject,
}: Props) {
  if (!open) return null;
  return (
    <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && onReject()}>
      <section className="operation-modal approval-dialog" role="dialog" aria-modal="true" aria-label="تأیید عملیات حساس">
        <div className="approval-icon">!</div>
        <span className="approval-kicker">عملیات حساس</span>
        <h2>{title}</h2>
        <p>{detail}</p>
        <div className="approval-rule">
          <strong>این عملیات برای نقش فعلی نیازمند تأیید است.</strong>
          <span>پس از اتصال به سرور، تأییدکننده و مجوز از Permission واقعی خوانده می‌شوند.</span>
        </div>
        <div className="modal-actions">
          <button type="button" className="btn primary" onClick={onApprove}>✓ {requestLabel}</button>
          <button type="button" className="btn" onClick={onReject}>انصراف</button>
        </div>
      </section>
    </div>
  );
}
