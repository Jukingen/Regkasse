/**
 * RKSV Checklist 1-5 signature diagnostic step.
 * Backend: SignatureDiagnosticStep
 */
export interface SignatureDiagnosticStepDto {
    stepId: number;
    name: string;
    status: 'PASS' | 'FAIL' | 'WARN';
    evidence?: string | null;
}

/** API response envelope for signature-debug */
export interface SignatureDebugApiResponse {
    success: boolean;
    message: string;
    data: SignatureDiagnosticStepDto[];
    timestamp: string;
}
