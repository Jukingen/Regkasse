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
import { useCartOptimized } from '../../hooks/useCartOptimized';
import { useCashRegister } from '../../hooks/useCashRegister';
import { useTableOrdersRecoveryOptimized } from '../../hooks/useTableOrdersRecoveryOptimized';
import { useProductsUnified } from '../../hooks/useProductsUnified'; // Unified product hook

// Yeni ürün API servislerini import et
import { Product } from '../../services/api/productService';

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

  // Cart hook'u
  const { 
    getCartForTable,
    addToCart, 
    updateItemQuantity, 
    removeFromCart,
    loadCartForTable,
    clearAllTables,
    loading: cartLoading, 
    error: cartError 
  } = useCartOptimized();

  // Local state'ler
  const [selectedTable, setSelectedTable] = useState<number>(1);
  const [tableSelectionLoading, setTableSelectionLoading] = useState<number | null>(null);
  const [cart, setCart] = useState<any>({ items: [], cartId: null, grandTotal: 0 });
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [paymentModalVisible, setPaymentModalVisible] = useState(false);
  const [customerId, setCustomerId] = useState<string>('00000000-0000-0000-0000-000000000000');

  // Ref'ler
  const isFirstLoad = useRef(true);

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
      if (!selectedTable) {
        addToast('error', 'Please select a table first', 3000);
        return;
      }

      const addResult = await addToCart({
        productId: product.id,
        productName: product.name,
        quantity: 1,
        unitPrice: product.price,
        notes: undefined
      }, selectedTable);
      
      if (addResult.success) {
        const updatedCart = getCartForTable(selectedTable);
        setCart(updatedCart);
        addToast('success', `${product.name} added to table ${selectedTable}`, 2000);
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

      if (selectedTable === tableNumber) {
        return;
      }

      if (tableSelectionLoading !== null) {
        return;
      }

      setTableSelectionLoading(tableNumber);
      setSelectedTable(tableNumber);
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

  // Sepet miktar güncelleme handler'ı
  const handleQuantityUpdate = async (itemId: string, newQuantity: number) => {
    if (!selectedTable) {
      addToast('error', 'No table selected. Please select a table first.', 3000);
      return;
    }

    if (newQuantity <= 0) {
      await removeFromCart(selectedTable, itemId);
    } else {
      await updateItemQuantity(selectedTable, itemId, newQuantity);
    }
    
    const updatedCart = getCartForTable(selectedTable);
    setCart(updatedCart);
  };

  // Ürün kaldırma handler'ı
  const handleItemRemove = async (itemId: string) => {
    if (!selectedTable) {
      addToast('error', 'No table selected. Please select a table first.', 3000);
      return;
    }

    await removeFromCart(selectedTable, itemId);
    const updatedCart = getCartForTable(selectedTable);
    setCart(updatedCart);
    addToast('info', 'Item removed from cart', 2000);
  };

  // Sepet temizleme handler'ı
  const handleClearCart = async () => {
    if (!selectedTable) {
      addToast('error', 'No table selected. Please select a table first.', 3000);
      return;
    }

    try {
      await clearCurrentCart(selectedTable);
      setCart(null);
      
      const freshCartResult = await loadCartForTable(selectedTable);
      if (freshCartResult?.success && freshCartResult.cart) {
        setCart(freshCartResult.cart);
      } else {
        setCart(null);
      }
      
    } catch (error) {
      console.error(`❌ Error clearing table ${selectedTable}:`, error);
      addToast('error', `Failed to clear table ${selectedTable}`, 3000);
    }
  };

  // Tüm masaları temizleme handler'ı
  const handleClearAllTables = async () => {
    try {
      const result = await clearAllTables();
      
      if (result && result.success) {
        setCart(null);
        setSelectedTable(1);
        
        const freshCartResult = await loadCartForTable(1);
        if (freshCartResult?.success && freshCartResult.cart) {
          setCart(freshCartResult.cart);
        } else {
          setCart(null);
        }
        
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

    if (!selectedTable) {
      addToast('error', 'No table selected. Please select a table first.', 3000);
      return;
    }

    setPaymentModalVisible(true);
  };

  // Ödeme başarılı handler'ı
  const handlePaymentSuccess = async (paymentId: string) => {
    try {
      addToast('success', `Payment successful! Payment ID: ${paymentId}`, 5000);
      await clearCurrentCart(selectedTable);
      setSelectedTable(1);
      setCart(null);
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

  useEffect(() => {
    if (selectedTable && isFirstLoad.current) {
      loadCartForTable(selectedTable).then((result: any) => {
        if (result?.success) {
          setCart(result.cart);
        } else {
          setCart(null);
        }
        isFirstLoad.current = false;
      }).catch((error: any) => {
        console.error('❌ İlk yüklemede sepet yükleme hatası:', error);
        setCart(null);
        isFirstLoad.current = false;
      });
    }
  }, [selectedTable]);

  useEffect(() => {
    if (!isFirstLoad.current && selectedTable) {
      loadCartForTable(selectedTable).then((result: any) => {
        if (result?.success) {
          setCart(result.cart);
        } else {
          setCart(null);
        }
      }).catch((error: any) => {
        console.error('❌ Masa değişti, yeni masa sepet hatası:', error);
        setCart(null);
      });
    }
  }, [selectedTable]);

  useEffect(() => {
    if (isRecoveryCompleted && recoveryData) {
      if (selectedTable && recoveryData.tableOrders) {
        const recoveryOrder = recoveryData.tableOrders.find(
          (order: any) => order.tableNumber === selectedTable
        );
        
        if (recoveryOrder && recoveryOrder.itemCount > 0) {
          const recoveryCart = {
            cartId: recoveryOrder.cartId,
            items: recoveryOrder.items.map((item: any) => ({
              id: item.productId,
              productId: item.productId,
              productName: item.productName,
              quantity: item.quantity,
              unitPrice: item.price,
              price: item.price,
              totalPrice: item.total,
              total: item.total,
              notes: item.notes
            })),
            totalItems: recoveryOrder.itemCount,
            grandTotal: recoveryOrder.totalAmount,
            subtotal: recoveryOrder.totalAmount * 0.8,
            totalTax: recoveryOrder.totalAmount * 0.2,
            status: recoveryOrder.status
          };
          
          setCart(recoveryCart);
        }
      }
    }
  }, [isRecoveryCompleted, recoveryData, selectedTable]);

  useEffect(() => {
    if (selectedTable) {
      const currentCart = getCartForTable(selectedTable);
      setCart(currentCart);
    }
  }, [selectedTable, getCartForTable]);

  return (
    <SafeAreaView style={styles.container}>
      {/* Toast Notifications */}
      <ToastContainer toasts={toasts} onRemove={removeToast} />

      {/* Header */}
      <CashRegisterHeader 
        selectedTable={selectedTable}
        recoveryLoading={recoveryLoading}
      />

      {/* Scrollable Content */}
      <ScrollView style={styles.scrollContainer} showsVerticalScrollIndicator={false}>
        {/* Table Selector */}
        <TableSelector
          selectedTable={selectedTable}
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
            categories={['all', ...categories]}
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
          selectedTable={selectedTable}
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
        cartItems={cart?.items || []}
        customerId={customerId}
        tableNumber={selectedTable}
      />
    </SafeAreaView>
  );
}

// Basit styles - ana styling component'larda
const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  scrollContainer: {
    flex: 1,
  },
  categorySection: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 10,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 15,
    color: '#333',
  },
}); 