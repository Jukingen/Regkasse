import { describe, expect, it } from '@jest/globals';

import { computeRegisterGateBlockingPayment } from '../utils/posRegisterPaymentGate';

const validId = '11111111-1111-1111-1111-111111111111';

/**
 * Mirrors PaymentModal `paySubmitDisabled` for the register slice (see PaymentModal.tsx):
 *   purchaseState === 'processing' || paymentBusy || isRegisterGateBlockingPayment
 * This file asserts the blocked → ready transition that enables "Zahlen" when ensure-ready succeeds before settings load.
 */
function paySubmitDisabledLikePaymentModal(
  processing: boolean,
  paymentBusy: boolean,
  isRegisterGateBlockingPayment: boolean
): boolean {
  return processing || paymentBusy || isRegisterGateBlockingPayment;
}

describe('Zahlen enable path (register gate + modal submit)', () => {
  it('blocked → ready: Zahlen becomes enabled when ensure-ready returns ready while settings still unresolved', () => {
    const loadingGate = computeRegisterGateBlockingPayment({
      enabled: true,
      posEnsureReadyOnEntry: true,
      cashRegisterResolved: true,
      settingsLoadFailed: false,
      posReadinessLoading: true,
      posReadinessError: false,
      posReadinessNextAction: null,
      posReadinessEffectiveRegisterId: null,
      effectiveCashRegisterIdForPayment: null,
    });
    expect(paySubmitDisabledLikePaymentModal(false, false, loadingGate)).toBe(true);

    const readyGate = computeRegisterGateBlockingPayment({
      enabled: true,
      posEnsureReadyOnEntry: true,
      cashRegisterResolved: false,
      settingsLoadFailed: false,
      posReadinessLoading: false,
      posReadinessError: false,
      posReadinessNextAction: 'ready',
      posReadinessEffectiveRegisterId: validId,
      effectiveCashRegisterIdForPayment: validId,
    });
    expect(paySubmitDisabledLikePaymentModal(false, false, readyGate)).toBe(false);
  });

  it('auto-open success path stays disabled while processing (in-flight payment)', () => {
    const readyGate = computeRegisterGateBlockingPayment({
      enabled: true,
      posEnsureReadyOnEntry: true,
      cashRegisterResolved: true,
      settingsLoadFailed: false,
      posReadinessLoading: false,
      posReadinessError: false,
      posReadinessNextAction: 'ready',
      posReadinessEffectiveRegisterId: validId,
      effectiveCashRegisterIdForPayment: validId,
    });
    expect(readyGate).toBe(false);
    expect(paySubmitDisabledLikePaymentModal(true, false, readyGate)).toBe(true);
    expect(paySubmitDisabledLikePaymentModal(false, true, readyGate)).toBe(true);
  });
});
