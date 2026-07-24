import { describe, expect, it } from 'vitest';

import { contrastingTextColor } from '@/features/tax/utils/taxGroupButtonColors';
import {
  RECENT_TAX_GROUPS_MAX,
  pushRecentTaxGroupId,
  readRecentTaxGroupIds,
} from '@/features/tax/utils/recentTaxGroups';

describe('contrastingTextColor', () => {
  it('returns dark text for light backgrounds', () => {
    expect(contrastingTextColor('#ffffff')).toBe('rgba(0,0,0,0.88)');
    expect(contrastingTextColor('#ffe58f')).toBe('rgba(0,0,0,0.88)');
  });

  it('returns white text for dark backgrounds', () => {
    expect(contrastingTextColor('#1677ff')).toBe('#fff');
    expect(contrastingTextColor('#000000')).toBe('#fff');
  });
});

describe('recentTaxGroups', () => {
  it('stores most-recent first and caps length', () => {
    const tenant = `test-${Date.now()}`;
    expect(readRecentTaxGroupIds(tenant)).toEqual([]);

    pushRecentTaxGroupId('a', tenant);
    pushRecentTaxGroupId('b', tenant);
    pushRecentTaxGroupId('c', tenant);
    expect(readRecentTaxGroupIds(tenant)).toEqual(['c', 'b', 'a']);

    pushRecentTaxGroupId('b', tenant);
    expect(readRecentTaxGroupIds(tenant)[0]).toBe('b');

    for (let i = 0; i < RECENT_TAX_GROUPS_MAX + 3; i++) {
      pushRecentTaxGroupId(`id-${i}`, tenant);
    }
    expect(readRecentTaxGroupIds(tenant)).toHaveLength(RECENT_TAX_GROUPS_MAX);
    expect(readRecentTaxGroupIds(tenant)[0]).toBe(`id-${RECENT_TAX_GROUPS_MAX + 2}`);
  });
});
