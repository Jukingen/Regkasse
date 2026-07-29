import {
  isGracePeriodWarningUrgent,
  shouldAutoShowGracePeriodModal,
} from '../utils/gracePeriodWarning';

describe('gracePeriodWarning helpers', () => {
  it('auto-shows modal when in grace and ≤5 days remain', () => {
    expect(shouldAutoShowGracePeriodModal(true, 5)).toBe(true);
    expect(shouldAutoShowGracePeriodModal(true, 3)).toBe(true);
    expect(shouldAutoShowGracePeriodModal(true, 0)).toBe(true);
    expect(shouldAutoShowGracePeriodModal(true, 6)).toBe(false);
    expect(shouldAutoShowGracePeriodModal(false, 2)).toBe(false);
  });

  it('marks urgent styling for ≤2 remaining days', () => {
    expect(isGracePeriodWarningUrgent(2)).toBe(true);
    expect(isGracePeriodWarningUrgent(1)).toBe(true);
    expect(isGracePeriodWarningUrgent(3)).toBe(false);
  });
});
