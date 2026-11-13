import { describe, expect, it } from 'vitest';

import {
  freshnessTagColor,
  proofAgeFreshnessTier,
} from '@/features/backup-dr/logic/recoverabilityPresentation';

describe('proofAgeFreshnessTier', () => {
  it('unknown for null', () => {
    expect(proofAgeFreshnessTier(null)).toBe('unknown');
  });

  it('recent under 24h', () => {
    expect(proofAgeFreshnessTier(3600)).toBe('recent');
  });

  it('aging between 24h and 7d', () => {
    expect(proofAgeFreshnessTier(90000)).toBe('aging');
  });

  it('stale after 7d', () => {
    expect(proofAgeFreshnessTier(8 * 86400)).toBe('stale');
  });
});

describe('freshnessTagColor', () => {
  it('maps tiers without green', () => {
    expect(freshnessTagColor('recent')).toBe('blue');
    expect(freshnessTagColor('stale')).toBe('red');
  });
});
