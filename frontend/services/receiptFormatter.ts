/**
 * Pure receipt HTML formatter (no React Native / Expo).
 * Used by receiptPrinter and by unit tests. Tax table from backend taxSummary only.
 */
import type { ReceiptDTO } from '../types/ReceiptDTO';

export interface FormatReceiptParams {
  qrBase64?: string;
  isDemoFiscal?: boolean;
  verificationUrl?: string;
}

function safeCurrency(value: number | undefined | null): string {
  if (value === undefined || value === null || isNaN(value)) return '0.00';
  return value.toFixed(2).replace('.', ',');
}

function safeNumber(value: number | undefined | null): string {
  if (value === undefined || value === null || isNaN(value)) return '0';
  return Number(value).toFixed(2).replace('.', ',');
}

function getTaxCode(rate: number): string {
  if (rate >= 20) return 'A';
  if (rate >= 10) return 'B';
  return 'C';
}

/**
 * Build receipt HTML. Tax table sorted by rate ascending (10% → 20%).
 * sum(taxSummary.netAmount)==subtotalNet, sum(taxSummary.taxAmount)==includedTaxTotal, sum(taxSummary.grossAmount)==grandTotalGross.
 */
export function formatReceiptHtml(data: ReceiptDTO, params?: FormatReceiptParams): string {
  const items = data.items || [];
  const company = data.company || { name: 'Unknown', address: '', taxNumber: '' };
  const taxRates = [...(data.taxRates || [])].sort((a, b) => (a.rate ?? 0) - (b.rate ?? 0));
  const payments = data.payments || [];
  const signature = data.signature;
  const qrBase64 = params?.qrBase64;
  const isDemoFiscal = params?.isDemoFiscal ?? false;
  const verificationUrl = params?.verificationUrl ?? (data as any).verificationUrl;

  const itemsHtml = items.map(item => `
        <tr>
          <td>${item.name || 'Unknown Item'}</td>
          <td style="text-align: center;">${item.quantity || 0}</td>
          <td style="text-align: right;">${safeCurrency(item.unitPrice)}</td>
          <td style="text-align: right;">${safeCurrency(item.totalPrice)} ${getTaxCode(item.taxRate)}</td>
        </tr>
      `).join('');

  const taxTableRowsHtml = taxRates.map(rate => `
        <tr>
          <td style="text-align: right;">${safeNumber(rate.rate)}%</td>
          <td style="text-align: right;">${safeCurrency(rate.netAmount)}</td>
          <td style="text-align: right;">${safeCurrency(rate.taxAmount)}</td>
          <td style="text-align: right;">${safeCurrency(rate.grossAmount)} ${getTaxCode(rate.rate)}</td>
        </tr>
      `).join('');

  const paymentsHtml = payments.map(p => `
        <div class="total-row">
            <span>${p.method}:</span>
            <span>${safeCurrency(p.amount)}</span>
        </div>
        ${p.method === 'cash' ? `
          <div class="total-row">
             <span>Gegeben:</span>
             <span>${safeCurrency(p.tendered)}</span>
          </div>
          <div class="total-row">
             <span>Rückgeld:</span>
             <span>${safeCurrency(p.change)}</span>
          </div>
        ` : ''}
    `).join('');

  const grandTotalGross = data.grandTotal ?? 0;
  const subtotalNet = data.subtotal ?? (grandTotalGross - (data.taxAmount ?? 0));
  const includedTaxTotal = data.taxAmount ?? 0;

  const rksvVerificationLine = verificationUrl
    ? `<div class="rksv-url">${verificationUrl}</div>`
    : '<div class="rksv-url">RKSV-Prüfung: QR-Code oben scannen</div>';

  const demoLabel = isDemoFiscal ? '<div class="qr-demo-label">DEMO / NICHT FISKAL</div>' : '';
  const qrBlock = qrBase64
    ? `
        <div class="qr-block">
          ${demoLabel}
          <img src="${qrBase64}" class="qr-image" width="180" height="180" alt="RKSV QR Code" />
          ${rksvVerificationLine}
        </div>
      `
    : `<div class="qr-block">${demoLabel}<div class="qr-fallback">QR konnte nicht geladen werden</div>${rksvVerificationLine}</div>`;

  return `
      <!DOCTYPE html>
      <html>
      <head>
        <meta charset="utf-8">
        <title>Receipt ${data.receiptNumber || 'N/A'}</title>
        <style>
          body { font-family: 'Courier New', monospace; max-width: 300px; margin: 20px auto; padding: 10px; color: #000; }
          h1 { text-align: center; font-size: 16px; margin: 5px 0; font-weight: bold; }
          .company-info { text-align: center; font-size: 12px; margin-bottom: 10px; }
          .meta-info { font-size: 12px; margin-bottom: 10px; border-bottom: 1px dashed #000; padding-bottom: 5px; }
          table { width: 100%; border-collapse: collapse; font-size: 12px; margin-bottom: 10px; }
          th { text-align: left; border-bottom: 1px solid #000; padding: 2px 0; }
          td { padding: 2px 0; vertical-align: top; }
          .totals { border-top: 1px dashed #000; margin-top: 5px; padding-top: 5px; font-size: 12px; }
          .total-row { display: flex; justify-content: space-between; margin: 2px 0; }
          .grand-total { font-weight: bold; font-size: 16px; border-top: 1px double #000; border-bottom: 1px double #000; padding: 5px 0; margin: 5px 0; }
          .footer { text-align: center; margin-top: 15px; font-size: 10px; border-top: 1px dashed #000; padding-top: 10px; }
          .signature-block { margin-top: 10px; font-size: 10px; word-break: break-all; text-align: center; }
          .qr-block { text-align: center; margin: 12px 0; margin-top: 15px; }
          .qr-image { display: block; margin: 8px auto; max-width: 180px; height: auto; }
          .qr-demo-label { font-weight: bold; color: #c62828; font-size: 11px; margin-bottom: 6px; }
          .qr-fallback { font-size: 10px; color: #666; text-align: center; margin: 8px 0; }
          .total-sep { text-align: center; font-size: 11px; margin: 4px 0; letter-spacing: 1px; }
          .mwst-section { margin-top: 8px; font-size: 11px; }
          .mwst-title { font-weight: bold; margin-bottom: 4px; }
          .mwst-table { width: 100%; border-collapse: collapse; font-size: 11px; }
          .mwst-table th, .mwst-table td { padding: 2px 4px; border-bottom: 1px solid #ccc; }
          .optional-totals { font-size: 11px; color: #444; }
          .rksv-url { font-size: 9px; word-break: break-all; margin-top: 6px; text-align: center; }
          @media print { body { margin: 0; padding: 0; } }
        </style>
      </head>
      <body>
        <div class="company-info">
          <h1>${company.name || 'Store Name'}</h1>
          <div>${company.address || ''}</div>
          <div>UID: ${company.taxNumber || ''}</div>
        </div>
        <div class="meta-info">
          <div>Beleg: ${data.receiptNumber}</div>
          <div>Datum: ${new Date(data.date).toLocaleString('de-AT')}</div>
          <div>Kasse: ${data.kassenID} | Kassierer: ${data.cashierName}</div>
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
            <span>EUR ${safeCurrency(grandTotalGross)}</span>
          </div>
          <div class="total-sep">==========</div>
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
            <span>${safeCurrency(subtotalNet)}</span>
          </div>
          <div class="total-row optional-totals">
            <span>MwSt gesamt:</span>
            <span>${safeCurrency(includedTaxTotal)}</span>
          </div>
          <div style="margin-top: 10px;">${paymentsHtml}</div>
        </div>
        <div class="signature-block">
          <div style="font-weight: bold; margin-bottom: 5px;">Registrierkassensicherheitsverordnung</div>
          ${qrBlock}
          ${signature?.value ? `<div style="margin-top: 6px; font-size: 9px; word-break: break-all;">${signature.value}</div>` : ''}
          ${signature?.serialNumber && signature?.timestamp ? `<div style="margin-top: 2px; font-size: 9px;">${signature.serialNumber} | ${signature.timestamp}</div>` : ''}
        </div>
        <div class="footer">
          <div>${data.footerText || 'Vielen Dank für Ihren Einkauf!'}</div>
        </div>
      </body>
      </html>
    `;
}
