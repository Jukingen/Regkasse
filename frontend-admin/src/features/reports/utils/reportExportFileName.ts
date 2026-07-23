/**
 * Canonical report download names:
 * report_{reportType}_{tenantSlug}_{period}_{yyyyMMdd_HHmmss}.{ext}
 */

export function buildReportFileName(options: {
  reportType: string;
  tenantSlug?: string | null;
  period?: string | null;
  businessDate?: Date | string | null;
  extension?: 'pdf' | 'csv' | 'json';
  at?: Date;
}): string {
  const stamp = formatLocalStamp(options.at ?? new Date());
  const type = sanitizeReportFileSegment(normalizeReportTypeLabel(options.reportType), 'report');
  const slug = sanitizeReportFileSegment(options.tenantSlug, 'tenant');
  const period =
    options.period?.trim() ||
    periodForReportType(options.reportType, options.businessDate) ||
    'period';
  const periodSeg = sanitizeReportFileSegment(period, 'period');
  const ext = options.extension ?? 'pdf';
  return `report_${type}_${slug}_${periodSeg}_${stamp}.${ext}`;
}

export function normalizeReportTypeLabel(reportType: string): string {
  switch (reportType.trim().toLowerCase()) {
    case 'tagesabschluss':
    case 'tagesbericht':
    case 'daily':
      return 'tagesbericht';
    case 'monatsbeleg':
    case 'monatsbericht':
    case 'monthly':
      return 'monatsbericht';
    case 'jahresbeleg':
    case 'jahresbericht':
    case 'yearly':
      return 'jahresbericht';
    default:
      return reportType.trim().toLowerCase().replace(/\s+/g, '_');
  }
}

export function periodForReportType(
  reportType: string,
  businessDate?: Date | string | null
): string {
  const date = coerceDate(businessDate) ?? new Date();
  const label = normalizeReportTypeLabel(reportType);
  if (label === 'monatsbericht') return formatYearMonth(date);
  if (label === 'jahresbericht') return String(date.getFullYear());
  return formatDay(date);
}

export function sanitizeReportFileSegment(
  value: string | null | undefined,
  fallback: string
): string {
  if (!value || !value.trim()) return fallback;
  const sanitized = value
    .trim()
    .replace(/[.\s/\\:]+/g, '_')
    .replace(/[^a-zA-Z0-9_-]/g, '')
    .replace(/^_+|_+$/g, '');
  return sanitized || fallback;
}

function coerceDate(value?: Date | string | null): Date | null {
  if (!value) return null;
  if (value instanceof Date) return Number.isNaN(value.getTime()) ? null : value;
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

function formatDay(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}${m}${d}`;
}

function formatYearMonth(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  return `${y}${m}`;
}

function formatLocalStamp(at: Date): string {
  const y = at.getFullYear();
  const m = String(at.getMonth() + 1).padStart(2, '0');
  const d = String(at.getDate()).padStart(2, '0');
  const hh = String(at.getHours()).padStart(2, '0');
  const mm = String(at.getMinutes()).padStart(2, '0');
  const ss = String(at.getSeconds()).padStart(2, '0');
  return `${y}${m}${d}_${hh}${mm}${ss}`;
}
