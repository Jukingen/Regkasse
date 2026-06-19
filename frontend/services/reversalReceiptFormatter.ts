import { formatUserDateTime } from '../utils/dateFormatter';

export type ReversalReceiptSnapshot = {
  /** Fiscal payment row id — when set, prefer full RKSV print path. */
  paymentId?: string;
  receiptNumber: string;
  createdAt: string;
  totalAmount: number;
  stornoReasonText?: string | null;
  refundReason?: string | null;
};

const THERMAL_STYLES = `
  body { font-family: 'Courier New', monospace; max-width: 300px; margin: 20px auto; padding: 10px; color: #000; }
  h1 { text-align: center; font-size: 16px; margin: 8px 0; font-weight: bold; letter-spacing: 1px; }
  .sep { text-align: center; font-size: 12px; margin: 6px 0; letter-spacing: 2px; }
  .row { display: flex; justify-content: space-between; gap: 8px; font-size: 12px; margin: 3px 0; }
  .row span:last-child { text-align: right; flex-shrink: 0; }
  .label { font-weight: bold; margin-top: 8px; font-size: 12px; }
  .reason { font-size: 12px; margin-top: 4px; white-space: pre-wrap; }
  .total-box { border-top: 1px dashed #000; border-bottom: 1px dashed #000; margin-top: 10px; padding: 8px 0; }
  @media print { body { margin: 0; padding: 0; } }
`;

export function formatMoneyDe(amount: number): string {
  if (!Number.isFinite(amount)) return '0,00 €';
  return new Intl.NumberFormat('de-AT', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amount);
}

export function formatDateTimeDe(iso: string): string {
  const formatted = formatUserDateTime(iso);
  return formatted || '—';
}

function wrapReversalHtml(title: string, body: string): string {
  return `<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <title>${title}</title>
  <style>${THERMAL_STYLES}</style>
</head>
<body>
${body}
</body>
</html>`;
}

/** Summary storno slip (German POS copy). */
export function formatStornoReceiptHtml(
  stornoPayment: ReversalReceiptSnapshot,
  originalPayment: ReversalReceiptSnapshot
): string {
  const stornoAmount = Math.abs(stornoPayment.totalAmount);
  const remaining = originalPayment.totalAmount - stornoAmount;
  const reason = (stornoPayment.stornoReasonText ?? '').trim() || '—';

  const body = `
  <div class="sep">=================================</div>
  <h1>STORNO BELEG</h1>
  <div class="sep">=================================</div>

  <div class="label">Original Beleg</div>
  <div class="row"><span>Beleg-Nr.:</span><span>${originalPayment.receiptNumber || '—'}</span></div>
  <div class="row"><span>Datum:</span><span>${formatDateTimeDe(originalPayment.createdAt)}</span></div>
  <div class="row"><span>Betrag:</span><span>${formatMoneyDe(originalPayment.totalAmount)}</span></div>

  <div class="label">Storno</div>
  <div class="row"><span>Beleg-Nr.:</span><span>${stornoPayment.receiptNumber || '—'}</span></div>
  <div class="row"><span>Storno am:</span><span>${formatDateTimeDe(stornoPayment.createdAt)}</span></div>
  <div class="row"><span>Storno-Betrag:</span><span>${formatMoneyDe(stornoAmount)}</span></div>
  <div class="label">Storno Grund</div>
  <div class="reason">${escapeHtml(reason)}</div>

  <div class="total-box">
    <div class="row"><span><strong>Neuer Gesamtbetrag:</strong></span><span><strong>${formatMoneyDe(remaining)}</strong></span></div>
  </div>
  <div class="sep">=================================</div>
`;

  return wrapReversalHtml('Storno Beleg', body);
}

/** Summary partial-refund slip (German POS copy). */
export function formatRefundReceiptHtml(
  refundPayment: ReversalReceiptSnapshot,
  originalPayment: ReversalReceiptSnapshot
): string {
  const refundAmount = Math.abs(refundPayment.totalAmount);
  const remaining = originalPayment.totalAmount - refundAmount;
  const reason = (refundPayment.refundReason ?? '').trim() || '—';

  const body = `
  <div class="sep">=================================</div>
  <h1>ERSTATTUNGSBELEG</h1>
  <div class="sep">=================================</div>

  <div class="label">Original Beleg</div>
  <div class="row"><span>Beleg-Nr.:</span><span>${originalPayment.receiptNumber || '—'}</span></div>
  <div class="row"><span>Datum:</span><span>${formatDateTimeDe(originalPayment.createdAt)}</span></div>
  <div class="row"><span>Betrag:</span><span>${formatMoneyDe(originalPayment.totalAmount)}</span></div>

  <div class="label">Erstattung</div>
  <div class="row"><span>Beleg-Nr.:</span><span>${refundPayment.receiptNumber || '—'}</span></div>
  <div class="row"><span>Erstattet am:</span><span>${formatDateTimeDe(refundPayment.createdAt)}</span></div>
  <div class="row"><span>Erstattungsbetrag:</span><span>${formatMoneyDe(refundAmount)}</span></div>
  <div class="label">Grund</div>
  <div class="reason">${escapeHtml(reason)}</div>

  <div class="total-box">
    <div class="row"><span><strong>Verbleibender Betrag:</strong></span><span><strong>${formatMoneyDe(remaining)}</strong></span></div>
  </div>
  <div class="sep">=================================</div>
`;

  return wrapReversalHtml('Erstattungsbeleg', body);
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}
