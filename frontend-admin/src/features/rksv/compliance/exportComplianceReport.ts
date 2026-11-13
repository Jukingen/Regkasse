import type { RksvComplianceReport } from '@/features/rksv/compliance/types';

function escapeCsvCell(value: unknown): string {
  const s = value == null ? '' : String(value);
  if (/[",\n\r]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

function downloadTextFile(content: string, fileName: string, mimeType: string) {
  const blob = new Blob([content], { type: mimeType });
  const url = globalThis.URL.createObjectURL(blob);
  const a = globalThis.document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  globalThis.URL.revokeObjectURL(url);
}

function stampFromReport(report: RksvComplianceReport): string {
  const raw = report.generatedAtUtc ?? new Date().toISOString();
  return raw.replace(/[:.]/g, '-').slice(0, 19);
}

export function exportComplianceReportJson(report: RksvComplianceReport) {
  const json = JSON.stringify(report, null, 2);
  downloadTextFile(
    json,
    `rksv-compliance-report_${stampFromReport(report)}_UTC.json`,
    'application/json'
  );
}

/**
 * Flat CSV for audit handoff: one section per check with a `section` column.
 */
export function exportComplianceReportCsv(report: RksvComplianceReport) {
  const rows: string[][] = [
    ['section', 'cashRegisterId', 'registerNumber', 'receiptNumber', 'status', 'detail'],
  ];

  for (const s of report.specialReceipts ?? []) {
    rows.push([
      'special_receipt',
      s.cashRegisterId ?? '',
      s.registerNumber ?? '',
      s.receiptNumber ?? '',
      s.kind ?? '',
      s.hasTseSignature ? 'has_tse' : 'missing_tse',
    ]);
  }

  for (const c of (report.signatureChain ?? []).filter((x) => x.status && x.status !== 'Pass')) {
    rows.push([
      'signature_chain',
      c.cashRegisterId ?? '',
      c.registerNumber ?? '',
      c.receiptNumber ?? '',
      c.status ?? '',
      c.issue ?? '',
    ]);
  }

  for (const g of report.sequenceGaps ?? []) {
    rows.push([
      'sequence_gap',
      g.cashRegisterId ?? '',
      g.registerNumber ?? '',
      g.previousReceiptNumber ?? '',
      String(g.expectedSequence ?? ''),
      g.nextReceiptNumber ?? '',
    ]);
  }

  for (const t of report.tseSignatureMissing ?? []) {
    rows.push([
      'tse_missing',
      t.cashRegisterId ?? '',
      t.registerNumber ?? '',
      t.receiptNumber ?? '',
      t.specialReceiptKind ?? '',
      [t.paymentSignatureMissing ? 'payment' : '', t.receiptSignatureMissing ? 'receipt' : '']
        .filter(Boolean)
        .join('+'),
    ]);
  }

  for (const q of report.qrPayloadValidation ?? []) {
    rows.push([
      'qr_validation',
      q.cashRegisterId ?? '',
      q.registerNumber ?? '',
      q.receiptNumber ?? '',
      q.qrPayloadMissing ? 'missing' : q.isValidFormat ? 'valid' : 'invalid',
      (q.errors ?? []).join('; '),
    ]);
  }

  const csv = rows.map((r) => r.map(escapeCsvCell).join(',')).join('\n');
  downloadTextFile(
    csv,
    `rksv-compliance-report_${stampFromReport(report)}_UTC.csv`,
    'text/csv;charset=utf-8'
  );
}
