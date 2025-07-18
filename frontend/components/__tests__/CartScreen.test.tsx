import { render, fireEvent } from '@testing-library/react-native';
import React from 'react';

import { CartItem } from '../../types/cart';
import CartScreen from '../CartScreen';

describe('CartScreen', () => {
  const mockProduct = {
    id: '1',
    name: 'Test Ürün',
    price: 10.5,
    stockQuantity: 5,
    unit: 'Adet',
    category: 'Test Kategori',
    taxType: 'Standard',
    isActive: true,
    createdAt: '',
    updatedAt: '',
  };

  const mockCartItem: CartItem = {
    product: mockProduct,
    quantity: 2,
    discount: 0,
  };

  const mockProps = {
    items: [mockCartItem],
    onUpdateQuantity: jest.fn(),
    onRemoveItem: jest.fn(),
    onClearCart: jest.fn(),
    onCheckout: jest.fn(),
    isLoading: false,
    error: null,
    onRetry: jest.fn(),
  };

  it('renders cart items correctly', () => {
    const { getByText } = render(<CartScreen {...mockProps} />);
    expect(getByText('Test Ürün')).toBeTruthy();
    expect(getByText('2')).toBeTruthy();
    expect(getByText('10.50 €')).toBeTruthy();
  });

  it('calls onUpdateQuantity when plus button is pressed', () => {
    const { getAllByA11yRole } = render(<CartScreen {...mockProps} />);
    const plusButtons = getAllByA11yRole('button');
    fireEvent.press(plusButtons[1]); // plus button
    expect(mockProps.onUpdateQuantity).toHaveBeenCalled();
  });

  it('calls onClearCart when clear button is pressed', () => {
    const { getByText } = render(<CartScreen {...mockProps} />);
    const clearButton = getByText('Temizle');
    fireEvent.press(clearButton);
    expect(mockProps.onClearCart).toHaveBeenCalled();
  });
}); 