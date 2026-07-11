import { getAuditActionLabelKey } from '@/features/audit-logs/utils/auditActionLabels';
import { USER_FACING_MISSING_TRANSLATION_LABEL } from '@/i18n/translationFallback';

/**
 * Human-readable label for audit `Action` values on the manager activity log.
 * Falls back to the raw backend action when no catalog entry exists.
 */
export function resolveActivityActionLabel(
    action: string | null | undefined,
    t: (key: string) => string,
): string {
    const raw = action?.trim();
    if (!raw) return '—';

    const auditKey = getAuditActionLabelKey(raw);
    if (auditKey) {
        const translated = t(auditKey);
        if (translated !== USER_FACING_MISSING_TRANSLATION_LABEL) return translated;
    }

    const activityKey = `activity.actions.${raw}`;
    const activityTranslated = t(activityKey);
    if (activityTranslated !== USER_FACING_MISSING_TRANSLATION_LABEL) return activityTranslated;

    return raw;
}
