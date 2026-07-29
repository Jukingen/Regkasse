/// <reference types="jest" />

import { mapPosLicenseChipState, posLicenseChipColor } from '../utils/posLicenseStatusChip';
import type { MandantLicenseWarningState } from '../types/mandantLicenseWarning';
import type { LicenseStatus } from '../services/license/licenseStatusCache';

function mandant(partial: Partial<MandantLicenseWarningState>): MandantLicenseWarningState {
  return {
    daysRemaining: 40,
    daysOverdue: 0,
    gracePeriodRemaining: 0,
    isInGracePeriod: false,
    isLocked: false,
    canAccess: true,
    validUntil: '2026-08-01T00:00:00.000Z',
    ...partial,
  };
}

function deployment(partial: Partial<LicenseStatus> = {}): LicenseStatus {
  return {
    isValid: true,
    isTrial: false,
    isExpired: false,
    daysRemaining: 40,
    expiryDate: '2026-08-01T00:00:00.000Z',
    machineHash: 'abc',
    ...partial,
  };
}

describe('mapPosLicenseChipState', () => {
  it('returns Active with days remaining', () => {
    expect(
      mapPosLicenseChipState({
        mandant: mandant({ daysRemaining: 25 }),
        deployment: deployment({ daysRemaining: 25 }),
      })
    ).toEqual({
      state: 'Active',
      displayDays: 25,
      expiresAt: '2026-08-01T00:00:00.000Z',
    });
  });

  it('returns Grace with gracePeriodRemaining', () => {
    expect(
      mapPosLicenseChipState({
        mandant: mandant({
          isInGracePeriod: true,
          gracePeriodRemaining: 5,
          daysRemaining: 0,
          daysOverdue: 2,
        }),
        shouldShowGrace: true,
        deployment: deployment({ isExpired: true, daysRemaining: 0 }),
      })
    ).toEqual({
      state: 'Grace',
      displayDays: 5,
      expiresAt: '2026-08-01T00:00:00.000Z',
    });
  });

  it('returns Locked when mandant locked', () => {
    expect(
      mapPosLicenseChipState({
        mandant: mandant({
          isLocked: true,
          canAccess: false,
          daysOverdue: 10,
          daysRemaining: 0,
        }),
        deployment: deployment({ isExpired: true }),
      })
    ).toMatchObject({
      state: 'Locked',
      displayDays: 10,
    });
  });

  it('falls back to deployment-only Active when mandant is null', () => {
    expect(
      mapPosLicenseChipState({
        mandant: null,
        deployment: deployment({ daysRemaining: 12 }),
      })
    ).toEqual({
      state: 'Active',
      displayDays: 12,
      expiresAt: '2026-08-01T00:00:00.000Z',
    });
  });

  it('locks when deployment expired without grace', () => {
    expect(
      mapPosLicenseChipState({
        mandant: null,
        deployment: deployment({ isExpired: true, daysRemaining: 0 }),
      }).state
    ).toBe('Locked');
  });
});

describe('posLicenseChipColor', () => {
  it('returns lifecycle colors', () => {
    expect(posLicenseChipColor('Active')).toBe('#52c41a');
    expect(posLicenseChipColor('Grace')).toBe('#faad14');
    expect(posLicenseChipColor('Locked')).toBe('#cf1322');
  });
});
