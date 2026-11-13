import { describe, expect, it } from '@jest/globals';

import { resolveMonatsbelegBannerState } from '../utils/posMonatsbelegBannerState';

describe('resolveMonatsbelegBannerState', () => {
  it('Strict + missing + canCreate → red, no dismiss, create shown', () => {
    const state = resolveMonatsbelegBannerState({
      readiness: {
        nextAction: 'monatsbeleg_required',
        monatsbelegBlockingMode: 'Strict',
        monatsbelegSalesBlocked: true,
        monatsbelegWarningLevel: 'red',
      },
      canCreate: true,
      dismissed: false,
    });
    expect(state).toMatchObject({
      visible: true,
      tone: 'red',
      bodyKey: 'bodyStrict',
      isHardBlocked: true,
      canDismiss: false,
      showCreate: true,
    });
  });

  it('Strict + missing + no permission → contact only (no create)', () => {
    const state = resolveMonatsbelegBannerState({
      readiness: {
        nextAction: 'monatsbeleg_required',
        monatsbelegBlockingMode: 'Strict',
        monatsbelegSalesBlocked: true,
      },
      canCreate: false,
      dismissed: false,
    });
    expect(state.showCreate).toBe(false);
    expect(state.canDismiss).toBe(false);
    expect(state.visible).toBe(true);
  });

  it('GracePeriod warningLevel red (days 1–7) → red, dismissible, sales not hard-blocked', () => {
    const state = resolveMonatsbelegBannerState({
      readiness: {
        nextAction: 'ready',
        monatsbelegBlockingMode: 'GracePeriod',
        monatsbelegSalesBlocked: false,
        monatsbelegCanContinueWithWarning: true,
        monatsbelegWarningLevel: 'red',
      },
      canCreate: true,
      dismissed: false,
    });
    expect(state).toMatchObject({
      visible: true,
      tone: 'red',
      bodyKey: 'bodyGrace',
      isHardBlocked: false,
      canDismiss: true,
      showCreate: true,
    });
  });

  it('GracePeriod warningLevel yellow (days 8–14) → yellow, dismissible', () => {
    const state = resolveMonatsbelegBannerState({
      readiness: {
        nextAction: 'ready',
        monatsbelegBlockingMode: 'GracePeriod',
        monatsbelegSalesBlocked: false,
        monatsbelegCanContinueWithWarning: true,
        monatsbelegWarningLevel: 'yellow',
      },
      canCreate: true,
      dismissed: false,
    });
    expect(state.tone).toBe('yellow');
    expect(state.canDismiss).toBe(true);
    expect(state.isHardBlocked).toBe(false);
  });

  it('GracePeriod day 15+ (sales blocked) → same as Strict', () => {
    const state = resolveMonatsbelegBannerState({
      readiness: {
        nextAction: 'monatsbeleg_required',
        monatsbelegBlockingMode: 'GracePeriod',
        monatsbelegSalesBlocked: true,
        monatsbelegWarningLevel: 'red',
      },
      canCreate: true,
      dismissed: false,
    });
    expect(state.isHardBlocked).toBe(true);
    expect(state.canDismiss).toBe(false);
    expect(state.tone).toBe('red');
    expect(state.bodyKey).toBe('bodyStrict');
  });

  it('WarningOnly → yellow banner, dismissible, sales allowed', () => {
    const state = resolveMonatsbelegBannerState({
      readiness: {
        nextAction: 'ready',
        monatsbelegBlockingMode: 'WarningOnly',
        monatsbelegSalesBlocked: false,
        monatsbelegCanContinueWithWarning: true,
        monatsbelegWarningLevel: 'red',
      },
      canCreate: true,
      dismissed: false,
    });
    expect(state).toMatchObject({
      visible: true,
      tone: 'yellow',
      bodyKey: 'bodyWarning',
      isHardBlocked: false,
      canDismiss: true,
    });
  });

  it('dismiss hides only when not hard-blocked', () => {
    const grace = resolveMonatsbelegBannerState({
      readiness: {
        nextAction: 'ready',
        monatsbelegBlockingMode: 'WarningOnly',
        monatsbelegCanContinueWithWarning: true,
        monatsbelegWarningLevel: 'yellow',
      },
      canCreate: true,
      dismissed: true,
    });
    expect(grace.visible).toBe(false);

    const strict = resolveMonatsbelegBannerState({
      readiness: {
        nextAction: 'monatsbeleg_required',
        monatsbelegBlockingMode: 'Strict',
        monatsbelegSalesBlocked: true,
      },
      canCreate: true,
      dismissed: true,
    });
    expect(strict.visible).toBe(true);
  });
});
