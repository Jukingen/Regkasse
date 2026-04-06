/**
 * Maps FinanzOnline readiness finding codes to operator-facing localized titles (API detail text stays English in UI).
 */

import type { FinanzOnlineReadinessFindingDto } from '@/api/generated/model/finanzOnlineReadinessFindingDto';

export function finanzOnlineReadinessFindingGermanTitle(
    t: (key: string) => string,
    code: string | null | undefined,
): string {
    const c = code?.trim();
    if (!c) return t('finanzOnlineOutbox.readiness.findingTitleFallback');
    const key = `finanzOnlineOutbox.readiness.findingTitles.${c}`;
    const label = t(key);
    return label === key ? c : label;
}

export function finanzOnlineReadinessFindingAlertMessage(
    t: (key: string) => string,
    finding: FinanzOnlineReadinessFindingDto,
): string {
    return finanzOnlineReadinessFindingGermanTitle(t, finding.code);
}
