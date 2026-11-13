import { describe, expect, it } from 'vitest';

import { evaluatePasswordStrength } from '../passwordStrength';

describe('evaluatePasswordStrength', () => {
  it('scores generated-style passwords highly', () => {
    const result = evaluatePasswordStrength('Ab3!xY9#mK2$pL4n');
    expect(result.level).toBeGreaterThanOrEqual(3);
    expect(result.percent).toBeGreaterThan(0);
  });

  it('returns empty for blank password', () => {
    expect(evaluatePasswordStrength('').level).toBe(0);
  });
});
