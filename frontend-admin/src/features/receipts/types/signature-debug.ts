/**
 * RKSV Checklist 1-5 signature diagnostic step.
 * Backend: SignatureDiagnosticStep (serialized camelCase).
 */
export interface SignatureDiagnosticStepDto {
    stepId: number;
    name: string;
    status: 'PASS' | 'FAIL' | 'WARN' | 'SIMULATED';
    evidence?: string | null;
}

/**
 * Payload nested under the success envelope for
 * GET /api/Receipts/{receiptId}/signature-debug (admin forensic diagnostics).
 * UI keeps a normalized checklist-like shape for stable rendering.
 */
export interface PaymentSignatureDebugPayload {
    steps: SignatureDiagnosticStepDto[];
    /** Compact JWS when a signature exists; omitted or null when none. */
    compactJws?: string | null;
}

/** Normalizes legacy responses where `data` was a bare step array. */
export function normalizePaymentSignatureDebugPayload(data: unknown): PaymentSignatureDebugPayload {
    if (data == null) {
        return { steps: [], compactJws: null };
    }
    if (Array.isArray(data)) {
        return { steps: data as SignatureDiagnosticStepDto[], compactJws: null };
    }
    if (typeof data === 'object' && data !== null && 'steps' in data) {
        const o = data as { steps?: unknown; compactJws?: unknown };
        return {
            steps: Array.isArray(o.steps) ? (o.steps as SignatureDiagnosticStepDto[]) : [],
            compactJws:
                typeof o.compactJws === 'string'
                    ? o.compactJws
                    : o.compactJws === null
                      ? null
                      : undefined,
        };
    }
    return { steps: [], compactJws: null };
}

/** API response envelope for payment signature-debug */
export interface SignatureDebugApiResponse {
    success: boolean;
    message: string;
    data: PaymentSignatureDebugPayload;
    timestamp: string;
}
