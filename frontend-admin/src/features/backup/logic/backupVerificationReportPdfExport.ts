import type { BackupVerificationReport } from '@/features/backup/logic/backupVerificationReportApi';
import {
  escapeHtml,
  getBackupVerificationRowDiff,
} from '@/features/backup/logic/backupVerificationReportPresentation';

type TranslateFn = (key: string, options?: Record<string, string | number>) => string;

export function exportBackupVerificationReportPdf(
  report: BackupVerificationReport,
  t: TranslateFn,
  formatLocale: string,
): boolean {
  const popup = globalThis.window.open('', '_blank', 'noopener,noreferrer');
  if (!popup) return false;

  const statusLabel =
    report.status === 'Verified'
      ? t('backupDr.verificationReport.status.Verified')
      : report.status === 'PartiallyVerified'
        ? t('backupDr.verificationReport.status.PartiallyVerified')
        : t('backupDr.verificationReport.status.NotVerified');

  const tableRows = report.tableStatistics
    .map((row) => {
      const diff = getBackupVerificationRowDiff(report, row);
      const diffLabel = diff.missingSource
        ? t('backupDr.verificationReport.diffMissing')
        : diff.diff === 0
          ? t('backupDr.verificationReport.diffIdentical')
          : t('backupDr.verificationReport.diffRows', {
              count: String(diff.diff),
              percent: String(diff.diffPercent ?? 0),
            });
      const rowStyle = diff.mismatched ? 'background:#fff7e6;' : '';
      return `
        <tr style="${rowStyle}">
          <td>${escapeHtml(row.tableName)}</td>
          <td>${row.rowCount.toLocaleString(formatLocale)}</td>
          <td>${diff.sourceRowCount != null ? diff.sourceRowCount.toLocaleString(formatLocale) : '—'}</td>
          <td>${escapeHtml(diffLabel)}</td>
          <td>${escapeHtml(row.isVerified ? t('backupDr.verificationReport.rowVerified') : t('backupDr.verificationReport.rowNotVerified'))}</td>
        </tr>
      `;
    })
    .join('');

  const analyzedAt = report.sourceStatistics?.analyzedAtUtc
    ? new Date(report.sourceStatistics.analyzedAtUtc).toLocaleString(formatLocale)
    : '—';

  popup.document.write(`
    <!doctype html>
    <html>
      <head>
        <meta charset="utf-8" />
        <title>${escapeHtml(t('backupDr.verificationReport.modalTitle'))}</title>
        <style>
          body { font-family: Arial, sans-serif; margin: 24px; color: #111; }
          h1 { margin-bottom: 4px; }
          .meta { color: #666; margin-bottom: 20px; font-size: 13px; }
          .summary { display: flex; gap: 24px; margin-bottom: 20px; flex-wrap: wrap; }
          .summary div { min-width: 140px; }
          .summary strong { display: block; font-size: 22px; }
          table { width: 100%; border-collapse: collapse; }
          th, td { border: 1px solid #d9d9d9; padding: 8px; text-align: left; font-size: 12px; }
          th { background: #fafafa; }
          @media print { body { margin: 12px; } }
        </style>
      </head>
      <body>
        <h1>${escapeHtml(t('backupDr.verificationReport.modalTitle'))}</h1>
        <p class="meta">${escapeHtml(t('backupDr.verificationReport.generatedAt', { time: new Date(report.generatedAtUtc).toLocaleString(formatLocale) }))}</p>
        <div class="summary">
          <div><span>${escapeHtml(t('backupDr.verificationReport.score'))}</span><strong>${report.verificationScore}%</strong></div>
          <div><span>${escapeHtml(t('backupDr.verificationReport.backupSizeTitle'))}</span><strong>${escapeHtml(report.totalSizeFormatted)}</strong></div>
          <div><span>${escapeHtml(t('backupDr.verificationReport.artifactsTitle'))}</span><strong>${report.artifactCount}</strong></div>
          <div><span>${escapeHtml(t('backupDr.verificationReport.statusLabel'))}</span><strong>${escapeHtml(statusLabel)}</strong></div>
        </div>
        <p>${escapeHtml(report.logicalDumpAnalysisMessage ?? '')}</p>
        <h2>${escapeHtml(t('backupDr.verificationReport.tableComparisonTitle'))}</h2>
        <table>
          <thead>
            <tr>
              <th>${escapeHtml(t('backupDr.verificationReport.tableName'))}</th>
              <th>${escapeHtml(t('backupDr.verificationReport.backupRows'))}</th>
              <th>${escapeHtml(t('backupDr.verificationReport.sourceRows'))}</th>
              <th>${escapeHtml(t('backupDr.verificationReport.diffColumn'))}</th>
              <th>${escapeHtml(t('backupDr.verificationReport.verified')}</th>
            </tr>
          </thead>
          <tbody>${tableRows}</tbody>
        </table>
        <p class="meta">${escapeHtml(t('backupDr.verificationReport.sourceAnalyzedAt', { time: analyzedAt }))}</p>
      </body>
    </html>
  `);
  popup.document.close();
  popup.focus();
  popup.print();
  return true;
}
