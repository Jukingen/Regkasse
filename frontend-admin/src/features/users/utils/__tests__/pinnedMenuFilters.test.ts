import { beforeEach, describe, expect, it } from 'vitest';

import {
  loadPinnedMenuFilters,
  savePinnedMenuFilters,
  togglePinnedMenuFilter,
} from '@/features/users/utils/pinnedMenuFilters';

describe('pinnedMenuFilters', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('loads empty / invalid storage safely', () => {
    expect(loadPinnedMenuFilters()).toEqual([]);
    window.localStorage.setItem('fa_pinned_menu_filters_v1', '{nope');
    expect(loadPinnedMenuFilters()).toEqual([]);
    window.localStorage.setItem('fa_pinned_menu_filters_v1', JSON.stringify([1, 'ok', '']));
    expect(loadPinnedMenuFilters()).toEqual(['ok']);
  });

  it('saves and toggles pins', () => {
    savePinnedMenuFilters(['a', 'b']);
    expect(loadPinnedMenuFilters()).toEqual(['a', 'b']);
    expect(togglePinnedMenuFilter('c', ['a', 'b'])).toEqual(['a', 'b', 'c']);
    expect(togglePinnedMenuFilter('a', ['a', 'b', 'c'])).toEqual(['b', 'c']);
  });
});
