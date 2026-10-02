function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

export function printTagesberichtSummary(options: {
  title: string;
  dateLabel: string;
  gross: string;
  tax: string;
  payments: string;
}): boolean {
  const popup = globalThis.window.open('', '_blank', 'noopener,noreferrer');
  if (!popup) return false;
  popup.document.write(`
    <!doctype html>
    <html>
      <head>
        <meta charset="utf-8" />
        <title>${escapeHtml(options.title)}</title>
        <style>
          body { font-family: Arial, sans-serif; margin: 24px; color: #111; }
          h1 { margin-bottom: 8px; }
          p { margin: 6px 0; }
          @media print { body { margin: 12px; } }
        </style>
      </head>
      <body>
        <h1>${escapeHtml(options.title)}</h1>
        <p>${escapeHtml(options.dateLabel)}</p>
        <p>${escapeHtml(options.gross)}</p>
        <p>${escapeHtml(options.tax)}</p>
        <p>${escapeHtml(options.payments)}</p>
      </body>
    </html>
  `);
  popup.document.close();
  popup.focus();
  popup.print();
  return true;
}
