import { useQuery } from '@tanstack/react-query';
import { fetchSignatureDebug } from '../api/signature-debug';

const SIGNATURE_DEBUG_KEYS = {
    all: ['signature-debug'] as const,
    byPayment: (paymentId: string) => [...SIGNATURE_DEBUG_KEYS.all, paymentId] as const,
} as const;

/**
 * Fetches signature diagnostic (RKSV Checklist 1-5) for a payment.
 * Disabled when paymentId is falsy or when offline.
 */
export function useSignatureDebugQuery(paymentId: string | null | undefined) {
    const isOffline = typeof navigator !== 'undefined' && !navigator.onLine;

    return useQuery({
        queryKey: SIGNATURE_DEBUG_KEYS.byPayment(paymentId ?? ''),
        queryFn: () => fetchSignatureDebug(paymentId!),
        enabled: !!paymentId && !isOffline,
        staleTime: 60_000,
        retry: false,
    });
}
