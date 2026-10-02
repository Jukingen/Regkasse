import QRCodeImport from 'qrcode';

import { printHtmlAsync } from '../utils/expoPrintShare';

export type IssuedTicketPrint = {
  code: string;
  displayCode?: string;
  validUntilUtc?: string | null;
};

type QrcodeDataUrlApi = {
  toDataURL: (
    text: string,
    options?: { errorCorrectionLevel?: string; margin?: number }
  ) => Promise<string>;
};

function getQrcodeApi(): QrcodeDataUrlApi {
  const mod = QRCodeImport as unknown as QrcodeDataUrlApi & { default?: QrcodeDataUrlApi };
  if (typeof mod?.toDataURL === 'function') return mod;
  if (typeof mod?.default?.toDataURL === 'function') return mod.default;
  return require('qrcode') as QrcodeDataUrlApi;
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

export async function printIssuedTicketsAsync(tickets: IssuedTicketPrint[]): Promise<void> {
  const payload = tickets.filter((ticket) => ticket.code.trim().length > 0);
  if (payload.length === 0) return;

  const qr = getQrcodeApi();
  const blocks: string[] = [];
  for (const ticket of payload) {
    const dataUrl = await qr.toDataURL(ticket.code.trim(), {
      errorCorrectionLevel: 'M',
      margin: 1,
    });
    const until = ticket.validUntilUtc
      ? escapeHtml(ticket.validUntilUtc.replace('T', ' ').slice(0, 16))
      : '';
    blocks.push(`
      <section style="page-break-after:always;text-align:center;font-family:sans-serif;padding:16px;">
        <h1 style="font-size:18px;margin:0 0 8px;">Ticket</h1>
        <p style="font-size:14px;letter-spacing:1px;margin:0 0 12px;">${escapeHtml(ticket.code.trim())}</p>
        <img alt="Ticket-QR" src="${dataUrl}" width="180" height="180" />
        ${until ? `<p style="font-size:12px;margin:12px 0 0;">Gültig bis ${until} UTC</p>` : ''}
        <p style="font-size:10px;color:#555;margin:8px 0 0;">Dieser QR ist kein RKSV-Belegcode.</p>
      </section>
    `);
  }

  await printHtmlAsync(`<!DOCTYPE html><html><body>${blocks.join('')}</body></html>`);
}
