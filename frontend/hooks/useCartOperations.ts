import { useState, useCallback, useRef } from 'react';
import { Alert, Vibration } from 'react-native';

import { CartItem } from '../types/cart';

interface UseCartOperationsProps {
  onUpdateQuantity: (productId: string, quantity: number) => Promise<void>;
  onRemoveItem: (productId: string) => Promise<void>;
  onClearCart: () => Promise<void>;
  onError?: (error: string) => void;
  onSuccess?: (message: string) => void;
}

interface CartOperationState {
  processingItem: string | null;
  isProcessing: boolean;
  lastOperation: string | null;
}

export const useCartOperations = ({
  onUpdateQuantity,
  onRemoveItem,
  onClearCart,
  onError,
  onSuccess,
}: UseCartOperationsProps) => {
  const [state, setState] = useState<CartOperationState>({
    processingItem: null,
    isProcessing: false,
    lastOperation: null,
  });

  const operationTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const retryCountRef = useRef<{ [key: string]: number }>({});

  // Çift tıklamayı önle
  const preventDoubleClick = useCallback((operation: string) => {
    if (state.isProcessing) {
      return false;
    }
    setState(prev => ({ ...prev, isProcessing: true, lastOperation: operation }));
    return true;
  }, [state.isProcessing]);

  // İşlem tamamlandığında state'i temizle
  const clearProcessingState = useCallback(() => {
    setState(prev => ({ ...prev, isProcessing: false, processingItem: null }));
  }, []);

  // Hata yönetimi
  const handleError = useCallback((error: Error, operation: string) => {
    console.error(`Cart operation error (${operation}):`, error);
    const errorMessage = error.message || 'Bir hata oluştu';
    onError?.(errorMessage);
    // Retry mekanizması
    const retryCount = retryCountRef.current[operation] || 0;
    if (retryCount < 3) {
      retryCountRef.current[operation] = retryCount + 1;
      setTimeout(() => {
        onError?.(`${errorMessage} - Tekrar deneniyor... (${retryCount + 1}/3)`);
      }, 1000);
    }
    clearProcessingState();
  }, [onError, clearProcessingState]);

  // Başarı yönetimi
  const handleSuccess = useCallback((message: string) => {
    onSuccess?.(message);
    retryCountRef.current = {}; // Retry sayacını sıfırla
    clearProcessingState();
  }, [onSuccess, clearProcessingState]);

  // Miktar güncelleme
  const updateQuantity = useCallback(async (
    productId: string,
    newQuantity: number,
    currentItems: CartItem[]
  ) => {
    if (!preventDoubleClick('updateQuantity')) return;
    try {
      setState(prev => ({ ...prev, processingItem: productId }));
      // Stok kontrolü
      const product = currentItems.find(item => item.product.id === productId)?.product;
      if (product && newQuantity > product.stockQuantity) {
        handleError(new Error(`Stokta sadece ${product.stockQuantity} adet var`), 'stockCheck');
        Vibration.vibrate(100);
        return;
      }
      // Negatif miktar kontrolü
      if (newQuantity < 0) {
        handleError(new Error('Miktar negatif olamaz'), 'validation');
        return;
      }
      // Çok büyük miktar kontrolü
      if (newQuantity > 999) {
        handleError(new Error('Maksimum 999 adet eklenebilir'), 'validation');
        return;
      }
      // Miktar 0 ise ürünü kaldır
      if (newQuantity === 0) {
        await removeItem(productId, currentItems);
        return;
      }
      await onUpdateQuantity(productId, newQuantity);
      const message = newQuantity > 1 ? 'Miktar güncellendi' : 'Ürün eklendi';
      handleSuccess(message);
      Vibration.vibrate(30);
    } catch (error) {
      handleError(error as Error, 'updateQuantity');
    }
  }, [preventDoubleClick, onUpdateQuantity, handleError, handleSuccess]);

  // Ürün kaldırma
  const removeItem = useCallback(async (productId: string, currentItems: CartItem[]) => {
    if (!preventDoubleClick('removeItem')) return;
    const product = currentItems.find(item => item.product.id === productId)?.product;
    Alert.alert(
      'Ürünü Kaldır',
      `${product?.name || 'Bu ürünü'} sepetten kaldırmak istediğinizden emin misiniz?`,
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Kaldır',
          style: 'destructive',
          onPress: async () => {
            try {
              setState(prev => ({ ...prev, processingItem: productId }));
              await onRemoveItem(productId);
              handleSuccess('Ürün sepetten kaldırıldı');
              Vibration.vibrate(50);
            } catch (error) {
              handleError(error as Error, 'removeItem');
            }
          },
        },
      ]
    );
  }, [preventDoubleClick, onRemoveItem, handleError, handleSuccess]);

  // Sepeti temizleme
  const clearCart = useCallback(async (itemCount: number) => {
    if (!preventDoubleClick('clearCart')) return;
    if (itemCount === 0) {
      handleError(new Error('Sepet zaten boş'), 'validation');
      return;
    }
    Alert.alert(
      'Sepeti Temizle',
      `${itemCount} ürün sepetten kaldırılacak. Bu işlem geri alınamaz.`,
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Temizle',
          style: 'destructive',
          onPress: async () => {
            try {
              setState(prev => ({ ...prev, isProcessing: true }));
              await onClearCart();
              handleSuccess('Sepet temizlendi');
              Vibration.vibrate(10);
            } catch (error) {
              handleError(error as Error, 'clearCart');
            }
          },
        },
      ]
    );
  }, [preventDoubleClick, onClearCart, handleError, handleSuccess]);

  // Hızlı miktar ayarlama
  const quickQuantitySet = useCallback(async (
    productId: string,
    quantity: number,
    currentItems: CartItem[]
  ) => {
    if (quantity <= 0) {
      await removeItem(productId, currentItems);
    } else {
      await updateQuantity(productId, quantity, currentItems);
    }
  }, [removeItem, updateQuantity]);

  // Toplu işlemler
  const batchUpdate = useCallback(async (
    updates: { productId: string; quantity: number }[],
    currentItems: CartItem[]
  ) => {
    if (!preventDoubleClick('batchUpdate')) return;
    try {
      setState(prev => ({ ...prev, isProcessing: true }));
      for (const update of updates) {
        await onUpdateQuantity(update.productId, update.quantity);
      }
      handleSuccess(`${updates.length} ürün güncellendi`);
    } catch (error) {
      handleError(error as Error, 'batchUpdate');
    }
  }, [preventDoubleClick, onUpdateQuantity, handleError, handleSuccess]);

  // İşlem geçmişi (örnek fonksiyon, gerçek uygulamada genişletilebilir)
  const getOperationHistory = useCallback(() => {
    return {
      lastOperation: state.lastOperation,
    };
  }, [state.lastOperation]);

  return {
    state,
    updateQuantity,
    removeItem,
    clearCart,
    quickQuantitySet,
    batchUpdate,
    getOperationHistory,
  };
}; 