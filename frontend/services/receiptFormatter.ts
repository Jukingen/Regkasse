import type { ReceiptDTO } from '../types/ReceiptDTO';
import { formatUserDateTime } from '../utils/dateFormatter';

export interface FormatReceiptParams {
  qrBase64?: string;
  /** @deprecated Prefer receipt.rksvFooterLabel from backend (environment-aware). */
  isDemoFiscal?: boolean;
  verificationUrl?: string;
}

const PRODUCTION_RKSV_FOOTER = 'RKSV-konform';

function safeCurrency(value: number | undefined | null): string {
  if (value === undefined || value === null || isNaN(value)) return '0.00';
  return value.toFixed(2).replace('.', ',');
}

function safeNumber(value: number | undefined | null): string {
  if (value === undefined || value === null || isNaN(value)) return '0';
  return Number(value).toFixed(2).replace('.', ',');
}

/** Escape text interpolated into receipt HTML (names, addresses, footer). */
export function escapeHtml(value: string | undefined | null): string {
  if (value == null || value === '') return '';
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function getTaxCode(rate: number): string {
  if (rate >= 20) return 'A';
  if (rate >= 10) return 'B';
  return 'C';
}

/** RKSV TSE compact JWS display (matches backend ReceiptService.GetTseSignatureDisplay). */
export function formatTseSignatureDisplay(signatureValue: string | undefined | null): string {
  const signature = signatureValue?.trim();
  if (!signature) return 'TSE-Signatur: nicht verfügbar';
  const shortened = signature.length > 50 ? `${signature.slice(0, 50)}...` : signature;
  return `TSE-Signatur:\n${shortened}`;
}

function resolveSignatureValue(signature: ReceiptDTO['signature'] | undefined): string {
  if (!signature) return '';
  const raw = signature.value?.trim();
  return raw || '';
}

/**
 * Build receipt HTML. Tax table sorted by rate ascending (10% → 20%).
 * sum(taxSummary.netAmount)==subtotalNet, sum(taxSummary.taxAmount)==includedTaxTotal, sum(taxSummary.grossAmount)==grandTotalGross.
 */
export function formatReceiptHtml(data: ReceiptDTO, params?: FormatReceiptParams): string {
  const items = data.items || [];
  const company = data.company || { name: '', address: '', taxNumber: '' };
  const taxRates = [...(data.taxRates || [])].sort((a, b) => (a.rate ?? 0) - (b.rate ?? 0));
  const payments = data.payments || [];
  const signature = data.signature;
  const qrBase64 = params?.qrBase64;
  const verificationUrl = params?.verificationUrl ?? (data as any).verificationUrl;
  const rksvFooterLabel =
    data.rksvFooterLabel?.trim() ||
    (params?.isDemoFiscal ? 'DEMO / NICHT FISKAL' : PRODUCTION_RKSV_FOOTER);
  const isDemoFooter = rksvFooterLabel.includes('DEMO');

  const itemsHtml = items
    .map(
      (item) => `
        <tr>
          <td>${escapeHtml(item.name) || '—'}</td>
          <td style="text-align: center;">${item.quantity || 0}</td>
          <td style="text-align: right;">${safeCurrency(item.unitPrice)}</td>
          <td style="text-align: right;">${safeCurrency(item.totalPrice)} ${getTaxCode(item.taxRate)}</td>
        </tr>
      `
    )
    .join('');

  const taxTableRowsHtml = taxRates
    .map(
      (rate) => `
        <tr>
          <td style="text-align: right;">${safeNumber(rate.rate)}%</td>
          <td style="text-align: right;">${safeCurrency(rate.netAmount)}</td>
          <td style="text-align: right;">${safeCurrency(rate.taxAmount)}</td>
          <td style="text-align: right;">${safeCurrency(rate.grossAmount)} ${getTaxCode(rate.rate)}</td>
        </tr>
      `
    )
    .join('');

  const paymentsHtml = payments
    .map(
      (p) => `
        <div class="total-row optional-totals">
            <span>${escapeHtml(p.method)}:</span>
            <span class="total-value">${safeCurrency(p.amount)}</span>
        </div>
        ${
          p.method === 'cash'
            ? `
          <div class="total-row optional-totals">
             <span>Gegeben:</span>
             <span class="total-value">${safeCurrency(p.tendered)}</span>
          </div>
          <div class="total-row optional-totals">
             <span>Rückgeld:</span>
             <span class="total-value">${safeCurrency(p.change)}</span>
          </div>
        `
            : ''
        }
    `
    )
    .join('');

  const grandTotalGross = data.grandTotal ?? 0;
  const subtotalNet = data.subtotal ?? grandTotalGross - (data.taxAmount ?? 0);
  const includedTaxTotal = data.taxAmount ?? 0;

  const rksvVerificationLine = verificationUrl
    ? `<div class="rksv-url">${escapeHtml(verificationUrl)}</div>`
    : '<div class="rksv-url">RKSV-Prüfung: QR-Code oben scannen</div>';

  const reversalBanner =
    data.fiscalTraceKind === 'Storno'
      ? '<div class="reversal-banner" style="text-align:center;font-weight:bold;font-size:14px;margin-bottom:8px;color:#b71c1c;">STORNO BELEG</div>'
      : data.fiscalTraceKind === 'Refund'
        ? '<div class="reversal-banner" style="text-align:center;font-weight:bold;font-size:14px;margin-bottom:8px;color:#e65100;">ERSTATTUNGSBELEG</div>'
        : '';

  const demoLabel = isDemoFooter
    ? `<div class="qr-demo-label">${escapeHtml(rksvFooterLabel)}</div>`
    : `<div class="qr-fiscal-label">${escapeHtml(rksvFooterLabel)}</div>`;
  const signatureValue = resolveSignatureValue(signature);
  const tseSignatureHtml = `<div class="tse-signature">${escapeHtml(formatTseSignatureDisplay(signatureValue)).replace(/\n/g, '<br/>')}</div>`;
  // qrBase64 must already be a data: URL from the printer service — do not escape (would break the image).
  const qrImgSrc = qrBase64 && /^data:image\//i.test(qrBase64) ? qrBase64 : '';
  const qrBlock = qrImgSrc
    ? `
        <div class="qr-block">
          ${demoLabel}
          <img src="${qrImgSrc}" class="qr-image" width="180" height="180" alt="RKSV QR Code" />
          ${rksvVerificationLine}
        </div>
      `
    : `<div class="qr-block">${demoLabel}<div class="qr-fallback">QR konnte nicht geladen werden</div>${rksvVerificationLine}</div>`;

  return `
      <!DOCTYPE html>
      <html>
      <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Beleg ${escapeHtml(data.receiptNumber) || 'N/A'}</title>
        <style>
          body { font-family: 'Courier New', monospace; max-width: 300px; margin: 20px auto; padding: 10px; color: #000; }
          h1 { text-align: center; font-size: 16px; margin: 5px 0; font-weight: bold; }
          .company-info { text-align: center; font-size: 12px; margin-bottom: 10px; }
          .meta-info { font-size: 12px; margin-bottom: 10px; border-bottom: 1px dashed #000; padding-bottom: 5px; }
          table { width: 100%; border-collapse: collapse; font-size: 12px; margin-bottom: 10px; }
          th { text-align: left; border-bottom: 1px solid #000; padding: 2px 0; }
          td { padding: 2px 0; vertical-align: top; }
          .totals { border-top: 1px dashed #000; margin-top: 5px; padding-top: 5px; font-size: 12px; }
          .total-row { display: flex; justify-content: space-between; align-items: baseline; margin: 2px 0; }
          .total-row .total-value { width: 12ch; text-align: right; flex-shrink: 0; }
          .grand-total { font-weight: bold; font-size: 16px; border-top: 1px double #000; border-bottom: 1px double #000; padding: 5px 0; margin: 5px 0 10px 0; }
          .footer { text-align: center; margin-top: 15px; font-size: 10px; border-top: 1px dashed #000; padding-top: 10px; }
          .signature-block { margin-top: 10px; font-size: 10px; word-break: break-all; text-align: center; }
          .qr-block { text-align: center; margin: 12px 0; margin-top: 15px; }
          .qr-image { display: block; margin: 8px auto; max-width: 180px; height: auto; }
          .qr-demo-label { font-weight: bold; color: #c62828; font-size: 11px; margin-bottom: 6px; }
          .qr-fiscal-label { font-weight: bold; color: #2e7d32; font-size: 11px; margin-bottom: 6px; }
          .tse-signature { font-size: 9px; word-break: break-all; margin-top: 6px; text-align: center; }
          .qr-fallback { font-size: 10px; color: #666; text-align: center; margin: 8px 0; }
          .total-sep { text-align: center; font-size: 11px; margin: 4px 0; letter-spacing: 1px; }
          .mwst-section { margin-top: 12px; font-size: 11px; }
          .payment-section { margin-top: 6px; }
          .mwst-title { font-weight: bold; margin-bottom: 4px; }
          .mwst-table { width: 100%; border-collapse: collapse; font-size: 11px; }
          .mwst-table th, .mwst-table td { padding: 2px 4px; border-bottom: 1px solid #ccc; }
          .optional-totals { font-size: 11px; color: #444; }
          .rksv-url { font-size: 9px; word-break: break-all; margin-top: 6px; text-align: center; }
          @media print { body { margin: 0; padding: 0; } }
        </style>
      </head>
      <body>
        ${reversalBanner}
        <div class="company-info">
          <h1>${escapeHtml(company.name) || '—'}</h1>
          <div>${escapeHtml(company.address)}</div>
          <div>${company.taxNumber ? `UID: ${escapeHtml(company.taxNumber)}` : ''}</div>
        </div>
        <div class="meta-info">
          <div>Beleg: ${escapeHtml(data.receiptNumber)}</div>
          <div>Datum: ${escapeHtml(formatUserDateTime(data.date))}</div>
          <div>Kasse: ${escapeHtml(data.kassenID)} | Kassierer: ${escapeHtml(data.cashierDisplayName?.trim() || (data.cashierId && data.cashierId.trim()) || '—')}</div>
        </div>
        <table>
          <thead>
            <tr>
              <th>Art.</th>
              <th style="text-align: center;">Menge</th>
              <th style="text-align: right;">Einh.</th>
              <th style="text-align: right;">Eur</th>
            </tr>
          </thead>
          <tbody>${itemsHtml}</tbody>
        </table>
        <div class="totals">
          <div class="total-row grand-total">
            <span>SUMME / Gesamtbetrag:</span>
            <span class="total-value">EUR ${safeCurrency(grandTotalGross)}</span>
          </div>
          <div class="total-sep">----------------</div>
          <div class="mwst-section">
            <div class="mwst-title">MwSt (MwSt% | Netto | MwSt | Brutto)</div>
            <table class="mwst-table">
              <thead>
                <tr>
                  <th style="text-align: right;">MwSt%</th>
                  <th style="text-align: right;">excl.</th>
                  <th style="text-align: right;">MWST.</th>
                  <th style="text-align: right;">Incl.</th>
                </tr>
              </thead>
              <tbody>${taxTableRowsHtml}</tbody>
            </table>
          </div>
          <div class="total-row optional-totals">
            <span>Netto:</span>
            <span class="total-value">${safeCurrency(subtotalNet)}</span>
          </div>
          <div class="total-row optional-totals">
            <span>MwSt gesamt:</span>
            <span class="total-value">${safeCurrency(includedTaxTotal)}</span>
          </div>
          <div class="payment-section">${paymentsHtml}</div>
        </div>
        <div class="signature-block">
          <div style="font-weight: bold; margin-bottom: 5px;">Registrierkassensicherheitsverordnung</div>
          ${qrBlock}
          ${tseSignatureHtml}
          ${signature?.serialNumber && signature?.timestamp ? `<div style="margin-top: 2px; font-size: 9px;">${escapeHtml(signature.serialNumber)} | ${escapeHtml(signature.timestamp)}</div>` : ''}
        </div>
        <div class="footer">
          <div>${escapeHtml(data.footerText) || 'Vielen Dank für Ihren Einkauf!'}</div>
        </div>
      </body>
      </html>
    `;
}
