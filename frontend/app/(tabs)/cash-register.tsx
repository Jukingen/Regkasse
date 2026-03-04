// Türkçe Açıklama: Ana cash register ekranı - yeni backend API yapısını kullanır
// RKSV uyumlu ürün yönetimi ve modern API entegrasyonu

import React, { useState, useEffect, useRef, useMemo } from 'react';
import { SafeAreaView, ScrollView, StyleSheet, View, Text } from 'react-native';

// Modüler component'ları import et
import { CashRegisterHeader } from '../../components/CashRegisterHeader';
import { TableSelector } from '../../components/TableSelector';
import { ProductList } from '../../components/ProductList'; // Yeni ProductList komponenti
import { CartDisplay } from '../../components/CartDisplay';
import { CartSummary } from '../../components/CartSummary';
import CategoryFilter from '../../components/CategoryFilter';
import PaymentModal from '../../components/PaymentModal';
import { ModifierSelectionModal, type SelectedModifier } from '../../components/ModifierSelectionModal';
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
    isLoading: recoveryLoading,
    provisioningMessage: recoveryProvisioningMessage,
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
    removeByItemId,
    clearCart,
    getCartForTable,
    updateItemQuantity: contextUpdateItemQuantity,
    updateItemQuantityByItemId, // Rename to avoid conflict if any
    isPaymentModalVisible,
    setIsPaymentModalVisible
  } = useCart();

  // Local state'ler
  const [tableSelectionLoading, setTableSelectionLoading] = useState<number | null>(null);
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [customerId, setCustomerId] = useState<string>('00000000-0000-0000-0000-000000000000');
  // Extra Zutaten: Ürün seçilince modal açılır, modifier seçilip "Hinzufügen" ile sepete eklenir
  const [modifierModalVisible, setModifierModalVisible] = useState(false);
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);

  // ✅ Aktif table'ın cart'ını al (Derived State)
  const cart = getCartForTable(activeTableId);

  // Masa badge counter için cartsByTable'dan Map türet (anlık güncelleme için tek kaynak)
  // Boş masalar dahil tüm entry'ler eklenir; Clear All sonrası badge 0 olur
  const tableCartsMap = useMemo(() => {
    const map = new Map<number, { items: any[]; totalItems: number }>();
    for (const [tableNumStr, cartData] of Object.entries(cartsByTable)) {
      const tableNum = Number(tableNumStr);
      const items = cartData?.items ?? [];
      const totalItems = items.reduce((s: number, i: any) => s + (i.qty ?? 0), 0);
      map.set(tableNum, { items, totalItems });
    }
    return map;
  }, [cartsByTable]);

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

  // Ürün seçimi: Önce Extra-Zutaten modal'ı aç; "Hinzufügen" ile sepete ekle (modifier seçmeden de eklenebilir)
  const handleProductSelect = (product: Product) => {
    if (!activeTableId) {
      addToast('error', 'Bitte zuerst Tisch wählen', 3000);
      return;
    }
    setSelectedProduct(product);
    setModifierModalVisible(true);
  };

  const handleModifierModalAdd = async (selectedModifiers: SelectedModifier[]) => {
    if (!selectedProduct || !activeTableId) return;
    try {
      await addItem(selectedProduct.id, 1, {
        modifiers: selectedModifiers,
        productName: selectedProduct.name,
        unitPrice: selectedProduct.price ?? 0,
      });
      addToast('success', `${selectedProduct.name} zu Tisch ${activeTableId} hinzugefügt`, 2000);
      setModifierModalVisible(false);
      setSelectedProduct(null);
    } catch (error: any) {
      addToast('error', `${selectedProduct.name}: ${error?.message || 'Fehler'}`, 5000);
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

  // Sepet miktar güncelleme handler'ı - Direct API Implementation with fresh state
  const handleQuantityUpdate = async (itemId: string, action: 'increment' | 'decrement') => {
    if (!activeTableId) return;

    const currentCart = getCartForTable(activeTableId);
    const item = currentCart?.items?.find((i: any) => (i.itemId || i.id || i.productId) === itemId);
    if (!item) return;

    const currentQty = (item as any).quantity ?? item.qty ?? 0;
    const newQty = action === 'increment' ? currentQty + 1 : currentQty - 1;

    try {
      await updateItemQuantityByItemId(itemId, newQty);
    } catch (err: any) {
      addToast('error', 'Update failed', 2000);
    }
  };


  const handleItemRemove = async (itemId: string) => {
    if (!activeTableId) return;
    try {
      await removeByItemId(itemId);
      addToast('info', 'Item removed from cart', 2000);
    } catch (err: any) {
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

  // Tüm masaları temizleme handler'ı (Clear All / Clear Current Table)
  const handleClearAllTables = async () => {
    try {
      if (!activeTableId) {
        addToast('error', 'No table selected', 3000);
        return;
      }

      // Capture active table explicitly to avoid race conditions
      const targetTableId = activeTableId;

      // Call clearCart directly for the active table
      // Note: We use clearCart from context, which handles the API call and local state update
      await clearCart(targetTableId);

      // ❌ REMOVED: switchTable(1); - This was forcing the UI to jump to table 1
      // ✅ Behavior: UI stays on the same table (targetTableId)

      addToast('success', `Table ${targetTableId} cleared successfully`, 3000);

    } catch (error: any) {
      console.error('❌ Error clearing table:', error);
      addToast('error', error.message || 'Error clearing table', 3000);
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

    setIsPaymentModalVisible(true);
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
        provisioningMessage={recoveryProvisioningMessage}
      />

      {/* Root List - ProductList acts as the main scrollable container */}
      {/* Stock info intentionally hidden from cashier UI. Stock management is handled in admin panel. Kept in code for potential future POS usage. */}
      <ProductList
        categoryFilter={selectedCategory === 'all' ? undefined : selectedCategory}
        onProductSelect={handleProductSelect}
        showStockInfo={false}
        showTaxInfo={true}
        ListHeaderComponent={
          <>
            {/* Table Selector */}
            <TableSelector
              selectedTable={activeTableId}
              onTableSelect={handleTableSelect}
              tableCarts={tableCartsMap}
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
          </>
        }
        ListFooterComponent={
          <>
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
          </>
        } 
      />

      {/* Extra Zutaten: Ürün tıklandığında modal, "Hinzufügen" ile sepete ekleme */}
      {selectedProduct && (
        <ModifierSelectionModal
          visible={modifierModalVisible}
          productId={selectedProduct.id}
          productName={selectedProduct.name}
          productPrice={selectedProduct.price ?? 0}
          onClose={() => { setModifierModalVisible(false); setSelectedProduct(null); }}
          onAdd={handleModifierModalAdd}
        />
      )}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
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