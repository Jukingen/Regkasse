import { customInstance } from '@/lib/axios';
import type { CancellationReasonCode } from '@/features/payments/types/cancellationReasons';

export interface AdminCancelPaymentRequest {
    reason: string;
    reasonCode: CancellationReasonCode;
    approvalToken?: string;
    idempotencyKey?: string;
}

export interface CancellationResponse {
    success?: boolean;
    requiresApproval?: boolean;
    approvalId?: string;
    message?: string;
    waitTimeSeconds?: number;
    cancelledAt?: string;
    paymentId?: string;
    diagnosticCode?: string;
    errors?: string[];
    approvalNotificationSent?: boolean;
    reasons?: string[];
}

export async function cancelAdminPayment(
    paymentId: string,
    body: AdminCancelPaymentRequest,
    signal?: AbortSignal,
): Promise<CancellationResponse> {
    return customInstance<CancellationResponse>({
        url: `/api/admin/payments/${paymentId}/cancel`,
        method: 'POST',
        data: body,
        signal,
    });
}
