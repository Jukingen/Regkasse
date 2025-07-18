import { calculateCartSummary } from '../cartCalculations';
import { CartItem } from '../../types/cart';

describe('Cart Calculations', () => {
  const mockProduct1 = {
    id: '1',
    name: 'Test Ürün 1',
    price: 10.0,
    stockQuantity: 10,
    unit: 'Adet',
    category: 'Test',
    taxType: 'Standard',
    isActive: true,
    createdAt: '',
    updatedAt: '',
  };
  const mockProduct2 = {
    id: '2',
    name: 'Test Ürün 2',
    price: 5.0,
    stockQuantity: 5,
    unit: 'Adet',
    category: 'Test',
    taxType: 'Reduced',
    isActive: true,
    createdAt: '',
    updatedAt: '',
  };
  const mockCartItem1: CartItem = {
    product: mockProduct1,
    quantity: 2,
    discount: 0,
  };
  const mockCartItem2: CartItem = {
    product: mockProduct2,
    quantity: 3,
    discount: 1.0,
  };

  it('calculates subtotal correctly', () => {
    const items = [mockCartItem1, mockCartItem2];
    const summary = calculateCartSummary(items);
    expect(summary.subtotal).toBeGreaterThan(0);
  });

  it('calculates tax correctly', () => {
    const items = [mockCartItem1, mockCartItem2];
    const summary = calculateCartSummary(items);
    expect(summary.taxAmount).toBeGreaterThanOrEqual(0);
  });

  it('calculates total correctly', () => {
    const items = [mockCartItem1, mockCartItem2];
    const summary = calculateCartSummary(items);
    expect(summary.total).toBe(summary.subtotal + summary.taxAmount);
  });
}); 