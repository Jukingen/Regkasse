import { describe, expect, it } from 'vitest';

import {
  WEB_VITAL_BUDGETS_CLS,
  WEB_VITAL_BUDGETS_MS,
  budgetFor,
  exceedsBudget,
  sanitizeRoutePath,
} from '@/lib/monitoring/webVitalsBudgets';

describe('webVitalsBudgets', () => {
  it('uses Google good budgets for LCP / FCP / TTFB / INP / CLS', () => {
    expect(WEB_VITAL_BUDGETS_MS.LCP).toBe(2500);
    expect(WEB_VITAL_BUDGETS_MS.FCP).toBe(1800);
    expect(WEB_VITAL_BUDGETS_MS.TTFB).toBe(800);
    expect(WEB_VITAL_BUDGETS_MS.INP).toBe(200);
    expect(WEB_VITAL_BUDGETS_CLS.CLS).toBe(0.1);
  });

  it('flags LCP above 2.5s as degraded', () => {
    expect(exceedsBudget('LCP', 2500)).toBe(false);
    expect(exceedsBudget('LCP', 2500.1)).toBe(true);
    expect(budgetFor('LCP')).toBe(2500);
  });

  it('flags CLS above 0.1 as degraded', () => {
    expect(exceedsBudget('CLS', 0.1)).toBe(false);
    expect(exceedsBudget('CLS', 0.11)).toBe(true);
  });

  it('sanitizes routes without query or hash', () => {
    expect(sanitizeRoutePath('/payments?tenant=dev#x')).toBe('/payments');
    expect(sanitizeRoutePath(null)).toBe('/');
    expect(sanitizeRoutePath('')).toBe('/');
  });
});
