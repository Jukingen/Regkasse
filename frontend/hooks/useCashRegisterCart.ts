import { useState, useCallback } from 'react';

import { useAuth } from '../contexts/AuthContext';
import { useCart } from './useCart';
import { usePaymentMethods } from './usePaymentMethods';

import { cartService } from '../services/api/cartService';

// English Description: Cash register cart/table/toast operations. Current-register identity lives in useCashRegister.ts.
// Payment execution lives in PaymentModal + paymentService (/api/pos/payment), not here.

interface ToastNotification {
  id: string;
  type: 'success' | 'error' | 'info' | 'warning';
  message: string;
  duration?: number;
}

export const useCashRegisterCart = () => {
  const { user } = useAuth();
  const {
    getCartForTable,
    clearCart,
    removeFromCart,
    updateItemQuantity: updateCartItemQuantity,
    loadCartForTable: loadCartFromHook,
  } = useCart();
  const { paymentMethods, getPaymentMethod } = usePaymentMethods(user);

  const [activeTableNumber, setActiveTableNumber] = useState<number | null>(null);

  const [toasts, setToasts] = useState<ToastNotification[]>([]);

  const addToast = useCallback((type: ToastNotification['type'], message: string, duration: number = 5000) => {
    const id = Date.now().toString();
    const newToast: ToastNotification = { id, type, message, duration };

    setToasts((prev) => [...prev, newToast]);

    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, duration);
  }, []);

  const removeToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((toast) => toast.id !== id));
  }, []);

  const clearToasts = useCallback(() => {
    setToasts([]);
  }, []);

  const loadCartForTable = useCallback(
    async (tableNumber: number) => {
      if (!tableNumber) {
        console.error('❌ Table number is required for loading cart');
        addToast('error', 'Table number is required');
        return;
      }

      try {
        addToast('info', `Switching to table ${tableNumber}...`, 2000);
        console.log('🔄 Switching to table', tableNumber);

        const result = await loadCartFromHook(tableNumber);

        if (result.success) {
          addToast('success', `Now serving table ${tableNumber}`, 2000);
          setActiveTableNumber(tableNumber);
        } else {
          addToast('warning', `Table ${tableNumber} loaded with fallback mode`, 3000);
          setActiveTableNumber(tableNumber);
        }
      } catch (error) {
        const errorMessage = 'Failed to switch table';
        addToast('error', errorMessage);
        console.error('❌ Error switching table:', error);
      }
    },
    [addToast, loadCartFromHook]
  );

  const updateItemQuantity = useCallback(
    (itemId: string, quantity: number, tableNumber: number) => {
      if (!tableNumber) {
        console.error('❌ Table number is required for updating item quantity');
        addToast('error', 'Table number is required');
        return;
      }

      if (quantity <= 0) {
        removeFromCart(tableNumber, itemId);
        addToast('info', 'Item removed from cart', 2000);
      } else {
        updateCartItemQuantity(tableNumber, itemId, quantity);
        addToast('success', 'Cart updated successfully', 2000);
      }
    },
    [removeFromCart, updateCartItemQuantity, addToast]
  );

  const removeItem = useCallback(
    (itemId: string, tableNumber: number) => {
      if (!tableNumber) {
        console.error('❌ Table number is required for removing item');
        addToast('error', 'Table number is required');
        return;
      }

      removeFromCart(tableNumber, itemId);
      addToast('info', 'Item removed from cart', 2000);
    },
    [removeFromCart, addToast]
  );

  const clearCurrentCart = useCallback(
    async (tableNumber: number): Promise<{ success: boolean; message: string }> => {
      if (!tableNumber) {
        console.error('❌ Table number is required for clearing cart');
        addToast('error', 'Table number is required');
        return { success: false, message: 'Table number is required' };
      }

      try {
        const result = await clearCart(tableNumber);
        setActiveTableNumber(null);

        if (result && result.success) {
          return { success: true, message: 'Cart cleared successfully' };
        }
        return { success: false, message: result?.message || 'Failed to clear cart' };
      } catch (error) {
        console.error('❌ Error clearing cart:', error);
        const errorMessage = error instanceof Error ? error.message : 'Failed to clear cart';
        addToast('error', errorMessage, 3000);
        return { success: false, message: errorMessage };
      }
    },
    [clearCart, addToast]
  );

  const resetCart = useCallback(
    (tableNumber?: number) => {
      if (tableNumber) {
        clearCart(tableNumber);
      }
      setActiveTableNumber(null);
      addToast('info', 'Cart reset', 2000);
    },
    [clearCart, addToast]
  );

  const resetCartAndUpdateOrderStatus = useCallback(
    async (invoiceId: string, receiptNumber: string, tableNumber: number) => {
      if (!user?.token) {
        addToast('error', 'User not authenticated for cart reset');
        return;
      }

      if (!tableNumber) {
        console.error('❌ Table number is required for resetting cart');
        addToast('error', 'Table number is required');
        return;
      }

      try {
        addToast('info', 'Resetting cart and updating order status...', 2000);

        const currentCart = getCartForTable(tableNumber);
        if (currentCart?.cartId) {
          const resetResult = await cartService.resetCartAfterPayment(
            currentCart.cartId,
            `Payment completed - Receipt: ${receiptNumber}`
          );
          console.log('✅ Cart reset and order status updated via API:', resetResult);

          if (resetResult.newCartId) {
            console.log('🆕 New cart ID assigned:', resetResult.newCartId);
          }
        }

        await clearCart(tableNumber);
        setActiveTableNumber(null);

        addToast('success', `Cart reset and new order ready. Receipt: ${receiptNumber}`, 5000);

        console.log('✅ Cart reset and order status update completed:', {
          invoiceId,
          receiptNumber,
          tableNumber,
          timestamp: new Date().toISOString(),
        });
      } catch (error) {
        console.error('❌ Error during cart reset and order status update:', error);

        let errorMessage = 'Failed to reset cart via API';
        if (error instanceof Error) {
          errorMessage = error.message;
        }

        addToast('error', errorMessage);

        await clearCart(tableNumber);
        setActiveTableNumber(null);
      }
    },
    [user?.token, getCartForTable, clearCart, addToast]
  );

  const getPaymentMethodInfo = useCallback(
    (method: string) => {
      return getPaymentMethod(method);
    },
    [getPaymentMethod]
  );

  const isTseRequired = useCallback(
    (method: string) => {
      const methodInfo = getPaymentMethod(method);
      return methodInfo?.requiresTse || false;
    },
    [getPaymentMethod]
  );

  return {
    activeTableNumber,
    toasts,

    loadCartForTable,
    updateItemQuantity,
    removeItem,
    clearCurrentCart,
    resetCart,
    resetCartAndUpdateOrderStatus,
    getPaymentMethodInfo,
    isTseRequired,

    addToast,
    removeToast,
    clearToasts,

    paymentMethods,
  };
};
