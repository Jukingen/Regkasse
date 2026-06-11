/** Matches backend MonatsbelegWarningResponse (400 when past month without force). */
export type MonatsbelegWarningResponse = {
    requiresForce?: boolean;
    warningMessage?: string;
    severity?: 'info' | 'warning' | 'error' | string;
    canForce?: boolean;
    monthDiff?: number;
};

export function warningSeverityToAlertType(
    severity: MonatsbelegWarningResponse['severity'],
): 'info' | 'warning' | 'error' {
    if (severity === 'error') return 'error';
    if (severity === 'info') return 'info';
    return 'warning';
}

export function isMonatsbelegWarningResponse(data: unknown): data is MonatsbelegWarningResponse {
    if (!data || typeof data !== 'object') return false;
    const record = data as Record<string, unknown>;
    return record.requiresForce === true;
}
