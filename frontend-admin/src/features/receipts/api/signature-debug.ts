import { customInstance } from '@/lib/axios';
import type { SignatureDebugApiResponse } from '../types/signature-debug';

/**
 * GET /api/Payment/{paymentId}/signature-debug
 * Admin only. Returns RKSV Checklist 1-5 diagnostic steps.
 */
export async function fetchSignatureDebug(paymentId: string): Promise<SignatureDebugApiResponse> {
    return customInstance<SignatureDebugApiResponse>({
        url: `/api/Payment/${paymentId}/signature-debug`,
        method: 'GET',
    });
}
