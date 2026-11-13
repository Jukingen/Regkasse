import { describe, expect, it } from '@jest/globals';

import { Breakpoints, isPosSplitLayout } from '../constants/breakpoints';

describe('isPosSplitLayout', () => {
  it('keeps phones on the stacked cash-register layout', () => {
    expect(isPosSplitLayout(0)).toBe(false);
    expect(isPosSplitLayout(Breakpoints.lg - 1)).toBe(false);
  });

  it('uses the split cash-register layout from the tablet breakpoint up', () => {
    expect(isPosSplitLayout(Breakpoints.lg)).toBe(true);
    expect(isPosSplitLayout(1280)).toBe(true);
  });
});
