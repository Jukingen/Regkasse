import type { ReceiptDTO } from '../types/ReceiptDTO';
import { formatUserDateTime } from '../utils/dateFormatter';
import { resolveReceiptNetAmount } from '../utils/normalizeReceiptDto';

export interface FormatReceiptParams {
  qrBase64?: string;
  /**
   * @deprecated Ignored. Demo disclaimer comes only from receipt.rksvFooterLabel /
   * receipt.showDemoLabel (backend RKSV:ShowDemoLabel).
   */
  isDemoFiscal?: boolean;
  verificationUrl?: string;
}

const PRODUCTION_RKSV_FOOTER = 'RKSV-konform';

/** Resolve QR-block label from the receipt DTO. Never invent a DEMO string on the client. */
export function resolveReceiptRksvFooter(data: ReceiptDTO): {
  label: string;
  showDemoLabel: boolean;
} {
  const fromBackend = data.rksvFooterLabel?.trim() ?? '';
  const showDemoLabel =
    data.showDemoLabel === true || (data.showDemoLabel == null && fromBackend.includes('DEMO'));
  if (!showDemoLabel) {
    const productionLabel =
      fromBackend && !fromBackend.includes('DEMO') ? fromBackend : PRODUCTION_RKSV_FOOTER;
    return { label: productionLabel, showDemoLabel: false };
  }
  return {
    label: fromBackend || PRODUCTION_RKSV_FOOTER,
    showDemoLabel: true,
  };
}

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

/** Thermal-printer line width for TSE wrap (50–60 chars). */
export const TSE_SIGNATURE_WRAP_WIDTH = 56;

export const RECEIPT_SEP_DOUBLE = '========================================';
export const RECEIPT_SEP_SINGLE = '----------------------------------------';

/** Split a long string into fixed-width lines without truncating. */
export function wrapAtWidth(text: string, width = TSE_SIGNATURE_WRAP_WIDTH): string[] {
  const value = text.trim();
  if (!value) return [];
  const lines: string[] = [];
  for (let i = 0; i < value.length; i += width) {
    lines.push(value.slice(i, i + width));
  }
  return lines;
}

export function formatPaymentMethodLabel(method: string | undefined | null): string {
  const key = (method ?? '').trim().toLowerCase();
  if (key === 'cash' || key === 'bar' || key === 'barzahlung') return 'Bar';
  if (key === 'card' || key === 'karte' || key === 'kartenzahlung') return 'Karte';
  if (key === 'voucher' || key === 'gutschein') return 'Gutschein';
  if (key === 'transfer' || key === 'ueberweisung' || key === 'überweisung') return 'Überweisung';
  return method?.trim() || 'Zahlung';
}

/** Netto for a MwSt row: stored net, otherwise Brutto − MwSt. */
export function resolveTaxLineNet(rate: {
  netAmount?: number | null;
  taxAmount?: number | null;
  grossAmount?: number | null;
}): number {
  const net = Number(rate.netAmount ?? 0);
  const tax = Number(rate.taxAmount ?? 0);
  const gross = Number(rate.grossAmount ?? 0);
  if (net !== 0 || gross === 0) return net;
  return gross - tax;
}

/** RKSV TSE compact JWS display (matches backend ReceiptService.GetTseSignatureDisplay). */
export function formatTseSignatureDisplay(signatureValue: string | undefined | null): string {
  const signature = signatureValue?.trim();
  if (!signature) return 'TSE-Signatur: nicht verfügbar';
  return `TSE-Signatur:\n${wrapAtWidth(signature).join('\n')}`;
}

export const DEFAULT_THANK_YOU_MESSAGE = 'Vielen Dank für Ihren Einkauf!';

function resolveThankYouMessage(data: ReceiptDTO): string {
  const custom = data.thankYouMessage?.trim() || data.footerText?.trim();
  return custom || DEFAULT_THANK_YOU_MESSAGE;
}

