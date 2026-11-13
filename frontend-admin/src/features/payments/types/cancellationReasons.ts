export const CANCELLATION_REASON_CODES = [1, 2, 3, 4, 5, 99] as const;

export type CancellationReasonCode = (typeof CANCELLATION_REASON_CODES)[number];

export type CancellationReasonRisk = 'low' | 'medium' | 'high';

export const CANCELLATION_REASON_RISK: Record<CancellationReasonCode, CancellationReasonRisk> = {
  1: 'low',
  2: 'low',
  3: 'medium',
  4: 'medium',
  5: 'high',
  99: 'medium',
};
