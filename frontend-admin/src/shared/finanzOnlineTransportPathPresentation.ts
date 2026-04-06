/**
 * FinanzOnline transport path — aligns with backend FinanzOnlineTransportPathKindResolver (authoritative SOAP path classification).
 */

export const FINANZ_ONLINE_TRANSPORT_PATH_KIND = {
    Simulated: 'Simulated',
    RealTest: 'RealTest',
    RealProduction: 'RealProduction',
} as const;

export type FinanzOnlineTransportPathKind =
    (typeof FINANZ_ONLINE_TRANSPORT_PATH_KIND)[keyof typeof FINANZ_ONLINE_TRANSPORT_PATH_KIND];

/** i18n: `finanzOnlineOutbox.transportPath.kind.*` (de UI strings). */
export function labelFinanzOnlineTransportPathKind(
    translate: (key: string) => string,
    kind: string | null | undefined,
    emptyLabel: string,
): string {
    if (!kind?.trim()) return emptyLabel;
    const key = `finanzOnlineOutbox.transportPath.kind.${kind}`;
    const label = translate(key);
    return label === key ? kind : label;
}

export function isSimulatedFinanzOnlineTransportPath(kind: string | null | undefined): boolean {
    return kind === FINANZ_ONLINE_TRANSPORT_PATH_KIND.Simulated;
}

export function finanzOnlineTransportPathTagColor(kind: string | null | undefined): string {
    switch (kind) {
        case FINANZ_ONLINE_TRANSPORT_PATH_KIND.Simulated:
            return 'warning';
        case FINANZ_ONLINE_TRANSPORT_PATH_KIND.RealTest:
            return 'blue';
        case FINANZ_ONLINE_TRANSPORT_PATH_KIND.RealProduction:
            return 'magenta';
        default:
            return 'default';
    }
}

function protocolSuccessIsAuthoritativelyReal(transportPathKind: string | null | undefined): boolean {
    return (
        transportPathKind === FINANZ_ONLINE_TRANSPORT_PATH_KIND.RealTest ||
        transportPathKind === FINANZ_ONLINE_TRANSPORT_PATH_KIND.RealProduction
    );
}

/** Outbox technical status → Tag color; ProtocolSuccess is strong green only for real transport. */
export function finanzOnlineOutboxStatusTagColor(
    status: string | null | undefined,
    transportPathKind?: string | null,
): string {
    if (!status) return 'default';
    if (status === 'ProtocolSuccess') {
        return protocolSuccessIsAuthoritativelyReal(transportPathKind) ? 'success' : 'warning';
    }
    switch (status) {
        case 'Pending':
            return 'blue';
        case 'Processing':
            return 'processing';
        case 'AwaitingProtocol':
            return 'cyan';
        case 'RetryableFailure':
            return 'orange';
        case 'ProtocolFailure':
        case 'PermanentFailure':
            return 'error';
        case 'ManualReviewRequired':
            return 'gold';
        case 'DeadLetter':
            return 'magenta';
        default:
            return 'default';
    }
}

/** Reconciliation lifecycle phase (mapped from outbox) — same success semantics as outbox status. */
export function finanzOnlineOutboxLifecyclePhaseTagColor(
    phase: string | null | undefined,
    transportPathKind?: string | null,
): string {
    if (!phase) return 'default';
    if (phase === 'ProtocolSuccess') {
        return protocolSuccessIsAuthoritativelyReal(transportPathKind) ? 'success' : 'warning';
    }
    switch (phase) {
        case 'PendingQueued':
            return 'blue';
        case 'Sent':
            return 'processing';
        case 'AwaitingProtocol':
            return 'cyan';
        case 'RetryableFailure':
            return 'orange';
        case 'PermanentFailure':
        case 'ManualReviewRequired':
            return 'error';
        case 'DeadLetter':
            return 'magenta';
        default:
            return 'default';
    }
}