function resolveCompanyDescription(data: ReceiptDTO, thankYou: string): string {
  const description = data.company?.description?.trim() ?? '';
  if (!description || description === thankYou) return '';
  return description;
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
  const { label: rksvFooterLabel, showDemoLabel: isDemoFooter } = resolveReceiptRksvFooter(data);

  const itemsHtml = items
    .map(
      (item) => `
        <tr>
          <td class="col-name">${escapeHtml(item.name) || '—'}</td>
          <td class="col-qty">${item.quantity || 0}</td>
          <td class="col-num">${safeCurrency(item.unitPrice)}</td>
          <td class="col-num">${safeCurrency(item.totalPrice)} ${getTaxCode(item.taxRate)}</td>
        </tr>
      `
    )
    .join('');

  const taxTableRowsHtml = taxRates
    .map((rate) => {
      const lineNet = resolveTaxLineNet(rate);
      return `
        <tr>
          <td class="col-num">${safeNumber(rate.rate)}%</td>
          <td class="col-num">${safeCurrency(lineNet)}</td>
          <td class="col-num">${safeCurrency(rate.taxAmount)}</td>
          <td class="col-num">${safeCurrency(rate.grossAmount)}</td>
        </tr>
      `;
    })
    .join('');

  const paymentsHtml = payments
    .map(
      (p) => `
        <div class="total-row">
            <span>${escapeHtml(formatPaymentMethodLabel(p.method))}:</span>
            <span class="total-value">${safeCurrency(p.amount)}</span>
        </div>
        ${
          p.method === 'cash'
            ? `
          <div class="total-row">
             <span>Gegeben:</span>
             <span class="total-value">${safeCurrency(p.tendered)}</span>
          </div>
          <div class="total-row">
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
  const includedTaxTotal = data.taxAmount ?? data.totals?.totalVat ?? 0;
  const subtotalNet = resolveReceiptNetAmount({
    netTotal: data.netTotal,
    totalNet: data.totals?.totalNet,
    subtotal: data.subtotal,
    grandTotal: grandTotalGross,
    taxAmount: includedTaxTotal,
  });

  const thankYouMessage = resolveThankYouMessage(data);
  const companyDescription = resolveCompanyDescription(data, thankYouMessage);
  const companyDescriptionHtml = companyDescription
    ? `<div>${escapeHtml(companyDescription)}</div>`
    : '';

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
  const sepDouble = `<div class="sep sep-double">${RECEIPT_SEP_DOUBLE}</div>`;
  const sepSingle = `<div class="sep sep-single">${RECEIPT_SEP_SINGLE}</div>`;
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
          body { font-family: 'Courier New', Courier, monospace; max-width: 320px; margin: 16px auto; padding: 8px 10px; color: #000; }
          h1 { text-align: center; font-size: 15px; margin: 2px 0 4px; font-weight: bold; letter-spacing: 0.2px; }
          .company-info { text-align: center; font-size: 12px; line-height: 1.35; }
          .meta-info { font-size: 12px; line-height: 1.4; }
          .sep { font-size: 11px; line-height: 1.2; text-align: center; white-space: nowrap; overflow: hidden; margin: 6px 0; }
          table.items, table.mwst-table { width: 100%; border-collapse: collapse; table-layout: fixed; font-size: 11px; }
          table.items th, table.mwst-table th { font-weight: bold; padding: 2px 2px 4px; }
          table.items td, table.mwst-table td { padding: 2px; vertical-align: top; }
          .col-name { width: 44%; text-align: left; word-break: break-word; }
          .col-qty { width: 12%; text-align: center; }
          .col-num { width: 22%; text-align: right; font-variant-numeric: tabular-nums; }
          .totals { font-size: 12px; }
          .total-row { display: flex; justify-content: space-between; align-items: baseline; margin: 2px 0; }
          .total-row .total-value { min-width: 10ch; text-align: right; flex-shrink: 0; }
          .grand-total { font-weight: bold; font-size: 14px; padding: 2px 0; }
          .section-title { font-weight: bold; font-size: 11px; margin: 2px 0 4px; }
          .footer { text-align: center; font-size: 12px; line-height: 1.4; padding: 4px 0; }
          .signature-block { font-size: 10px; text-align: center; }
          .qr-block { text-align: center; margin: 8px 0; }
          .qr-image { display: block; margin: 8px auto; max-width: 180px; height: auto; }
          .qr-demo-label { font-weight: bold; color: #c62828; font-size: 11px; margin-bottom: 6px; }
          .qr-fiscal-label { font-weight: bold; color: #2e7d32; font-size: 11px; margin-bottom: 6px; }
          .tse-signature { font-size: 9px; line-height: 1.35; word-break: break-all; overflow-wrap: anywhere; margin-top: 6px; text-align: left; }
          .qr-fallback { font-size: 10px; color: #666; text-align: center; margin: 8px 0; }
          .rksv-url { font-size: 9px; word-break: break-all; margin-top: 6px; text-align: center; }
          @media print { body { margin: 0; padding: 4px; } }
        </style>
      </head>
      <body>
        ${reversalBanner}
        ${sepDouble}
        <div class="company-info">
          <h1>${escapeHtml(company.name) || '—'}</h1>
          <div>${escapeHtml(company.address)}</div>
          <div>${company.taxNumber ? `UID: ${escapeHtml(company.taxNumber)}` : ''}</div>
        </div>
        ${sepDouble}
        <div class="meta-info">
          <div>Beleg: ${escapeHtml(data.receiptNumber)}</div>
          <div>Datum: ${escapeHtml(formatUserDateTime(data.date))}</div>
          ${data.branchName?.trim() ? `<div>Filiale: ${escapeHtml(data.branchName.trim())}</div>` : ''}
          <div>Kassen-ID: ${escapeHtml(data.kassenID)}</div>
          ${data.terminalNumber?.trim() ? `<div>Terminal: ${escapeHtml(data.terminalNumber.trim())}</div>` : ''}
          <div>Kassierer: ${escapeHtml(data.cashierDisplayName?.trim() || (data.cashierId && data.cashierId.trim()) || '—')}</div>
        </div>
        ${sepSingle}
        <table class="items">
          <thead>
            <tr>
              <th class="col-name">Artikel</th>
              <th class="col-qty">Menge</th>
              <th class="col-num">Einh.</th>
              <th class="col-num">Betrag</th>
            </tr>
          </thead>
          <tbody>${itemsHtml}</tbody>
        </table>
        ${sepSingle}
        <div class="totals">
          <div class="total-row">
            <span>Netto:</span>
            <span class="total-value">${safeCurrency(subtotalNet)}</span>
          </div>
          <div class="total-row">
            <span>MwSt:</span>
            <span class="total-value">${safeCurrency(includedTaxTotal)}</span>
          </div>
          <div class="total-row grand-total">
            <span>SUMME / Brutto:</span>
            <span class="total-value">EUR ${safeCurrency(grandTotalGross)}</span>
          </div>
        </div>
        ${sepSingle}
        <table class="mwst-table">
          <thead>
            <tr>
              <th class="col-num">MwSt%</th>
              <th class="col-num">Netto</th>
              <th class="col-num">MwSt</th>
              <th class="col-num">Brutto</th>
            </tr>
          </thead>
          <tbody>${taxTableRowsHtml}</tbody>
        </table>
        ${sepSingle}
        <div class="totals">${paymentsHtml}</div>
        ${sepSingle}
        <div class="signature-block">
          <div class="section-title">Registrierkassensicherheitsverordnung</div>
          ${qrBlock}
          ${tseSignatureHtml}
          ${signature?.serialNumber && signature?.timestamp ? `<div style="margin-top: 4px; font-size: 9px;">${escapeHtml(signature.serialNumber)} | ${escapeHtml(signature.timestamp)}</div>` : ''}
        </div>
        ${sepSingle}
        <div class="footer">
          <div>${escapeHtml(thankYouMessage)}</div>
          ${companyDescriptionHtml}
        </div>
        ${sepDouble}
      </body>
      </html>
    `;
}
