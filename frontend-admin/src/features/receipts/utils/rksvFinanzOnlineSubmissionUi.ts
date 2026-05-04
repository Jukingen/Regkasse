/**
 * UI helpers for RKSV Startbeleg/Jahresbeleg FinanzOnline submission status (admin).
 */

const TRACKED_KINDS = new Set(['Startbeleg', 'Jahresbeleg']);

export function isRksvFinanzOnlineTrackedSpecialReceiptKind(kind: string | null | undefined): boolean {
    return TRACKED_KINDS.has((kind ?? '').trim());
}

/** Ant Design Tag color preset by backend status string. */
export function rksvFinanzOnlineSubmissionStatusTagColor(status: string | null | undefined): string {
    switch ((status ?? '').trim()) {
        case 'Verified':
            return 'success';
        case 'Failed':
            return 'error';
        case 'ManualVerificationRequired':
            return 'warning';
        case 'Submitted':
            return 'processing';
        case 'Pending':
            return 'default';
        case 'NotRequired':
            return 'blue';
        default:
            return 'default';
    }
}

/** German operator label for tables (Sonderbelege page — de-DE copy). */
export function rksvFinanzOnlineSubmissionStatusLabelDe(status: string | null | undefined): string {
    switch ((status ?? '').trim()) {
        case 'Pending':
            return 'Ausstehend';
        case 'Submitted':
            return 'Übermittelt';
        case 'Verified':
            return 'Geprüft';
        case 'Failed':
            return 'Fehlgeschlagen';
        case 'ManualVerificationRequired':
            return 'Manuelle Prüfung nötig';
        case 'NotRequired':
            return 'Nicht erforderlich';
        default:
            return (status ?? '').trim() ? String(status) : '—';
    }
}

export function shouldOfferFinanzOnlineReconciliationRetry(status: string | null | undefined): boolean {
    const s = (status ?? '').trim();
    return s === 'Failed' || s === 'ManualVerificationRequired' || s === 'Pending';
}
