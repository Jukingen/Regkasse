import { describe, expect, it } from '@jest/globals';

import { computeRegisterGateBlockingPayment } from '../utils/posRegisterPaymentGate';

const validId = '11111111-1111-1111-1111-111111111111';

describe('computeRegisterGateBlockingPayment', () => {
  it('does not block when settings are still loading but ensure-ready is ready with valid id (scenario 1 race)', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: true,
        cashRegisterResolved: false,
        settingsLoadFailed: false,
        posReadinessLoading: false,
        posReadinessError: false,
        posReadinessNextAction: 'ready',
        posReadinessEffectiveRegisterId: validId,
        effectiveCashRegisterIdForPayment: validId,
      })
    ).toBe(false);
  });

  it('blocks while readiness is loading and no register id yet', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: true,
        cashRegisterResolved: true,
        settingsLoadFailed: false,
        posReadinessLoading: true,
        posReadinessError: false,
        posReadinessNextAction: null,
        posReadinessEffectiveRegisterId: null,
        effectiveCashRegisterIdForPayment: null,
      })
    ).toBe(true);
  });

  it('transitions to unblocked when readiness becomes ready (blocked → ready)', () => {
    const loading = computeRegisterGateBlockingPayment({
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
    expect(loading).toBe(true);

    const ready = computeRegisterGateBlockingPayment({
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
    expect(ready).toBe(false);
  });

  it('does not block when settings failed if ensure-ready is ready with valid id (server is source of truth)', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: true,
        cashRegisterResolved: true,
        settingsLoadFailed: true,
        posReadinessLoading: false,
        posReadinessError: false,
        posReadinessNextAction: 'ready',
        posReadinessEffectiveRegisterId: validId,
        effectiveCashRegisterIdForPayment: validId,
      })
    ).toBe(false);
  });

  it('still blocks when settings failed and ensure-ready does not waive (flag off)', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: false,
        cashRegisterResolved: true,
        settingsLoadFailed: true,
        posReadinessLoading: false,
        posReadinessError: false,
        posReadinessNextAction: 'ready',
        posReadinessEffectiveRegisterId: validId,
        effectiveCashRegisterIdForPayment: validId,
      })
    ).toBe(true);
  });

  it('requires POS ensure-ready flag to skip settings wait', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: false,
        cashRegisterResolved: false,
        settingsLoadFailed: false,
        posReadinessLoading: false,
        posReadinessError: false,
        posReadinessNextAction: 'ready',
        posReadinessEffectiveRegisterId: validId,
        effectiveCashRegisterIdForPayment: validId,
      })
    ).toBe(true);
  });

  it('blocks when ensure-ready is forbidden even if profile/settings carry a valid register id (conflict / actor-already-open)', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: true,
        cashRegisterResolved: true,
        settingsLoadFailed: false,
        posReadinessLoading: false,
        posReadinessError: false,
        posReadinessNextAction: 'forbidden',
        posReadinessEffectiveRegisterId: validId,
        effectiveCashRegisterIdForPayment: validId,
      })
    ).toBe(true);
  });
});
