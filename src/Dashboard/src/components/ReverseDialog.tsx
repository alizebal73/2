type Props = {
  open: boolean;
  title: string;
  detail: string;
  onConfirm: () => void;
  onCancel: () => void;
};

export function ReverseDialog({ open, title, detail, onConfirm, onCancel }: Props) {
  if (!open) return null;
  return (
    <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && onCancel()}>
      <section className="operation-modal reverse-dialog" role="dialog" aria-modal="true" aria-label="برگشت عملیات">
        <div className="reverse-icon">↩</div>
        <span className="reverse-kicker">برگشت عملیات</span>
        <h2>{title}</h2>
        <p>{detail}</p>
        <div className="reverse-rule">
          <strong>رکورد اصلی حذف نمی‌شود.</strong>
          <span>یک عملیات معکوس ثبت می‌شود تا تاریخچه و حسابرسی قابل پیگیری بماند.</span>
        </div>
        <div className="modal-actions">
          <button type="button" className="btn danger" onClick={onConfirm}>↩ ثبت برگشت</button>
          <button type="button" className="btn" onClick={onCancel}>انصراف</button>
        </div>
      </section>
    </div>
  );
}
