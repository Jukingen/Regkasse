import { describe, expect, it } from '@jest/globals';

import { computeTaxiSuggestedAmount } from '../services/taxiFare';

describe('computeTaxiSuggestedAmount', () => {
  it('uses km times tariff when no manual amount is set', () => {
    expect(computeTaxiSuggestedAmount(10, 2.4, null)).toBe(24);
  });

  it('prefers a manual amount over the tariff', () => {
    expect(computeTaxiSuggestedAmount(10, 2.4, 18.5)).toBe(18.5);
  });
});
