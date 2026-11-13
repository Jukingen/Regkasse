import { describe, expect, it } from 'vitest';

import {
  comparePermissionSets,
  diffKindToHighlight,
} from '../permissionRoleDiff';

describe('permissionRoleDiff', () => {
  it('classifies same, onlyBase, and onlyCompare', () => {
    const diff = comparePermissionSets(
      ['sale.view', 'user.view', 'report.view'],
      ['sale.view', 'payment.view']
    );
    expect(diff.same).toEqual(['sale.view']);
    expect(diff.onlyBase).toEqual(['report.view', 'user.view']);
    expect(diff.onlyCompare).toEqual(['payment.view']);
    expect(diff.differenceCount).toBe(3);
    expect(diff.byPermission.get('sale.view')).toBe('same');
    expect(diff.byPermission.get('user.view')).toBe('onlyBase');
    expect(diff.byPermission.get('payment.view')).toBe('onlyCompare');
  });

  it('returns empty diff for identical sets', () => {
    const diff = comparePermissionSets(['a', 'b'], ['b', 'a']);
    expect(diff.differenceCount).toBe(0);
    expect(diff.same).toEqual(['a', 'b']);
  });

  it('maps kinds to highlights', () => {
    expect(diffKindToHighlight('onlyBase')).toBe('added');
    expect(diffKindToHighlight('onlyCompare')).toBe('removed');
    expect(diffKindToHighlight('same')).toBe('same');
    expect(diffKindToHighlight(undefined)).toBeUndefined();
  });
});
