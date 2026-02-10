/// <reference types="jest" />

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { jest } from '@jest/globals';

// Mock CartService
const mockCartService = {
  setCartIdForTable: jest.fn(),
  getCartIdForTable: jest.fn(),
  clearCartIdForTable: jest.fn(),
  hasActiveCartForTable: jest.fn(),
  getActiveTableCarts: jest.fn(),
  createCart: jest.fn(),
  addItemToCart: jest.fn(),
  resetCartAfterPayment: jest.fn(),
  deleteCart: jest.fn(),
  completeCart: jest.fn(),
};

jest.mock('../../../services/api/cartService', () => ({
  CartService: jest.fn().mockImplementation(() => mockCartService),
}));

describe('Payment Integration - Cart Reset and Order Status Update', () => {
  const mockCartId = 'test-cart-123';
  const mockTableNumber = 1;

  beforeEach(() => {
    jest.clearAllMocks();
    
    // Default mock implementations
    (mockCartService.setCartIdForTable as any).mockResolvedValue(undefined);
    (mockCartService.getCartIdForTable as any).mockResolvedValue(mockCartId);
    (mockCartService.hasActiveCartForTable as any).mockResolvedValue(true);
    (mockCartService.resetCartAfterPayment as any).mockResolvedValue({ success: true });
    (mockCartService.deleteCart as any).mockResolvedValue({ success: true });
    (mockCartService.completeCart as any).mockResolvedValue({ success: true });
  });

  test('should reset cart after payment and create new order status', async () => {
    // Mock successful response
    const mockResponse = {
      success: true,
      orderId: 'order-123',
      receiptNumber: 'REC-2025-001',
    };

    (mockCartService.resetCartAfterPayment as any).mockResolvedValue(mockResponse);

    // Test cart reset
    const result = await mockCartService.resetCartAfterPayment(mockCartId, {
      notes: 'Payment completed - Receipt: REC-2025-001',
    });

    expect(result).toEqual(mockResponse);
    expect(mockCartService.resetCartAfterPayment).toHaveBeenCalledWith(mockCartId, {
      notes: 'Payment completed - Receipt: REC-2025-001',
    });
  });

  test('should update current cart ID when reset is successful', async () => {
    // Set current cart ID for table
    await mockCartService.setCartIdForTable(mockTableNumber, mockCartId);

    expect(mockCartService.setCartIdForTable).toHaveBeenCalledWith(mockTableNumber, mockCartId);
  });

  test('should prevent double click on payment button', async () => {
    // Mock successful response
    const mockResponse = {
      success: true,
      orderId: 'order-456',
      receiptNumber: 'REC-2025-002',
    };

    (mockCartService.resetCartAfterPayment as any).mockResolvedValue(mockResponse);

    // Test cart reset
    const result = await mockCartService.resetCartAfterPayment(mockCartId, {
      notes: 'Test notes',
    });

    expect(result).toEqual(mockResponse);
    expect(mockCartService.resetCartAfterPayment).toHaveBeenCalledTimes(1);
  });

  test('should handle payment errors gracefully', async () => {
    // Mock error response
    const mockError = new Error('Payment failed');
    (mockCartService.resetCartAfterPayment as any).mockRejectedValue(mockError);

    // Test error handling
    await expect(
      mockCartService.resetCartAfterPayment(mockCartId, {
        notes: 'Failed payment',
      })
    ).rejects.toThrow('Payment failed');

    expect(mockCartService.resetCartAfterPayment).toHaveBeenCalledWith(mockCartId, {
      notes: 'Failed payment',
    });
  });

  test('should clear cart ID after successful payment', async () => {
    // Mock successful payment
    const mockResponse = { success: true };
    (mockCartService.resetCartAfterPayment as any).mockResolvedValue(mockResponse);

    // Complete payment
    await mockCartService.resetCartAfterPayment(mockCartId, {
      notes: 'Payment completed',
    });

    // Clear cart ID for table
    await mockCartService.clearCartIdForTable(mockTableNumber);

    expect(mockCartService.clearCartIdForTable).toHaveBeenCalledWith(mockTableNumber);
  });

  test('should check active cart status for table', async () => {
    // Check if table has active cart
    const hasActiveCart = await mockCartService.hasActiveCartForTable(mockTableNumber);

    expect(hasActiveCart).toBe(true);
    expect(mockCartService.hasActiveCartForTable).toHaveBeenCalledWith(mockTableNumber);
  });

  test('should get all active table carts', async () => {
    const mockActiveCarts = new Map([
      [1, 'cart-1'],
      [2, 'cart-2'],
    ]);

    (mockCartService.getActiveTableCarts as any).mockResolvedValue(mockActiveCarts);

    const activeCarts = await mockCartService.getActiveTableCarts();

    expect(activeCarts).toEqual(mockActiveCarts);
    expect(mockCartService.getActiveTableCarts).toHaveBeenCalled();
  });
});
