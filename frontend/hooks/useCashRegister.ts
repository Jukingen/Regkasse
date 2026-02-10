import { useState, useCallback } from 'react';

import { useAuth } from '../contexts/AuthContext';
import { useCart } from './useCart';
import { usePaymentMethods } from './usePaymentMethods';

import { cartService } from '../services/api/cartService';
import { apiClient } from '../services/api/config'; // ✅ YENİ: apiClient import edildi

// English Description: Simple and reliable hook for cash register operations. Only works with backend, does not use local storage.

// Payment request interface
interface PaymentRequest {
  cartId: string;
  totalAmount: number;
  paymentMethod: string;
  customerId?: string;
  customerName?: string;
  customerEmail?: string;
  customerPhone?: string;
  notes?: string;
  tseRequired: boolean;
  taxNumber?: string;
  tableNumber?: number; // Masa numarası eklendi
}

// Payment response interface
interface PaymentResponse {
  success: boolean;
  message: string;
  invoiceId?: string;
  receiptNumber?: string;
  tseSignature?: string;
}

// Toast notification interface
interface ToastNotification {
  id: string;
  type: 'success' | 'error' | 'info' | 'warning';
  message: string;
  duration?: number;
}

export const useCashRegister = () => {
  const { user } = useAuth();
  const { 
    getCartForTable, 
    clearCart, 
    removeFromCart, 
    updateItemQuantity: updateCartItemQuantity,
    loadCartForTable: loadCartFromHook 
  } = useCart();
  const { paymentMethods, getPaymentMethod } = usePaymentMethods(user);
  
  const [activeTableNumber, setActiveTableNumber] = useState<number | null>(null);
  const [paymentProcessing, setPaymentProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  // Çift tıklama koruması için state
  const [preventDoubleClick, setPreventDoubleClick] = useState(false);
  
  // Toast notifications state
  const [toasts, setToasts] = useState<ToastNotification[]>([]);

  // Add toast notification
  const addToast = useCallback((type: ToastNotification['type'], message: string, duration: number = 5000) => {
    const id = Date.now().toString();
    const newToast: ToastNotification = { id, type, message, duration };
    
    setToasts(prev => [...prev, newToast]);
    
    // Auto remove toast after duration
    setTimeout(() => {
      removeToast(id);
    }, duration);
  }, []);

  // Remove toast notification
  const removeToast = useCallback((id: string) => {
    setToasts(prev => prev.filter(toast => toast.id !== id));
  }, []);

  // Clear all toasts
  const clearToasts = useCallback(() => {
    setToasts([]);
  }, []);

  // Process payment with real-time notifications
  const processPayment = useCallback(async (paymentData: PaymentRequest): Promise<PaymentResponse> => {
    if (!user?.token) {
      const errorMsg = 'User not authenticated';
      addToast('error', errorMsg);
      setError(errorMsg);
      return { success: false, message: errorMsg };
    }

    if (!paymentData.tableNumber) {
      const errorMsg = 'Table number is required for payment';
      addToast('error', errorMsg);
      setError(errorMsg);
      return { success: false, message: errorMsg };
    }

    // Çift tıklama koruması
    if (preventDoubleClick) {
      console.log('⚠️ Payment already in progress, ignoring duplicate click');
      return { success: false, message: 'Payment already in progress' };
    }

    setPreventDoubleClick(true);
    setPaymentProcessing(true);
    setError(null);
    
    // Timeout koruması (5 dakika)
    const timeoutId = setTimeout(() => {
      console.log('⚠️ Payment timeout - resetting states');
      setPaymentProcessing(false);
      setPreventDoubleClick(false);
      setError('Payment timeout - please try again');
      addToast('error', 'Payment timeout - please try again', 5000);
    }, 5 * 60 * 1000); // 5 dakika
    
    // Show payment initiation notification
    addToast('info', 'Payment process started...', 2000);

    try {
      // Step 1: Initiate payment (/api/Payment/initiate)
      addToast('info', 'Initiating payment session...', 2000);
      
      // ✅ YENİ: apiClient kullanımı - token management otomatik
      const initiateResult = await apiClient.post<any>('/Payment/initiate', {
        cartId: paymentData.cartId,
        totalAmount: paymentData.totalAmount,
        paymentMethod: paymentData.paymentMethod,
        customerId: paymentData.customerId,
        customerName: paymentData.customerName,
        customerEmail: paymentData.customerEmail,
        customerPhone: paymentData.customerPhone,
        notes: paymentData.notes,
        tseRequired: paymentData.tseRequired,
        taxNumber: paymentData.taxNumber,
        tableNumber: paymentData.tableNumber // Masa numarası eklendi
      });

      // ✅ YENİ: apiClient hata yönetimi otomatik, sadace success kontrolü
      if (!initiateResult || !initiateResult.success) {
        const errorMessage = initiateResult?.message || 'Payment could not be initiated';
        addToast('error', errorMessage);
        setError(errorMessage);
        return { success: false, message: errorMessage };
      }

      addToast('success', 'Payment session initiated successfully', 2000);
      console.log('✅ Payment initiated, session ID:', initiateResult.paymentSessionId);

      // Step 2: Confirm payment (/api/Payment/confirm)
      addToast('info', 'Confirming payment...', 2000);
      
      // ✅ YENİ: apiClient kullanımı - token management otomatik
      const confirmResult = await apiClient.post<any>('/Payment/confirm', {
        paymentSessionId: initiateResult.paymentSessionId,
        transactionReference: `TXN-${Date.now()}`,
        transactionId: `TID-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
        tseSignature: paymentData.tseRequired ? `TSE-${Date.now()}` : null,
        tableNumber: paymentData.tableNumber // Masa numarası eklendi
      });

      // ✅ YENİ: apiClient kullanımı - hata yönetimi basitleşti
      if (confirmResult.success) {
        const successMessage = `Payment completed successfully! Invoice: ${confirmResult.invoiceNumber}`;
        addToast('success', successMessage, 8000);
        
        console.log('✅ Payment completed successfully:', {
          invoiceId: confirmResult.invoiceId,
          receiptNumber: confirmResult.receiptNumber,
          tseSignature: confirmResult.tseSignature,
          tableNumber: paymentData.tableNumber
        });

        // Clear cart for specific table and error state
        await clearCart(paymentData.tableNumber);
        setError(null);
        
        // API ile sepeti sıfırla ve yeni sipariş durumunu güncelle
        await resetCartAndUpdateOrderStatus(confirmResult.invoiceId, confirmResult.receiptNumber, paymentData.tableNumber);
        
        // Show final success notification
        setTimeout(() => {
          addToast('success', `Receipt: ${confirmResult.receiptNumber}`, 10000);
        }, 1000);

        return {
          success: true,
          message: successMessage,
          invoiceId: confirmResult.invoiceId,
          receiptNumber: confirmResult.receiptNumber,
          tseSignature: confirmResult.tseSignature
        };
      } else {
        const errorMessage = confirmResult.message || 'Payment operation failed';
        addToast('error', errorMessage);
        console.error('❌ Payment operation failed:', confirmResult.error);
        setError(errorMessage);
        return { success: false, message: errorMessage };
      }
    } catch (error) {
      console.error('❌ Payment processing error:', error);
      
      let errorMessage = 'Network error occurred during payment';
      if (error instanceof Error) {
        errorMessage = error.message;
      }
      
      addToast('error', errorMessage);
      setError(errorMessage);
      return { success: false, message: errorMessage };
    } finally {
      setPaymentProcessing(false);
      setPreventDoubleClick(false); // Reset double click protection
      clearTimeout(timeoutId); // Clear timeout on success or failure
    }
  }, [user?.token, clearCart, addToast, preventDoubleClick]);

  // Load cart for specific table from backend
  const loadCartForTable = useCallback(async (tableNumber: number) => {
    if (!tableNumber) {
      console.error('❌ Table number is required for loading cart');
      addToast('error', 'Table number is required');
      return;
    }

    try {
      addToast('info', `Switching to table ${tableNumber}...`, 2000);
      console.log('🔄 Switching to table', tableNumber);
      
      // Backend'den sepet yükle
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
  }, [addToast, loadCartFromHook]);

  // Update cart item quantity for specific table
  const updateItemQuantity = useCallback((itemId: string, quantity: number, tableNumber: number) => {
    if (!tableNumber) {
      console.error('❌ Table number is required for updating item quantity');
      addToast('error', 'Table number is required');
      return;
    }

    if (quantity <= 0) {
      removeFromCart(tableNumber, itemId); // ✅ YENİ: Doğru parameter sırası
      addToast('info', 'Item removed from cart', 2000);
    } else {
      // ✅ YENİ: useCart'dan updateItemQuantity kullan (parameter sırası: tableNumber, itemId, quantity)
      updateCartItemQuantity(tableNumber, itemId, quantity);
      addToast('success', 'Cart updated successfully', 2000);
    }
  }, [removeFromCart, updateCartItemQuantity, addToast]);

  // Remove item from cart for specific table
  const removeItem = useCallback((itemId: string, tableNumber: number) => {
    if (!tableNumber) {
      console.error('❌ Table number is required for removing item');
      addToast('error', 'Table number is required');
      return;
    }

    removeFromCart(tableNumber, itemId); // ✅ YENİ: Doğru parameter sırası
    addToast('info', 'Item removed from cart', 2000);
  }, [removeFromCart, addToast]);

  // Clear current cart for specific table
  const clearCurrentCart = useCallback(async (tableNumber: number): Promise<{ success: boolean; message: string }> => {
    console.log('🧹 clearCurrentCart called with tableNumber:', tableNumber);
    console.log('🔍 clearCart function type:', typeof clearCart);
    console.log('🔍 clearCart function:', clearCart);
    
    if (!tableNumber) {
      console.error('❌ Table number is required for clearing cart');
      addToast('error', 'Table number is required');
      return { success: false, message: 'Table number is required' };
    }

    try {
      console.log('🚀 About to call clearCart for table:', tableNumber);
      const result = await clearCart(tableNumber);
      console.log('✅ clearCart API call completed for table:', tableNumber);
      setActiveTableNumber(null);
      
      if (result && result.success) {
        return { success: true, message: 'Cart cleared successfully' };
      } else {
        return { success: false, message: result?.message || 'Failed to clear cart' };
      }
    } catch (error) {
      console.error('❌ Error clearing cart:', error);
      const errorMessage = error instanceof Error ? error.message : 'Failed to clear cart';
      addToast('error', errorMessage, 3000);
      return { success: false, message: errorMessage };
    }
  }, [clearCart, addToast]);

  // Reset cart function for AuthContext
  const resetCart = useCallback((tableNumber?: number) => {
    if (tableNumber) {
      clearCart(tableNumber);
    }
    setActiveTableNumber(null);
    addToast('info', 'Cart reset', 2000);
  }, [clearCart, addToast]);

  // API ile sepeti sıfırla ve yeni sipariş durumunu güncelle
  const resetCartAndUpdateOrderStatus = useCallback(async (invoiceId: string, receiptNumber: string, tableNumber: number) => {
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
      
      // 1. Mevcut sepeti API ile sıfırla ve yeni sipariş durumunu güncelle
      const currentCart = getCartForTable(tableNumber);
      if (currentCart?.cartId) {
        const resetResult = await cartService.resetCartAfterPayment(currentCart.cartId, `Payment completed - Receipt: ${receiptNumber}`);
        console.log('✅ Cart reset and order status updated via API:', resetResult);
        
        // Yeni sepet ID'sini güncelle
        if (resetResult.newCartId) {
          console.log('🆕 New cart ID assigned:', resetResult.newCartId);
        }
      }

      // 2. Frontend state'i temizle
      await clearCart(tableNumber);
      setActiveTableNumber(null);
      
      // 3. Başarı mesajı göster
      addToast('success', `Cart reset and new order ready. Receipt: ${receiptNumber}`, 5000);
      
      console.log('✅ Cart reset and order status update completed:', {
        invoiceId,
        receiptNumber,
        tableNumber,
        timestamp: new Date().toISOString()
      });

    } catch (error) {
      console.error('❌ Error during cart reset and order status update:', error);
      
      let errorMessage = 'Failed to reset cart via API';
      if (error instanceof Error) {
        errorMessage = error.message;
      }
      
      addToast('error', errorMessage);
      
      // Hata durumunda bile frontend state'i temizle
      await clearCart(tableNumber);
      setActiveTableNumber(null);
    }
  }, [user?.token, getCartForTable, clearCart, addToast]);

  // Get payment method info
  const getPaymentMethodInfo = useCallback((method: string) => {
    return getPaymentMethod(method);
  }, [getPaymentMethod]);

  // Check if payment method requires TSE
  const isTseRequired = useCallback((method: string) => {
    const methodInfo = getPaymentMethod(method);
    return methodInfo?.requiresTse || false;
  }, [getPaymentMethod]);

  return {
    // State
    activeTableNumber,
    paymentProcessing,
    preventDoubleClick,
    error,
    toasts,
    
    // Actions
    processPayment,
    loadCartForTable,
    updateItemQuantity,
    removeItem,
    clearCurrentCart,
    resetCart,
    resetCartAndUpdateOrderStatus,
    getPaymentMethodInfo,
    isTseRequired,
    
    // Toast management
    addToast,
    removeToast,
    clearToasts,
    
    // Cart data
    paymentMethods
  };
}; 