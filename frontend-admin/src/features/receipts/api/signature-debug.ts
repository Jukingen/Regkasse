import { customInstance } from '@/lib/axios';
import {
    normalizePaymentSignatureDebugPayload,
    type PaymentSignatureDebugPayload,
    type SignatureDebugApiResponse,
} from '../types/signature-debug';

function isRecord(v: unknown): v is Record<string, unknown> {
    return v != null && typeof v === 'object' && !Array.isArray(v);
}

/**
 * GET /api/Payment/{paymentId}/signature-debug
 * Admin only. RKSV checklist steps + optional compact JWS for payment-linked TSE signature.
 * Accepts standard SuccessResponse `{ success, message, data, timestamp }` or a bare `{ steps, compactJws }` body.
 */
export async function fetchSignatureDebug(paymentId: string): Promise<SignatureDebugApiResponse> {
    const rawUnknown = await customInstance<unknown>({
        url: `/api/Payment/${paymentId}/signature-debug`,
        method: 'GET',
    });

    if (!isRecord(rawUnknown)) {
        return {
            success: false,
            message: 'Invalid response',
            data: normalizePaymentSignatureDebugPayload(null),
            timestamp: new Date().toISOString(),
        };
    }

    const hasEnvelope =
        typeof rawUnknown.success === 'boolean' && 'data' in rawUnknown;
    const payloadSource: unknown = hasEnvelope ? rawUnknown.data : rawUnknown;
    const normalized: PaymentSignatureDebugPayload =
        normalizePaymentSignatureDebugPayload(payloadSource);

    if (hasEnvelope) {
        return {
            success: Boolean(rawUnknown.success),
            message: typeof rawUnknown.message === 'string' ? rawUnknown.message : '',
            data: normalized,
            timestamp:
                typeof rawUnknown.timestamp === 'string'
                    ? rawUnknown.timestamp
                    : new Date().toISOString(),
        };
    }

    return {
        success: true,
        message: 'OK',
        data: normalized,
        timestamp: new Date().toISOString(),
    };
}
