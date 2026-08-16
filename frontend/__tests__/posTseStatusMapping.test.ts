import { describe, expect, it } from '@jest/globals';

import { toOperationalHealthFromPosTse } from '../utils/posTseStatus';

describe('toOperationalHealthFromPosTse', () => {
  it('prefers explicit operationalHealth', () => {
    expect(toOperationalHealthFromPosTse('Active', 'Degraded')).toBe('Degraded');
  });

  it('maps cashier indicator when operationalHealth is missing', () => {
    expect(toOperationalHealthFromPosTse('Active')).toBe('Online');
    expect(toOperationalHealthFromPosTse('Degraded')).toBe('Degraded');
    expect(toOperationalHealthFromPosTse('Inactive')).toBe('Offline');
  });
});
