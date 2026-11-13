import { resolveCategoryIcon } from '../utils/categoryDisplay';

describe('categoryDisplay', () => {
  it('uses the FA emoji when set', () => {
    expect(resolveCategoryIcon('🥗')).toBe('🥗');
    expect(resolveCategoryIcon('🍕')).toBe('🍕');
  });

  it('falls back to the default box when icon is missing', () => {
    expect(resolveCategoryIcon(undefined)).toBe('📦');
    expect(resolveCategoryIcon('')).toBe('📦');
    expect(resolveCategoryIcon('   ')).toBe('📦');
  });

  it('maps legacy Ionicons glyph names to emoji', () => {
    expect(resolveCategoryIcon('wine')).toBe('🍷');
    expect(resolveCategoryIcon('restaurant')).toBe('🍽️');
  });
});
