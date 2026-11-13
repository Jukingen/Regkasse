import { describe, expect, it } from 'vitest';

import type { ModifierGroupDto } from '@/lib/api/modifierGroups';

import { modifierGroupIdsFromAssigned } from '../modifierGroupHooks';

describe('modifierGroupIdsFromAssigned', () => {
  it('extracts camelCase and PascalCase ids', () => {
    const groups = [
      { id: 'a' },
      { Id: 'b' } as unknown as ModifierGroupDto,
      { id: '' },
    ] as ModifierGroupDto[];

    expect(modifierGroupIdsFromAssigned(groups)).toEqual(['a', 'b']);
  });
});
