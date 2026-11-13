import { describe, expect, it } from 'vitest';

import {
  calculateTaxFromNet,
  grossDifference,
  roundMoney,
} from '@/features/tax/utils/taxPreviewMath';

describe('taxPreviewMath', () => {
  it('rounds money to 2 decimals', () => {
    expect(roundMoney(1.005)).toBe(1.01);
    expect(roundMoney(10)).toBe(10);
  });

  it('calculates Austrian net-based VAT for 20%', () => {
    const result = calculateTaxFromNet(10, 20);
    expect(result).toEqual({ net: 10, ratePercent: 20, tax: 2, gross: 12 });
  });

  it('calculates 4.9% reduced rate', () => {
    const result = calculateTaxFromNet(10, 4.9);
    expect(result.tax).toBe(0.49);
    expect(result.gross).toBe(10.49);
  });

  it('computes gross difference', () => {
    const current = calculateTaxFromNet(10, 10);
    const next = calculateTaxFromNet(10, 20);
    expect(grossDifference(current.gross, next.gross)).toBe(1);
  });
});
