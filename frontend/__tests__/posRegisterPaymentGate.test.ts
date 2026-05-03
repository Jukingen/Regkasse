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

  it('does not block when settings failed if ensure-ready is ready with valid id (client gate; POST still uses ValidatePaymentRegisterAsync only)', () => {
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

  it('blocks when ensure-ready requires open_register (closed) even if client still holds a register id and picklist looked populated', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: true,
        cashRegisterResolved: true,
        settingsLoadFailed: false,
        posReadinessLoading: false,
        posReadinessError: false,
        posReadinessNextAction: 'open_register',
        posReadinessEffectiveRegisterId: '11111111-1111-1111-1111-111111111111',
        effectiveCashRegisterIdForPayment: validId,
      })
    ).toBe(true);
  });

  it('blocks when ensure-ready requires monatsbeleg_required even with a valid register id', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: true,
        cashRegisterResolved: true,
        settingsLoadFailed: false,
        posReadinessLoading: false,
        posReadinessError: false,
        posReadinessNextAction: 'monatsbeleg_required',
        posReadinessEffectiveRegisterId: validId,
        effectiveCashRegisterIdForPayment: validId,
      })
    ).toBe(true);
  });

  it('blocks when ensure-ready requires startbeleg_required even with a valid register id', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: true,
        cashRegisterResolved: true,
        settingsLoadFailed: false,
        posReadinessLoading: false,
        posReadinessError: false,
        posReadinessNextAction: 'startbeleg_required',
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

  it('blocks when ensure-ready is select_register and no payment register id (empty selectable / no assignment)', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: true,
        cashRegisterResolved: true,
        settingsLoadFailed: false,
        posReadinessLoading: false,
        posReadinessError: false,
        posReadinessNextAction: 'select_register',
        posReadinessEffectiveRegisterId: null,
        effectiveCashRegisterIdForPayment: null,
      })
    ).toBe(true);
  });

  it('does not unblock select_register solely from profile id when ensure-ready still requires selection', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: true,
        cashRegisterResolved: true,
        settingsLoadFailed: false,
        posReadinessLoading: false,
        posReadinessError: false,
        posReadinessNextAction: 'select_register',
        posReadinessEffectiveRegisterId: null,
        effectiveCashRegisterIdForPayment: validId,
      })
    ).toBe(true);
  });

  it('after readiness cache cleared for refetch (null nextAction), valid register id unblocks pay (post-assignment refresh path)', () => {
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
        effectiveCashRegisterIdForPayment: validId,
      })
    ).toBe(false);

    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: true,
        cashRegisterResolved: true,
        settingsLoadFailed: false,
        posReadinessLoading: false,
        posReadinessError: false,
        posReadinessNextAction: null,
        posReadinessEffectiveRegisterId: null,
        effectiveCashRegisterIdForPayment: validId,
      })
    ).toBe(false);
  });

  it('settings failed + readiness error + no register id: remains blocked', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: true,
        cashRegisterResolved: true,
        settingsLoadFailed: true,
        posReadinessLoading: false,
        posReadinessError: true,
        posReadinessNextAction: undefined,
        posReadinessEffectiveRegisterId: null,
        effectiveCashRegisterIdForPayment: null,
      })
    ).toBe(true);
  });

  it('settings failed + readiness error + valid register id (server-accepted persist): does not block on settings alone', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: true,
        cashRegisterResolved: true,
        settingsLoadFailed: true,
        posReadinessLoading: false,
        posReadinessError: true,
        posReadinessNextAction: undefined,
        posReadinessEffectiveRegisterId: null,
        effectiveCashRegisterIdForPayment: validId,
      })
    ).toBe(false);
  });

  it('ensure-ready off + settings failed + valid id: still blocked (no server waiver path)', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: false,
        cashRegisterResolved: true,
        settingsLoadFailed: true,
        posReadinessLoading: false,
        posReadinessError: true,
        posReadinessNextAction: undefined,
        posReadinessEffectiveRegisterId: null,
        effectiveCashRegisterIdForPayment: validId,
      })
    ).toBe(true);
  });

  it('settings failed + select_register + valid id: still blocked until cached ensure-ready is ready (POST does not read nextAction)', () => {
    expect(
      computeRegisterGateBlockingPayment({
        enabled: true,
        posEnsureReadyOnEntry: true,
        cashRegisterResolved: true,
        settingsLoadFailed: true,
        posReadinessLoading: false,
        posReadinessError: false,
        posReadinessNextAction: 'select_register',
        posReadinessEffectiveRegisterId: null,
        effectiveCashRegisterIdForPayment: validId,
      })
    ).toBe(true);
  });

  describe('temporal: stale ensure-ready snapshot vs post-assignment refresh (no sleeps)', () => {
    it('manual assignment merged id does not unblock while cached nextAction is forbidden (stale readiness)', () => {
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

    it('after refresh fix: same payment id with nextAction ready enables pay (was blocked only by stale cache)', () => {
      expect(
        computeRegisterGateBlockingPayment({
          enabled: true,
          posEnsureReadyOnEntry: true,
          cashRegisterResolved: true,
          settingsLoadFailed: false,
          posReadinessLoading: false,
          posReadinessError: false,
          posReadinessNextAction: 'ready',
          posReadinessEffectiveRegisterId: validId,
          effectiveCashRegisterIdForPayment: validId,
        })
      ).toBe(false);
    });

    it('manual assignment id present but select_register cache still blocks until ensure-ready refetch', () => {
      expect(
        computeRegisterGateBlockingPayment({
          enabled: true,
          posEnsureReadyOnEntry: true,
          cashRegisterResolved: true,
          settingsLoadFailed: false,
          posReadinessLoading: false,
          posReadinessError: false,
          posReadinessNextAction: 'select_register',
          posReadinessEffectiveRegisterId: null,
          effectiveCashRegisterIdForPayment: validId,
        })
      ).toBe(true);
    });

    it('after refresh fix: select_register cleared to ready with effective id unblocks', () => {
      expect(
        computeRegisterGateBlockingPayment({
          enabled: true,
          posEnsureReadyOnEntry: true,
          cashRegisterResolved: true,
          settingsLoadFailed: false,
          posReadinessLoading: false,
          posReadinessError: false,
          posReadinessNextAction: 'ready',
          posReadinessEffectiveRegisterId: validId,
          effectiveCashRegisterIdForPayment: validId,
        })
      ).toBe(false);
    });
  });
});
