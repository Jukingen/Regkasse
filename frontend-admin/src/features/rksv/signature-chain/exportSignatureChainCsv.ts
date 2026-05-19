import type { RksvComplianceSignatureChainItem } from '@/features/rksv/compliance/types';
import { isChainItemIssue } from '@/features/rksv/signature-chain/signatureChainUtils';

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

export function exportSignatureChainIssuesCsv(
  issues: RksvComplianceSignatureChainItem[],
  stampUtc: string,
): void {
  const header = [
    'receiptNumber',
    'receiptId',
    'issuedAtUtc',
    'status',
    'signaturePrefix',
    'prevSignaturePrefix',
    'expectedPrevSignaturePrefix',
    'issue',
  ];
  const rows = issues.map((item) =>
    [
      item.receiptNumber ?? '',
      item.receiptId ?? '',
      item.issuedAtUtc ?? '',
      item.status ?? '',
      item.signaturePrefix ?? '',
      item.prevSignaturePrefix ?? '',
      item.expectedPrevSignaturePrefix ?? '',
      item.issue ?? '',
    ].map(escapeCsvCell),
  );
  const csv = [header.join(','), ...rows.map((r) => r.join(','))].join('\n');
  const stamp = stampUtc.replace(/[:.]/g, '-').slice(0, 19);
  downloadTextFile(
    csv,
    `rksv-signature-chain-issues_${stamp}_UTC.csv`,
    'text/csv;charset=utf-8',
  );
}

export function exportSignatureChainIssuesFromChain(
  chain: RksvComplianceSignatureChainItem[],
  stampUtc: string,
): void {
  exportSignatureChainIssuesCsv(chain.filter(isChainItemIssue), stampUtc);
}
