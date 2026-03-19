import {
  extractPaymentMethodsArrayFromApiBody,
  normalizeToPosPaymentMethods,
  unwrapApiResponseLayer,
} from '../services/api/normalizePosPaymentMethods';

describe('normalizePosPaymentMethods', () => {
  it('extracts array from SuccessResponse envelope (data)', () => {
    const body = {
      success: true,
      message: 'ok',
      data: [{ id: 'cash', name: 'Nakit', type: 'cash', icon: 'cash-outline' }],
    };
    expect(extractPaymentMethodsArrayFromApiBody(body)).toHaveLength(1);
    expect(normalizeToPosPaymentMethods(body)).toEqual([
      { id: 'cash', name: 'Nakit', type: 'cash', icon: 'cash-outline' },
    ]);
  });

  it('accepts a bare array', () => {
    const rows = [{ id: 'card', name: 'Kart', type: 'card', icon: 'card-outline' }];
    expect(normalizeToPosPaymentMethods(rows)).toEqual(rows);
  });

  it('drops rows without id', () => {
    expect(normalizeToPosPaymentMethods({ data: [{ name: 'x' }] })).toEqual([]);
  });

  it('unwrapApiResponseLayer peels Value, value, and data', () => {
    expect(unwrapApiResponseLayer({ Value: { a: 1 } })).toEqual({ a: 1 });
    expect(unwrapApiResponseLayer({ value: { a: 2 } })).toEqual({ a: 2 });
    expect(unwrapApiResponseLayer({ data: { b: 2 } })).toEqual({ b: 2 });
  });

  it('prefers data array over legacy methods key when both present', () => {
    const body = {
      data: [{ id: 'a', name: 'A', type: 'cash', icon: 'i' }],
      methods: [{ id: 'b', name: 'B', type: 'card', icon: 'j' }],
    };
    expect(normalizeToPosPaymentMethods(body).map((m) => m.id)).toEqual(['a']);
  });
});
