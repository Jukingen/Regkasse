/**
 * Report Center: reine Zuordnung von API-Status zu Badge-Farben (Ant Design Tag).
 * Beschriftungen kommen aus i18n (adminShell.reporting.reportCenter.*).
 */

export type AntTagColor =
  | 'success'
  | 'processing'
  | 'warning'
  | 'error'
  | 'default'
  | 'gold'
  | 'blue'
  | 'cyan'
  | 'geekblue'
  | 'orange'
  | 'magenta'
  | 'volcano';

/** Tagesbericht / Monatsbericht / Jahresbericht — ReportStatus (Dokument). */
export function documentStatusVisual(status: string | undefined): {
  color: AntTagColor;
  labelKey: string;
} {
  const s = (status ?? '').trim();
  switch (s) {
    case 'Provisional':
      return { color: 'gold', labelKey: 'docProvisional' };
    case 'Finalized':
      return { color: 'success', labelKey: 'docFinalized' };
    case 'Superseded':
      return { color: 'orange', labelKey: 'docSuperseded' };
    default:
      return { color: 'default', labelKey: 'docUnknown' };
  }
}

const inFlight = new Set(['queued', 'pending', 'awaiting_protocol', 'retry_pending']);

/** submission.lifecycle aus List-DTOs (TagesberichtSubmissionStateDto). */
export function submissionLifecycleVisual(lifecycle: string | undefined): {
  color: AntTagColor;
  labelKey: string;
} {
  const l = (lifecycle ?? 'not_submitted').trim();
  if (l === 'not_submitted') return { color: 'default', labelKey: 'subNotSubmitted' };
  if (l === 'accepted') return { color: 'success', labelKey: 'subAccepted' };
  if (l === 'rejected' || l === 'failed_terminal')
    return { color: 'error', labelKey: 'subRejected' };
  if (l === 'correction_required') return { color: 'warning', labelKey: 'subNeedsReview' };
  if (inFlight.has(l)) return { color: 'processing', labelKey: 'subInFlight' };
  return { color: 'default', labelKey: 'subUnknown' };
}

/** Zeigt, ob die Meldung bereits unterwegs ist (für Filter «Gemeldet»). */
export function isSubmissionInFlight(lifecycle: string | undefined): boolean {
  return inFlight.has((lifecycle ?? '').trim());
}

export function isSubmissionAccepted(lifecycle: string | undefined): boolean {
  return (lifecycle ?? '').trim() === 'accepted';
}

export type SubmissionFilterKey =
  'all' | 'notSubmitted' | 'inFlight' | 'accepted' | 'rejectedOrReview';

export function matchesSubmissionFilter(
  lifecycle: string | undefined,
  filter: SubmissionFilterKey
): boolean {
  const l = (lifecycle ?? 'not_submitted').trim();
  if (filter === 'all') return true;
  if (filter === 'notSubmitted') return l === 'not_submitted';
  if (filter === 'inFlight') return isSubmissionInFlight(lifecycle);
  if (filter === 'accepted') return l === 'accepted';
  if (filter === 'rejectedOrReview')
    return l === 'rejected' || l === 'failed_terminal' || l === 'correction_required';
  return true;
}

export type ReportDocFilterKey = 'all' | 'Provisional' | 'Finalized' | 'Superseded';

export function matchesReportDocFilter(
  status: string | undefined,
  filter: ReportDocFilterKey
): boolean {
  if (filter === 'all') return true;
  return (status ?? '').trim() === filter;
}
