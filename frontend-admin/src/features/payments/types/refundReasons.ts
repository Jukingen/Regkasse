import type { RefundReasonCode } from '@/api/generated/model/refundReasonCode';

export const REFUND_REASON_CODES = [1, 2, 3, 4, 99] as const satisfies readonly RefundReasonCode[];

export type RefundReasonCodeValue = (typeof REFUND_REASON_CODES)[number];
