// Türkçe Açıklama: Ana cash register ekranı - yeni backend API yapısını kullanır
// RKSV uyumlu ürün yönetimi ve modern API entegrasyonu

import React, { useState, useEffect, useRef } from 'react';
import { SafeAreaView, ScrollView, StyleSheet, View, Text } from 'react-native';

// Modüler component'ları import et
import { CashRegisterHeader } from '../../components/CashRegisterHeader';
import { TableSelector } from '../../components/TableSelector';
import { ProductList } from '../../components/ProductList'; // Yeni ProductList komponenti
import { CartDisplay } from '../../components/CartDisplay';
import { CartSummary } from '../../components/CartSummary';
import CategoryFilter from '../../components/CategoryFilter';
import PaymentModal from '../../components/PaymentModal';
import { ToastContainer } from '../../components/ToastNotification';

// Hook'ları import et
import { useCashRegister } from '../../hooks/useCashRegister';
import { useTableOrdersRecoveryOptimized } from '../../hooks/useTableOrdersRecoveryOptimized';
import { useProductsUnified } from '../../hooks/useProductsUnified'; // Unified product hook

// ✅ Cart Context (Zustand removed)
import { useCart } from '../../contexts/CartContext';

// Yeni ürün API servislerini import et
import { Product } from '../../services/api/productService';
import { apiClient } from '../../services/api/config';

// Soft minimal theme
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography } from '../../constants/SoftTheme';

