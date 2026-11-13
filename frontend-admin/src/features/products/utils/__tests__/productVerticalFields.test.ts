import { describe, expect, it } from 'vitest';

import { shouldShowDurationAndStaffFields } from '@/features/products/utils/productVerticalFields';

describe('shouldShowDurationAndStaffFields', () => {
  it('hides fields for gastronomy-style profiles', () => {
    expect(
      shouldShowDurationAndStaffFields({
        kitchenDisplay: true,
        tables: true,
        patientRecord: false,
      })
    ).toBe(false);
    expect(shouldShowDurationAndStaffFields(undefined)).toBe(false);
  });

  it('shows fields when serviceDuration or appointment is enabled', () => {
    expect(shouldShowDurationAndStaffFields({ serviceDuration: true })).toBe(true);
    expect(shouldShowDurationAndStaffFields({ appointment: true })).toBe(true);
    expect(
      shouldShowDurationAndStaffFields({
        serviceDuration: true,
        appointment: true,
      })
    ).toBe(true);
  });
});
