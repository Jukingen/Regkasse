import { describe, expect, it } from 'vitest';

import { buildStableRowKeys, compositeRowKeyPart, entityIdRowKey } from '../adminTableRowKey';

describe('adminTableRowKey', () => {
  it('joins parts and treats empty as underscore', () => {
    expect(compositeRowKeyPart(null)).toBe('_');
    expect(compositeRowKeyPart('')).toBe('_');
    expect(compositeRowKeyPart('abc')).toBe('abc');
  });

  it('disambiguates identical composites without using array index as the base key', () => {
    const rows = [
      { name: 'A', qty: 1 },
      { name: 'B', qty: 1 },
      { name: 'A', qty: 1 },
    ];
    expect(buildStableRowKeys(rows, (r) => [r.name, r.qty])).toEqual(['A|1', 'B|1', 'A|1#2']);
  });

  it('prefers entity id and avoids empty-string collision', () => {
    expect(entityIdRowKey('uuid-1', ['user'])).toBe('uuid-1');
    expect(entityIdRowKey('', ['user', 'alice'])).toBe('missing|user|alice');
    expect(entityIdRowKey(null, ['user', null])).toBe('missing|user|_');
  });
});