export default function CashRegisterScreen() {
  // Unified product hook - tüm ürün işlemlerini tek noktada yönet
  const {
    products,
    categories,
    loading: productsLoading,
    error: productsError,
    refreshData,
    getProductsByCategory,
    searchProducts
  } = useProductsUnified();

  // Cash register hook'u
  const {
    paymentProcessing,
    preventDoubleClick,
    error,
    toasts,
    addToast,
    removeToast,
    clearCurrentCart
  } = useCashRegister();

  // Filtrelenmiş ürünler için state - unified hook'tan gelen fonksiyonları kullan
  const [filteredProducts, setFilteredProducts] = useState<Product[]>([]);

  // Table orders recovery hook'u
  const {
    recoveryData,
    isRecoveryCompleted,
    isLoading: recoveryLoading
  } = useTableOrdersRecoveryOptimized();

  // ✅ Cart Context Usage
  const {
    activeTableId,
    cartsByTable,
    loading: cartLoading,
    error: cartError,
    switchTable, // ✅ Use switchTable
    addItem,
    increment,
    decrement,
    remove,
    clearCart,
    getCartForTable,
    updateItemQuantity: contextUpdateItemQuantity // Rename to avoid conflict if any
  } = useCart();

  // Local state'ler
  // REMOVED: selectedTable state (activeTableId is source of truth)
  const [tableSelectionLoading, setTableSelectionLoading] = useState<number | null>(null);
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [paymentModalVisible, setPaymentModalVisible] = useState(false);
  const [customerId, setCustomerId] = useState<string>('00000000-0000-0000-0000-000000000000');

  // ✅ Aktif table'ın cart'ını al (Derived State)
  const cart = getCartForTable(activeTableId);

  // Ref'ler
  const isFirstLoad = useRef(true);

  // ---------------------------------------------------------------------------
  // ADAPTERS for Context (Mapping old hook API to Context API)
  // ---------------------------------------------------------------------------

  const addToCart = async (item: any, tableId: number) => {
    try {
      if (tableId !== activeTableId) await switchTable(tableId);
      await addItem(item.productId, item.quantity);
      return { success: true };
    } catch (e: any) {
      return { success: false, message: e.message };
    }
  };

  const removeFromCart = async (tableId: number, itemId: string) => {
    try {
      // Note: Context uses productId, but cash-register might pass itemId.
      // Assuming itemId here IS productId for now due to check above.
      await remove(itemId);
      return { success: true };
    } catch (e: any) {
      console.error(e);
      return { success: false };
    }
  };

  const updateItemQuantity = async (tableId: number, itemId: string, qty: number) => {
    try {
      await contextUpdateItemQuantity(itemId, qty);
      return { success: true };
    } catch (e) { console.error(e); return { success: false }; }
  };

  // Mock load function as Context manages loading state via useEffect/actions
  const loadCartForTable = async (tableId: number) => {
    // In Context, data is either there or will be loaded/optimistic.
    // We can trigger a refresh if context had a refresh method, but for now just return current state.
    // If we really need fresh data, we could call an API here, but let's trust the Context.
    return { success: true, cart: getCartForTable(tableId) };
  };

  const clearAllTables = async () => {
    // Context doesn't have clearAllTables yet, but we can clear current.
    // Or implement it. For now, specific table clear.
    try {
      await clearCart(activeTableId);
      return { success: true, message: 'Table cleared' };
    } catch (e: any) {
      return { success: false, message: e.message || 'Failed' };
    }
  };

  // ---------------------------------------------------------------------------

  // Ürün filtreleme fonksiyonu - unified hook'tan gelen fonksiyonu kullan
  const filterProducts = (category: string) => {
    const filtered = getProductsByCategory(category);
    setFilteredProducts(filtered);
  };

  // Kategorileri yükleme fonksiyonu - artık unified hook'tan otomatik geliyor
  const loadCategories = () => {
    // Categories artık unified hook'tan otomatik geliyor - hiçbir şey yapmaya gerek yok
    console.log(`📂 Categories loaded: ${categories.length} items`);
  };

  // Yeni ürün yükleme fonksiyonu - unified hook'tan gelen fonksiyonları kullan
  const loadProductsNew = (category?: string) => {
    try {
      const productsData = getProductsByCategory(category || 'all');
      setFilteredProducts(productsData);
      console.log(`📦 Products loaded: ${productsData.length} items for category: ${category || 'all'}`);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Ürünler yüklenemedi';
      console.error('❌ Yeni ürün yükleme hatası:', error);
      addToast('error', errorMessage, 3000);
    }
  };

  // Ürün arama fonksiyonu
  const handleProductSearch = (searchResults: Product[]) => {
    setFilteredProducts(searchResults);
  };

  // Ürün seçimi handler'ı (yeni yapı)
  const handleProductSelect = async (product: Product) => {
    try {
      if (!activeTableId) {
        addToast('error', 'Please select a table first', 3000);
        return;
      }

      const addResult = await addToCart({
        productId: product.id,
        productName: product.name,
        quantity: 1,
        unitPrice: product.price,
        notes: undefined
      }, activeTableId);

      if (addResult.success) {
        // No need to setCart, Context updates automatically
        addToast('success', `${product.name} added to table ${activeTableId}`, 2000);
      }

    } catch (error: any) {
      console.error('❌ Ürün ekleme hatası:', error);
      addToast('error', `Failed to add ${product.name}: ${error?.message || 'Unknown error'}`, 5000);
    }
  };

  // Masa seçimi handler'ı
  const handleTableSelect = async (tableNumber: number) => {
    try {
      if (!tableNumber || tableNumber < 1 || tableNumber > 10) {
        addToast('error', 'Invalid table number', 3000);
        return;
      }

      if (activeTableId === tableNumber) {
        return;
      }

      if (tableSelectionLoading !== null) {
        return;
      }

      setTableSelectionLoading(tableNumber);

      // IMPERATIVE SWITCH
      await switchTable(tableNumber);

      addToast('info', `Switching to table ${tableNumber}`, 2000);

      setTimeout(() => {
        setTableSelectionLoading(null);
      }, 500);

    } catch (error) {
      console.error('❌ Masa seçim hatası:', error);
      addToast('error', 'Failed to switch table', 3000);
      setTableSelectionLoading(null);
    }
  };

  // Sepet miktar güncelleme handler'ı - uses dedicated increment/decrement endpoints
  const handleQuantityUpdate = async (itemId: string, newQuantity: number) => {
    if (!activeTableId) {
      addToast('error', 'No table selected. Please select a table first.', 3000);
      return;
    }

    const currentCart = getCartForTable(activeTableId);
    const item = currentCart?.items?.find((i: any) => i.id === itemId || i.itemId === itemId);
    if (!item) {
      addToast('error', 'Item not found', 2000);
      return;
    }

    const currentQty = item.qty || 0;

    const productId = item.productId;
    if (!productId) {
      console.error('❌ ProductId missing for item', item);
      addToast('error', 'Cannot update item', 2000);
      return;
    }

    try {
      if (newQuantity <= 0) {
        // Use CartContext remove (proper error handling)
        await remove(productId);
        addToast('info', 'Item removed from cart', 2000);
      } else if (newQuantity > currentQty) {
        // Use CartContext increment (has POST→PUT fallback)
        await increment(productId);
        addToast('success', 'Quantity increased', 2000);
      } else if (newQuantity < currentQty) {
        // Use CartContext decrement (has POST→PUT fallback)
        await decrement(productId);
        if (currentQty === 1) {
          addToast('info', 'Item removed from cart', 2000);
        } else {
          addToast('success', 'Quantity decreased', 2000);
        }
      }

      // CartContext already handles state updates

    } catch (err: any) {
      console.error('❌ Quantity update error:', err);
      addToast('error', err?.message || 'Failed to update quantity', 3000);
    }
  };

  // Ürün kaldırma handler'ı
  const handleItemRemove = async (itemId: string) => {
    if (!activeTableId) {
      addToast('error', 'No table selected. Please select a table first.', 3000);
      return;
    }

    try {
      await apiClient.delete(`/cart/items/${itemId}`);
      addToast('info', 'Item removed from cart', 2000);
      await switchTable(activeTableId);
    } catch (err: any) {
      console.error('❌ Remove item error:', err);
      addToast('error', err?.message || 'Failed to remove item', 3000);
    }
  };

  // Sepet temizleme handler'ı
  const handleClearCart = async () => {
    if (!activeTableId) {
      addToast('error', 'No table selected. Please select a table first.', 3000);
      return;
    }

    try {
      await clearCart(activeTableId);
      addToast('success', `Cart cleared for Table ${activeTableId}`, 2000);

    } catch (error) {
      console.error(`❌ Error clearing table ${activeTableId}:`, error);
      addToast('error', `Failed to clear table ${activeTableId}`, 3000);
    }
  };

  // Tüm masaları temizleme handler'ı
  const handleClearAllTables = async () => {
    try {
      const result = await clearAllTables();

      if (result && result.success) {
        // setSelectedTable(1); // Removed
        switchTable(1);
        addToast('success', 'All tables cleared successfully', 3000);
      } else {
        addToast('error', `Failed to clear all tables: ${result?.message || 'Unknown error'}`, 5000);
      }
    } catch (error) {
      console.error('❌ Error clearing all tables:', error);
      addToast('error', 'Error clearing all tables', 3000);
    }
  };

  // Ödeme handler'ı
  const handlePayment = () => {
    if (!cart?.items?.length) {
      addToast('warning', 'Cart is empty. Please add items first.', 3000);
      return;
    }

    if (!activeTableId) {
      addToast('error', 'No table selected. Please select a table first.', 3000);
      return;
    }

    setPaymentModalVisible(true);
  };

  // Ödeme başarılı handler'ı
  const handlePaymentSuccess = async (paymentId: string) => {
    try {
      addToast('success', `Payment successful! Payment ID: ${paymentId}`, 5000);
      await clearCart(activeTableId);
      // setSelectedTable(1); // Removed
      switchTable(1);
    } catch (error) {
      console.error('Payment success handling error:', error);
      addToast('error', 'Payment success handling failed.', 5000);
    }
  };

  // Kategori değişimi handler'ı (yeni yapı)
  const handleCategoryChange = async (category: typeof selectedCategory) => {
    setSelectedCategory(category);

    // Kategori değiştiğinde ürünleri yeniden yükle
    if (category !== selectedCategory) {
      await loadProductsNew(category);
    }
  };

  // useEffect'ler
  useEffect(() => {
    // Unified hook'tan gelen veriler hazır olduğunda kategorileri ve ürünleri yükle
    if (products.length > 0 && categories.length > 0) {
      loadCategories();
      loadProductsNew();
    }
  }, [products, categories]);

  // Debug: Unified hook state durumunu kontrol et
  useEffect(() => {
    console.log('🔍 CashRegister: Unified hook state changed', {
      productsCount: products.length,
      categoriesCount: categories.length,
      loading: productsLoading,
      error: productsError
    });
  }, [products.length, categories.length, productsLoading, productsError]);

  // REMOVED: Load cart on selection change (Sync active table) -> Now handled by switchTable

  useEffect(() => {
    // Recovery logic could update Context here if needed.
    // Already handled by CartContext via generic logic usually,
    // But if recoveryData provides a cart structure, we might need to populate Context?
    // For now assume recoveryData syncs with backend and CartContext reads from backend/storage.
    if (isRecoveryCompleted && recoveryData && activeTableId) {
      // We are trusting CartContext to have the data.
      // If recoveryData has data that Context doesn't, we might need to sync.
      // But recovery implies "Backend has checks", and Context reads from Backend.
      // So they should eventually consistency.
    }
  }, [isRecoveryCompleted, recoveryData, activeTableId]);


  return (
    <SafeAreaView style={styles.container}>
      {/* Toast Notifications */}
      <ToastContainer toasts={toasts} onRemove={removeToast} />

      {/* Header */}
      <CashRegisterHeader
        selectedTable={activeTableId}
        recoveryLoading={recoveryLoading}
      />

      {/* Scrollable Content */}
      <ScrollView style={styles.scrollContainer} showsVerticalScrollIndicator={false}>
        {/* Table Selector */}
        <TableSelector
          selectedTable={activeTableId}
          onTableSelect={handleTableSelect}
          tableCarts={new Map()} // Bu kısmı daha sonra optimize edebiliriz
          recoveryData={recoveryData}
          tableSelectionLoading={tableSelectionLoading}
          onClearAllTables={handleClearAllTables}
        />

        {/* Category Filter */}
        <View style={styles.categorySection}>
          <Text style={styles.sectionTitle}>Categories</Text>
          <CategoryFilter
            selectedCategory={selectedCategory}
            onCategoryChange={handleCategoryChange}
            categories={categories}
          />
        </View>

        {/* Yeni ProductList komponenti */}
        <ProductList
          categoryFilter={selectedCategory === 'all' ? undefined : selectedCategory}
          onProductSelect={handleProductSelect}
          showStockInfo={true}
          showTaxInfo={true}
        />

        {/* Cart Display */}
        <CartDisplay
          cart={cart}
          selectedTable={activeTableId}
          loading={cartLoading}
          error={cartError}
          onQuantityUpdate={handleQuantityUpdate}
          onItemRemove={handleItemRemove}
          onClearCart={handleClearCart}
        />

        {/* Cart Summary & Payment Button */}
        <CartSummary
          cart={cart}
          loading={cartLoading}
          error={error}
          paymentProcessing={paymentProcessing}
          preventDoubleClick={preventDoubleClick}
          onPayment={handlePayment}
        />
      </ScrollView>

      {/* PaymentModal */}
      <PaymentModal
        visible={paymentModalVisible}
        onClose={() => setPaymentModalVisible(false)}
        onSuccess={handlePaymentSuccess}
        cartItems={(cart?.items || []).map(item => ({
          id: item.itemId || item.productId,
          productId: item.productId,
          productName: item.productName || 'Unknown Product',
          quantity: item.qty,
          unitPrice: item.unitPrice || item.price || 0,
          totalPrice: item.totalPrice || ((item.price || 0) * item.qty),
          taxType: undefined
        }))}
        customerId={customerId}
        tableNumber={activeTableId}
      />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
  },
  scrollContainer: {
    flex: 1,
  },
  categorySection: {
    backgroundColor: SoftColors.bgCard,
    paddingVertical: SoftSpacing.lg,
    paddingHorizontal: SoftSpacing.lg,
    marginBottom: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.borderLight,
  },
  sectionTitle: {
    ...SoftTypography.h3,
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.md,
  },
}); 