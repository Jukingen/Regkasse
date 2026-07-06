import { describe, expect, it } from 'vitest';
import { mapUiProductToApi } from '@/features/products/utils/productMapper';

describe('mapUiProductToApi', () => {
  it('sets canonical description from Turkish when German is empty', () => {
    const payload = mapUiProductToApi({
      id: 'p1',
      name: 'pizza',
      nameDe: 'pizza',
      price: 10,
      categoryId: 'cat-1',
      category: 'Pizza',
      descriptionDe: undefined,
      descriptionTr: 'tesstt',
    } as never);

    expect(payload.description).toBe('tesstt');
    expect(payload.descriptionTr).toBe('tesstt');
    expect(payload.descriptionDe).toBeNull();
  });

  it('never sends null canonical description', () => {
    const payload = mapUiProductToApi({
      id: 'p1',
      name: 'Cola',
      nameDe: 'Cola',
      price: 2,
      categoryId: 'cat-1',
      category: 'Drinks',
    } as never);

    expect(payload.description).toBe('');
  });

  it('trims localized description fields and drops blank values', () => {
    const payload = mapUiProductToApi({
      id: 'p1',
      name: 'Salad',
      nameDe: 'Salat',
      price: 5,
      categoryId: 'cat-1',
      category: 'Salate',
      descriptionDe: '  mit Dressing  ',
      descriptionEn: '   ',
    } as never);

    expect(payload.description).toBe('mit Dressing');
    expect(payload.descriptionDe).toBe('mit Dressing');
    expect(payload.descriptionEn).toBeNull();
  });
});
