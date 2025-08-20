// Türkçe Açıklama: Bu ekran kasiyer için sade, hızlı ve modern bir ana satış arayüzü sunar. Tab ile masa seçimi, basit sepet görünümü, manuel masa durumu yönetimi. Kod linter uyumludur ve kasiyer dostu tasarlanmıştır.
import React, { useState, useEffect, useRef } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  SafeAreaView,
  Vibration,
} from 'react-native';

import CategoryFilter from '../../components/CategoryFilter';
import { ToastContainer } from '../../components/ToastNotification';
import { useCart } from '../../hooks/useCart';
import { useCashRegister } from '../../hooks/useCashRegister';
import { useProductOperations } from '../../hooks/useProductOperations';
import { useTableOrdersRecovery } from '../../hooks/useTableOrdersRecovery';
import PaymentModal from '../../components/PaymentModal';

// English Description: Simplified cash register screen with tab-based table selection and clean cart view

export default function CashRegisterScreen() {
  const {
    paymentProcessing,
    preventDoubleClick,
    error,
    toasts,
    processPayment,
    isTseRequired,
    addToast,
    removeToast,
    clearCurrentCart
  } = useCashRegister();

  const { products, refreshProducts } = useProductOperations();
  const [selectedTable, setSelectedTable] = useState<number>(1);
  const [tableSelectionLoading, setTableSelectionLoading] = useState<number | null>(null); // Masa seçim loading state
  const isFirstLoad = useRef(true); // İlk yükleme kontrolü için

  // Table orders recovery hook'u ekle - F5 sonrası masa siparişlerini geri yüklemek için
  const { 
    recoveryData, 
    isRecoveryCompleted, 
    isLoading: recoveryLoading 
  } = useTableOrdersRecovery();

  const { 
    getCartForTable,
    addToCart, 
    updateItemQuantity, 
    removeFromCart,
    loadCartForTable,
    clearAllTables,
    loading: cartLoading, 
    error: cartError 
  } = useCart();

  // Aktif masanın sepetini al - state olarak tanımla
  const [cart, setCart] = useState<any>(null);
  const [selectedCategory, setSelectedCategory] = useState<'all' | 'Hauptgerichte' | 'Getränke' | 'Desserts' | 'Alkoholische Getränke' | 'Snacks' | 'Suppen' | 'Vorspeisen' | 'Salate' | 'Kaffee & Tee' | 'Süßigkeiten' | 'Spezialitäten' | 'Brot & Gebäck'>('all');
  
  // PaymentModal state'leri
  const [paymentModalVisible, setPaymentModalVisible] = useState(false);
  const [customerId, setCustomerId] = useState<string>('demo-customer-001'); // Demo müşteri ID

  // Table numbers for selection
  const tableNumbers = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

  // Load products and cart when component mounts
  useEffect(() => {
    console.log('🔄 Component mount - Ürünler yükleniyor...');
    console.log('🔍 refreshProducts fonksiyonu çağrılıyor...');
    
    refreshProducts().then(() => {
      console.log('✅ Ürünler yüklendi');
      console.log('📦 Products state:', products);
    }).catch((error) => {
      console.error('❌ Ürün yükleme hatası:', error);
    });
    
    // İlk yüklemede seçili masanın sepetini yükle (sadece bir kere)
    if (selectedTable) {
      loadCartForTable(selectedTable).then((result) => {
        if (result?.success) {
          // Cart boş olsa bile setleyelim (backend'den geliyorsa)
          setCart(result.cart);
          console.log('✅ İlk yüklemede masa', selectedTable, 'sepeti yüklendi:', {
            cartId: result.cart?.cartId,
            itemsCount: result.cart?.items?.length ?? 0,
            hasItems: result.cart?.items && result.cart.items.length > 0
          });
        } else {
          console.log('ℹ️ İlk yüklemede masa', selectedTable, 'için sepet bulunamadı');
          setCart(null);
        }
      }).catch((error) => {
        console.error('❌ İlk yüklemede sepet hatası:', error);
        setCart(null);
      });
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // Sadece component mount'ta çalışsın

  // Load cart when table changes (sadece masa değiştiğinde)
  useEffect(() => {
    // İlk yüklemede çalışmasın, sadece masa değiştiğinde
    if (!isFirstLoad.current && selectedTable) {
      loadCartForTable(selectedTable).then((result) => {
        if (result?.success) {
          // Cart boş olsa bile setleyelim (backend'den geliyorsa)
          setCart(result.cart);
          console.log('✅ Masa değişti, yeni masa', selectedTable, 'sepeti yüklendi:', {
            cartId: result.cart?.cartId,
            itemsCount: result.cart?.items?.length ?? 0,
            hasItems: result.cart?.items && result.cart.items.length > 0
          });
        } else {
          console.log('ℹ️ Masa değişti, yeni masa', selectedTable, 'için sepet bulunamadı');
          setCart(null);
        }
      }).catch((error) => {
        console.error('❌ Masa değişti, yeni masa', selectedTable, 'sepet hatası:', error);
        setCart(null);
      });
    } else {
      isFirstLoad.current = false;
    }
  }, [selectedTable, loadCartForTable]);

  // Recovery data değiştiğinde masaları güncelle
  useEffect(() => {
    if (isRecoveryCompleted && recoveryData) {
      console.log('🔄 Recovery data güncellendi, masalar güncelleniyor...');
      console.log('📊 Recovery data:', recoveryData);
      
      // Recovery data'dan gelen siparişleri masalarda göster
      // Bu useEffect sadece recovery data değiştiğinde çalışır
      
      // Recovery data'dan seçili masa için cart yükle
      if (selectedTable && recoveryData.tableOrders) {
        const recoveryOrder = recoveryData.tableOrders.find(
          order => order.tableNumber === selectedTable
        );
        
        if (recoveryOrder && recoveryOrder.itemCount > 0) {
          console.log(`🔄 Recovery: Masa ${selectedTable} için ${recoveryOrder.itemCount} ürün bulundu`);
          
                     // Recovery data'dan cart state'ini güncelle
           const recoveryCart = {
             cartId: recoveryOrder.cartId,
             items: recoveryOrder.items.map(item => ({
               id: item.productId, // ID alanı ekle
               productId: item.productId,
               productName: item.productName,
               quantity: item.quantity,
               unitPrice: item.price, // unitPrice alanı ekle
               price: item.price,
               totalPrice: item.total, // totalPrice alanı ekle
               total: item.total,
               notes: item.notes
             })),
             totalItems: recoveryOrder.itemCount,
             grandTotal: recoveryOrder.totalAmount,
             subtotal: recoveryOrder.totalAmount * 0.8, // Subtotal hesapla
             totalTax: recoveryOrder.totalAmount * 0.2, // Tax hesapla
             status: recoveryOrder.status
           };
          
          setCart(recoveryCart);
          console.log('✅ Recovery cart state güncellendi:', recoveryCart);
        }
      }
    }
  }, [isRecoveryCompleted, recoveryData, selectedTable]);

  // Cart state'ini sürekli güncellemek için useEffect ekle - Force refresh
  useEffect(() => {
    if (selectedTable) {
      // useCart hook'undan gelen cart'ı al ve local state'i güncelle
      const currentCart = getCartForTable(selectedTable);
      // Her zaman güncelle - comparison kaldırıldı
      setCart(currentCart);
      console.log('🔄 Cart state force güncellendi:', {
        tableNumber: selectedTable,
        hasCart: !!currentCart,
        itemsCount: currentCart?.items?.length || 0,
        cartData: currentCart
      });
    }
  }, [selectedTable, getCartForTable]); // cart dependency kaldırıldı - infinite loop önlemek için



  // Handle table selection
  const handleTableSelect = async (tableNumber: number) => {
    try {
      // Input validation
      if (!tableNumber || tableNumber < 1 || tableNumber > 10) {
        console.error('❌ Geçersiz masa numarası:', tableNumber);
        addToast('error', 'Invalid table number', 3000);
        return;
      }

      // Aynı masaya tıklanırsa işlem yapma
      if (selectedTable === tableNumber) {
        console.log('ℹ️ Zaten seçili masa:', tableNumber);
        return;
      }

      // Loading state kontrolü
      if (tableSelectionLoading !== null) {
        console.log('⚠️ Zaten masa değişimi yapılıyor, bekleniyor...');
        return;
      }

      console.log('🔄 Masa değiştiriliyor:', selectedTable, '->', tableNumber);
      
      // Loading state'i set et
      setTableSelectionLoading(tableNumber);
      
      // Masa değiştir
      setSelectedTable(tableNumber);
      addToast('info', `Switching to table ${tableNumber}`, 2000);
      
      // Loading state'i daha kısa tut
      setTimeout(() => {
        setTableSelectionLoading(null);
        console.log('✅ Masa değişimi tamamlandı:', tableNumber);
      }, 500); // 1 saniye yerine 500ms
      
    } catch (error) {
      console.error('❌ Masa seçim hatası:', error);
      addToast('error', 'Failed to switch table', 3000);
      setTableSelectionLoading(null);
    }
  };

  // Handle category selection
  const handleCategoryChange = (category: typeof selectedCategory) => {
    setSelectedCategory(category);
  };

  // Filter products by selected category
  // Backend'den gelen response formatı: { success: true, message: "...", data: { items: [...], pagination: {...} } }
  const productsData = products.data?.data?.items || products.data?.items || products.data || [];
  const filteredProducts = Array.isArray(productsData) 
    ? productsData.filter((product: any) => 
        selectedCategory === 'all' || product.category === selectedCategory
      )
    : [];
    
  // Debug: Ürün durumunu kontrol et
  console.log('🔍 Products state:', products);
  console.log('🔍 Products.data:', products.data);
  console.log('🔍 Products.data.data:', products.data?.data);
  console.log('🔍 Products.data.data.items:', products.data?.data?.items);
  console.log('🔍 ProductsData:', productsData);
  console.log('🔍 Filtered products:', filteredProducts);
  console.log('🔍 Selected category:', selectedCategory);

  // Handle payment processing - PaymentModal'ı açar
  const handlePayment = () => {
    if (!cart || cart.items.length === 0) {
      addToast('warning', 'Cart is empty. Please add items first.', 3000);
      return;
    }

    if (!selectedTable) {
      addToast('error', 'No table selected. Please select a table first.', 3000);
      return;
    }

    // Haptic feedback for payment button press
    Vibration.vibrate(50);

    // PaymentModal'ı aç
    setPaymentModalVisible(true);
  };

  // PaymentModal'dan gelen başarılı ödeme
  const handlePaymentSuccess = async (paymentId: string) => {
    try {
      addToast('success', `Payment successful! Payment ID: ${paymentId}`, 5000);
      
      // Başarılı ödemeden sonra masayı temizle
      clearCurrentCart(selectedTable);
      
      // Masayı 1'e geri döndür
      setSelectedTable(1);
      
    } catch (error) {
      console.error('Payment success handling error:', error);
      addToast('error', 'Payment success handling failed.', 5000);
    }
  };

  // Handle quantity update
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
    
    // İşlem tamamlandıktan sonra cart state'ini güncelle
    const updatedCart = getCartForTable(selectedTable);
    setCart(updatedCart);
    console.log('🔄 Miktar güncellendi, cart state güncellendi:', updatedCart);
  };

  // Handle item removal
  const handleItemRemove = async (itemId: string) => {
    if (!selectedTable) {
      addToast('error', 'No table selected. Please select a table first.', 3000);
      return;
    }

    await removeFromCart(selectedTable, itemId);
    
    // İşlem tamamlandıktan sonra cart state'ini güncelle
    const updatedCart = getCartForTable(selectedTable);
    setCart(updatedCart);
    console.log('🗑️ Ürün kaldırıldı, cart state güncellendi:', updatedCart);
    
    addToast('info', 'Item removed from cart', 2000);
  };

  // Handle cart clearing - Direct execution without confirmation
  const handleClearCart = async () => {
    console.log('🧹 handleClearCart called for table:', selectedTable);
    if (!selectedTable) {
      addToast('error', 'No table selected. Please select a table first.', 3000);
      return;
    }

    console.log(`🚀 Direct clear table ${selectedTable} - no confirmation needed`);
    console.log('🔍 clearCurrentCart function type:', typeof clearCurrentCart);
    console.log('🔍 clearCurrentCart function:', clearCurrentCart);
    
    try {
      console.log(`🚀 About to call clearCurrentCart(${selectedTable})...`);
      await clearCurrentCart(selectedTable);
      console.log(`✅ clearCurrentCart(${selectedTable}) completed`);
      
      // Force UI refresh - cart state'ini agresif şekilde güncelle
      console.log('🔄 Force refreshing UI state...');
      
      // 1. Local cart state'ini direkt null yap
      setCart(null);
      console.log('✅ Cart state set to null');
      
      // 2. Backend'den fresh cart yükle
      try {
        const freshCartResult = await loadCartForTable(selectedTable);
        if (freshCartResult?.success && freshCartResult.cart) {
          setCart(freshCartResult.cart);
          console.log('✅ Fresh cart loaded from backend:', freshCartResult.cart);
        } else {
          // Backend'de cart yoksa null kalsın
          setCart(null);
          console.log('✅ No cart found in backend, keeping null');
        }
      } catch (loadError) {
        console.warn('⚠️ Failed to load fresh cart, keeping null:', loadError);
        setCart(null);
      }
      
      // 3. useCart hook'undan da güncelle
      const hookCart = getCartForTable(selectedTable);
      console.log('🔍 Hook cart after clear:', hookCart);
      
      console.log('🧹 UI force refresh completed');
    } catch (error) {
      console.error(`❌ Error clearing table ${selectedTable}:`, error);
      addToast('error', `Failed to clear table ${selectedTable}`, 3000);
    }
  };

  // Handle clear all tables - Direct execution without confirmation
  const handleClearAllTables = async () => {
    console.log('🧹 handleClearAllTables called - direct execution');
    console.log('🔍 clearAllTables function type:', typeof clearAllTables);
    console.log('🔍 clearAllTables function:', clearAllTables);
    
    try {
      console.log('🚀 About to call clearAllTables()...');
      const result = await clearAllTables();
      console.log('📦 clearAllTables() result:', result);
      
      if (result && result.success) {
        // Force UI refresh for all tables
        console.log('🔄 Force refreshing UI state for all tables...');
        
        // 1. Tüm local state'leri sıfırla
        setCart(null);
        setSelectedTable(1);
        console.log('✅ All local states cleared');
        
        // 2. Selected table için fresh cart yükle (masa 1)
        try {
          const freshCartResult = await loadCartForTable(1);
          if (freshCartResult?.success && freshCartResult.cart) {
            setCart(freshCartResult.cart);
            console.log('✅ Fresh cart loaded for table 1:', freshCartResult.cart);
          } else {
            setCart(null);
            console.log('✅ No cart found for table 1, keeping null');
          }
        } catch (loadError) {
          console.warn('⚠️ Failed to load fresh cart for table 1:', loadError);
          setCart(null);
        }
        
        console.log('✅ ALL TABLES CLEARED:', result);
        console.log('🧹 All tables UI force refresh completed');
      } else {
        addToast('error', `Failed to clear all tables: ${result?.message || 'Unknown error'}`, 5000);
      }
    } catch (error) {
      console.error('❌ Error clearing all tables:', error);
      addToast('error', 'Error clearing all tables', 3000);
    }
  };

  return (
    <SafeAreaView style={styles.container}>
      {/* Toast Notifications */}
      <ToastContainer toasts={toasts} onRemove={removeToast} />

      {/* Header */}
      <View style={styles.header}>
        <Text style={styles.headerTitle}>Cash Register</Text>
        <Text style={styles.headerSubtitle}>Table Management & Payments</Text>
        {selectedTable && (
          <Text style={styles.activeTableInfo}>Active Table: {selectedTable}</Text>
        )}
        {recoveryLoading && (
          <Text style={styles.recoveryLoadingText}>🔄 Loading table orders...</Text>
        )}
      </View>

      {/* Scrollable Content */}
      <ScrollView style={styles.scrollContainer} showsVerticalScrollIndicator={false}>
        {/* Table Selection - Tab Style */}
        <View style={styles.tableSection}>
          <Text style={styles.sectionTitle}>Select Table</Text>
          <ScrollView 
            horizontal 
            showsHorizontalScrollIndicator={false} 
            style={styles.tableScroll}
            contentContainerStyle={styles.tableScrollContent}
            bounces={false}
            decelerationRate="fast"
            scrollEventThrottle={16} // Scroll event throttling
            keyboardShouldPersistTaps="handled" // Keyboard ile touch handling
            nestedScrollEnabled={false} // Nested scroll'u devre dışı bırak
          >
            {tableNumbers.map((tableNumber) => {
              const tableCart = getCartForTable(tableNumber);
              const hasItems = tableCart && tableCart.items.length > 0;
              
              // Recovery data'dan masa için ürün sayısını al
              const recoveryOrder = recoveryData?.tableOrders?.find(
                order => order.tableNumber === tableNumber
              );
              const recoveryItemCount = recoveryOrder?.itemCount || 0;
              
              // Hem local cart hem de recovery data'dan ürün sayısını kontrol et
              const finalItemCount = hasItems ? (tableCart?.totalItems || tableCart?.items?.length || 0) : recoveryItemCount;
              const shouldShowItemCount = finalItemCount > 0;
              
              return (
                <TouchableOpacity
                  key={tableNumber}
                  style={[
                    styles.tableTab,
                    selectedTable === tableNumber && styles.selectedTableTab,
                    shouldShowItemCount && styles.tableTabWithItems,
                    tableSelectionLoading === tableNumber && styles.tableTabLoading
                  ]}
                  onPress={() => {
                    console.log('🔄 Masa butonu tıklandı:', tableNumber);
                    console.log('🔄 Mevcut seçili masa:', selectedTable);
                    console.log('🔄 Loading state:', tableSelectionLoading);
                    console.log('🔄 Buton style:', {
                      isSelected: selectedTable === tableNumber,
                      hasItems,
                      isLoading: tableSelectionLoading === tableNumber
                    });
                    
                    // Loading state kontrolü - sadece farklı masaya tıklanırsa işlem yap
                    if (tableSelectionLoading !== null) {
                      console.log('⚠️ Loading state aktif, işlem bekleniyor...');
                      addToast('info', 'Please wait, table switching in progress...', 2000);
                      return;
                    }
                    
                    // State validation
                    if (typeof tableNumber !== 'number' || tableNumber < 1 || tableNumber > 10) {
                      console.error('❌ Geçersiz masa numarası:', tableNumber);
                      addToast('error', 'Invalid table number', 3000);
                      return;
                    }
                    
                    // Haptic feedback ekle
                    Vibration.vibrate(30);
                    
                    // handleTableSelect'i try-catch ile çağır
                    try {
                      handleTableSelect(tableNumber);
                    } catch (error) {
                      console.error('❌ Masa seçim hatası:', error);
                      addToast('error', 'Failed to select table', 3000);
                    }
                  }}
                  onPressIn={() => {
                    console.log('🔄 Masa butonu press in:', tableNumber);
                  }}
                  onPressOut={() => {
                    console.log('🔄 Masa butonu press out:', tableNumber);
                  }}
                  onLongPress={() => {
                    console.log('🔄 Masa butonu long press:', tableNumber);
                  }}
                  activeOpacity={0.7}
                  hitSlop={{ top: 20, bottom: 20, left: 20, right: 20 }} // Hit area'yı daha da büyüt
                  delayPressIn={0}
                  delayPressOut={0}
                  disabled={false} // Disabled state'i kaldır - sadece loading kontrolü yap
                  pressRetentionOffset={{ top: 25, bottom: 25, left: 25, right: 25 }} // Press retention'ı büyüt
                  accessible
                  accessibilityLabel={`Table ${tableNumber}`}
                  accessibilityHint={`Select table ${tableNumber}`}
                  accessibilityRole="button"
                  accessibilityState={{ 
                    selected: selectedTable === tableNumber,
                    disabled: false
                  }}
                >
                  <Text style={[
                    styles.tableTabText,
                    selectedTable === tableNumber && styles.selectedTableTabText,
                    shouldShowItemCount && styles.tableTabTextWithItems,
                    tableSelectionLoading === tableNumber && styles.tableTabTextLoading
                  ]}>
                    {tableSelectionLoading === tableNumber ? '...' : tableNumber}
                  </Text>
                  {shouldShowItemCount && (
                    <View style={styles.tableItemIndicator}>
                      <Text style={styles.tableItemIndicatorText}>{finalItemCount}</Text>
                    </View>
                  )}
                </TouchableOpacity>
              );
            })}
            
            {/* Clear All Tables Button - Son masanın sağında */}
            <TouchableOpacity
              style={styles.clearAllTablesButton}
              onPress={() => {
                console.log('🧹 Clear ALL Tables button clicked!');
                handleClearAllTables();
              }}
              activeOpacity={0.7}
              accessible
              accessibilityLabel="Clear All Tables"
              accessibilityHint="Clear all items from all tables - DANGEROUS!"
              accessibilityRole="button"
            >
              <Text style={styles.clearAllTablesIcon}>🧹</Text>
              <Text style={styles.clearAllTablesText}>Clear{'\n'}ALL</Text>
            </TouchableOpacity>
          </ScrollView>
        </View>

        {/* Category Filter */}
        <View style={styles.categorySection}>
          <Text style={styles.sectionTitle}>Categories</Text>
          <CategoryFilter
            selectedCategory={selectedCategory}
            onCategoryChange={handleCategoryChange}
          />
        </View>

        {/* Products Section */}
        <View style={styles.productsSection}>
          <Text style={styles.sectionTitle}>Available Products</Text>
          {products.loading ? (
            <View style={styles.loadingContainer}>
              <Text style={styles.loadingText}>Loading products...</Text>
            </View>
          ) : products.error ? (
            <View style={styles.errorContainer}>
              <Text style={styles.errorText}>Error loading products: {products.error}</Text>
              <TouchableOpacity onPress={refreshProducts} style={styles.retryButton}>
                <Text style={styles.retryButtonText}>Retry</Text>
              </TouchableOpacity>
            </View>
          ) : filteredProducts.length > 0 ? (
            <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.productsScroll}>
              {filteredProducts.map((product: any) => {
                // Sepetteki bu ürünün miktarını bul
                const cartItem = cart?.items.find((item: any) => item.productId === product.id);
                const quantityInCart = cartItem?.quantity || 0;
                
                return (
                  <TouchableOpacity
                    key={product.id}
                    style={[
                      styles.productCard,
                      quantityInCart > 0 && styles.productCardInCart
                    ]}
                    onPress={async () => {
                      if (!selectedTable) {
                        addToast('error', 'Please select a table first', 3000);
                        return;
                      }

                      // Haptic feedback for product selection
                      Vibration.vibrate(30);
                      
                      // Add product to cart with table number
                      await addToCart({
                        productId: product.id,
                        productName: product.name,
                        quantity: 1,
                        unitPrice: product.price,
                        notes: undefined
                      }, selectedTable);
                      
                      // Ürün eklendikten sonra cart state'ini güncelle
                      const updatedCart = getCartForTable(selectedTable);
                      setCart(updatedCart);
                      console.log('✅ Ürün eklendi, cart güncellendi:', updatedCart);
                      
                      const newQuantity = quantityInCart + 1;
                      addToast('success', `${product.name} added to table ${selectedTable} (${newQuantity}x)`, 2000);
                    }}
                  >
                    {/* Quantity Badge */}
                    {quantityInCart > 0 && (
                      <View style={styles.quantityBadge}>
                        <Text style={styles.quantityBadgeText}>{quantityInCart}x</Text>
                      </View>
                    )}
                    
                    <Text style={styles.productName}>{product.name}</Text>
                    <Text style={styles.productPrice}>€{product.price.toFixed(2)}</Text>
                    <Text style={styles.productStock}>Stock: {product.stockQuantity}</Text>
                  </TouchableOpacity>
                );
              })}
            </ScrollView>
          ) : (
            <View style={styles.noProductsContainer}>
              <Text style={styles.noProductsText}>
                {selectedCategory === 'all' ? 'No products available' : `No products in ${selectedCategory} category`}
              </Text>
            </View>
          )}
        </View>

        {/* Cart Items - Simplified */}
        <View 
          key={`cart-section-${selectedTable}-${cart?.cartId || 'empty'}-${cart?.items?.length || 0}`} 
          style={styles.cartSection}
        >
          <View style={styles.cartHeader}>
            <Text style={styles.sectionTitle}>Cart Items - Table {selectedTable}</Text>
            {cart && cart.items.length > 0 && (
              <TouchableOpacity 
                onPress={() => {
                  console.log('🧹 Clear Table button clicked!');
                  handleClearCart();
                }} 
                style={styles.clearButton}
              >
                <Text style={styles.clearButtonText}>Clear Table {selectedTable}</Text>
              </TouchableOpacity>
            )}
          </View>

          {cartLoading ? (
            <View style={styles.loadingContainer}>
              <Text style={styles.loadingText}>Loading cart...</Text>
            </View>
          ) : cartError ? (
            <View style={styles.errorContainer}>
              <Text style={styles.errorText}>Cart error: {cartError}</Text>
            </View>
          ) : cart && cart.items.length > 0 ? (
            <ScrollView 
              key={`cart-items-${selectedTable}-${cart.cartId}-${cart.items.length}`}
              style={styles.cartItems}
            >
              {cart.items.map((item: any) => (
                <View key={`${item.id}-${item.quantity}-${selectedTable}`} style={styles.cartItem}>
                  <View style={styles.itemInfo}>
                    <Text style={styles.itemName}>{item.productName}</Text>
                    <Text style={styles.itemPrice}>€{(item.unitPrice || item.price || 0).toFixed(2)}</Text>
                  </View>
                  <View style={styles.itemActions}>
                    <TouchableOpacity
                      style={styles.quantityButton}
                      onPress={() => handleQuantityUpdate(item.id, item.quantity - 1)}
                    >
                      <Text style={styles.quantityButtonText}>-</Text>
                    </TouchableOpacity>
                    <Text style={styles.quantityText}>{item.quantity}</Text>
                    <TouchableOpacity
                      style={styles.quantityButton}
                      onPress={() => handleQuantityUpdate(item.id, item.quantity + 1)}
                    >
                      <Text style={styles.quantityButtonText}>+</Text>
                    </TouchableOpacity>
                    <TouchableOpacity
                      style={styles.removeButton}
                      onPress={() => handleItemRemove(item.id)}
                    >
                      <Text style={styles.removeButtonText}>×</Text>
                    </TouchableOpacity>
                  </View>
                  <Text style={styles.itemTotal}>€{(item.totalPrice || item.total || 0).toFixed(2)}</Text>
                </View>
              ))}
            </ScrollView>
          ) : (
            <View style={styles.emptyCart}>
              <Text style={styles.emptyCartText}>No items in cart for table {selectedTable}</Text>
              <Text style={styles.emptyCartSubtext}>Select a table and add items to get started</Text>
            </View>
          )}
        </View>

        {/* Cart Summary */}
        {!cartLoading && !cartError && cart && cart.items.length > 0 && (
          <View style={styles.summarySection}>
            <View style={styles.summaryRow}>
              <Text style={styles.summaryLabel}>Subtotal:</Text>
              <Text style={styles.summaryValue}>€{(cart.subtotal || cart.grandTotal || 0).toFixed(2)}</Text>
            </View>
            <View style={styles.summaryRow}>
              <Text style={styles.summaryLabel}>Tax (20%):</Text>
              <Text style={styles.summaryValue}>€{(cart.totalTax || ((cart.grandTotal || 0) * 0.2)).toFixed(2)}</Text>
            </View>
            <View style={styles.summaryRow}>
              <Text style={styles.summaryLabel}>Total:</Text>
              <Text style={styles.summaryValue}>€{(cart.grandTotal || 0).toFixed(2)}</Text>
            </View>
          </View>
        )}

        {/* New Order Status Indicator */}
        {!cartLoading && !cartError && (!cart || cart.items.length === 0) ? (
          <View style={styles.newOrderSection}>
            <View style={styles.newOrderStatus}>
              <Text style={styles.newOrderTitle}>🆕 New Order Ready</Text>
              <Text style={styles.newOrderSubtitle}>Table {selectedTable} is ready for new items</Text>
              <Text style={styles.newOrderInfo}>Previous order completed successfully</Text>
            </View>
          </View>
        ) : null}

        {/* Payment Button - Simplified */}
        {!cartLoading && !cartError && cart && cart.items.length > 0 && (
          <View style={styles.paymentButtonContainer}>
            <TouchableOpacity
              style={[
                styles.paymentButton,
                (paymentProcessing || preventDoubleClick) && styles.paymentButtonDisabled
              ]}
              onPress={handlePayment}
              disabled={paymentProcessing || preventDoubleClick}
              activeOpacity={paymentProcessing || preventDoubleClick ? 1.0 : 0.8}
            >
              <View style={styles.paymentButtonContent}>
                {(paymentProcessing || preventDoubleClick) && (
                  <View style={styles.loadingSpinner}>
                    <Text style={styles.spinnerText}>⏳</Text>
                  </View>
                )}
                                 <Text style={styles.paymentButtonText}>
                   {paymentProcessing ? 'Processing Payment...' : 
                    preventDoubleClick ? 'Payment in Progress...' : 
                    `Complete Payment - €${(cart.grandTotal || 0).toFixed(2)}`}
                 </Text>
              </View>
            </TouchableOpacity>

            {/* Error Display */}
            {error && (
              <View style={styles.errorContainer}>
                <Text style={styles.errorText}>{error}</Text>
              </View>
            )}
          </View>
        )}
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

// Styles
const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  header: {
    backgroundColor: '#2196F3',
    padding: 20,
    alignItems: 'center',
  },
  headerTitle: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#fff',
    marginBottom: 5,
  },
  headerSubtitle: {
    fontSize: 16,
    color: '#fff',
    opacity: 0.9,
  },
  activeTableInfo: {
    fontSize: 16,
    color: '#fff',
    marginTop: 5,
    opacity: 0.9,
  },
  tableSection: {
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
  tableScroll: {
    flexDirection: 'row',
  },
  tableScrollContent: {
    alignItems: 'center',
  },
  // Tab Style Table Buttons
  tableTab: {
    paddingHorizontal: 20,
    paddingVertical: 12,
    marginRight: 10,
    borderRadius: 8,
    backgroundColor: '#f0f0f0',
    borderWidth: 2,
    borderColor: 'transparent',
    minWidth: 60,
    minHeight: 50, // Minimum height ekle
    alignItems: 'center',
    justifyContent: 'center',
    elevation: 3, // Android için shadow - artır
    shadowColor: '#000', // iOS için shadow
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.15, // Shadow opacity artır
    shadowRadius: 4,
    zIndex: 10, // Z-index artır
    // Touch feedback için ek özellikler
    transform: [{ scale: 1 }], // Transform ekle
    // Touch handling için ek özellikler
    overflow: 'visible', // Overflow visible yap
  },
  selectedTableTab: {
    backgroundColor: '#2196F3',
    borderColor: '#1976D2',
    elevation: 6, // Seçili masa için daha yüksek elevation
    shadowOpacity: 0.25, // Shadow opacity artır
    zIndex: 15, // Z-index artır
    transform: [{ scale: 1.05 }], // Seçili masa için büyüt
    overflow: 'visible',
  },
  tableTabWithItems: {
    borderColor: '#4CAF50',
    borderWidth: 2,
    elevation: 4, // Elevation artır
    zIndex: 12, // Z-index artır
    transform: [{ scale: 1.02 }], // Ürünü olan masa için hafif büyüt
    overflow: 'visible',
  },
  tableTabLoading: {
    backgroundColor: '#e0e0e0',
    borderColor: '#ccc',
    borderWidth: 2,
    opacity: 0.8, // Opacity artır
    transform: [{ scale: 0.98 }], // Loading sırasında hafif küçült
    elevation: 2,
    zIndex: 8,
    overflow: 'visible',
  },
  tableTabText: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#666',
  },
  selectedTableTabText: {
    color: '#fff',
  },
  tableTabTextWithItems: {
    color: '#4CAF50',
  },
  tableTabTextLoading: {
    color: '#999',
  },
  tableItemIndicator: {
    position: 'absolute',
    top: -5,
    right: -5,
    backgroundColor: '#FF9800',
    borderRadius: 10,
    paddingHorizontal: 5,
    paddingVertical: 2,
    minWidth: 20,
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 20, // En yüksek z-index
    elevation: 8, // Android için yüksek elevation
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.4, // Shadow opacity artır
    shadowRadius: 3, // Shadow radius artır
    overflow: 'visible',
  },
  tableItemIndicatorText: {
    color: '#fff',
    fontSize: 10,
    fontWeight: 'bold',
  },
  cartSection: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 10,
  },
  cartHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 15,
  },
  clearButton: {
    backgroundColor: '#f44336',
    paddingHorizontal: 15,
    paddingVertical: 8,
    borderRadius: 5,
  },
  clearButtonText: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '500',
  },
  cartItems: {
    maxHeight: 300,
  },
  cartItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
  },
  itemInfo: {
    flex: 1,
  },
  itemName: {
    fontSize: 16,
    fontWeight: '500',
    color: '#333',
    marginBottom: 4,
  },
  itemPrice: {
    fontSize: 14,
    color: '#666',
  },
  itemActions: {
    flexDirection: 'row',
    alignItems: 'center',
    marginHorizontal: 15,
  },
  quantityButton: {
    width: 30,
    height: 30,
    borderRadius: 15,
    backgroundColor: '#e0e0e0',
    justifyContent: 'center',
    alignItems: 'center',
  },
  quantityButtonText: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#666',
  },
  quantityText: {
    fontSize: 16,
    fontWeight: '500',
    marginHorizontal: 15,
    color: '#333',
  },
  removeButton: {
    width: 30,
    height: 30,
    borderRadius: 15,
    backgroundColor: '#ffebee',
    justifyContent: 'center',
    alignItems: 'center',
    marginLeft: 10,
  },
  removeButtonText: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#f44336',
  },
  itemTotal: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
  },
  emptyCart: {
    alignItems: 'center',
    paddingVertical: 40,
  },
  emptyCartText: {
    fontSize: 18,
    color: '#999',
    marginBottom: 10,
  },
  emptyCartSubtext: {
    fontSize: 14,
    color: '#ccc',
    textAlign: 'center',
  },
  summarySection: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 10,
  },
  summaryRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 10,
  },
  summaryLabel: {
    fontSize: 16,
    color: '#666',
  },
  summaryValue: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
  },

  paymentButton: {
    backgroundColor: '#4CAF50',
    paddingVertical: 15,
    borderRadius: 5,
    alignItems: 'center',
    marginBottom: 15,
  },
  paymentButtonDisabled: {
    backgroundColor: '#ccc',
  },
  paymentButtonContent: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  loadingSpinner: {
    marginRight: 10,
  },
  spinnerText: {
    fontSize: 20,
  },
  paymentButtonText: {
    color: '#fff',
    fontSize: 18,
    fontWeight: 'bold',
  },
  errorContainer: {
    backgroundColor: '#ffebee',
    padding: 15,
    borderRadius: 5,
    borderLeftWidth: 4,
    borderLeftColor: '#f44336',
  },
  errorText: {
    color: '#f44336',
    fontSize: 14,
  },
  newOrderSection: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 10,
    borderRadius: 5,
    borderLeftWidth: 4,
    borderLeftColor: '#4CAF50',
  },
  newOrderStatus: {
    alignItems: 'center',
  },
  newOrderTitle: {
    fontSize: 20,
  },
  // Products Section Styles
  productsSection: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 10,
    borderRadius: 5,
  },
  productsScroll: {
    flexDirection: 'row',
  },
  productCard: {
    backgroundColor: '#f8f9fa',
    padding: 15,
    borderRadius: 8,
    marginRight: 15,
    minWidth: 140,
    borderWidth: 1,
    borderColor: '#2196F3',
    elevation: 2,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
  },
  productName: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
    marginBottom: 5,
  },
  productPrice: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#2196F3',
    marginBottom: 3,
  },
  productStock: {
    fontSize: 12,
    color: '#666',
  },
  loadingContainer: {
    padding: 20,
    alignItems: 'center',
  },
  loadingText: {
    fontSize: 16,
    color: '#666',
  },

  retryButton: {
    backgroundColor: '#f44336',
    paddingHorizontal: 20,
    paddingVertical: 10,
    borderRadius: 5,
    alignSelf: 'flex-start',
  },
  retryButtonText: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '500',
  },
  noProductsContainer: {
    padding: 20,
    alignItems: 'center',
  },
  noProductsText: {
    fontSize: 16,
    color: '#666',
    marginBottom: 5,
  },

  newOrderSubtitle: {
    fontSize: 16,
    color: '#666',
    marginBottom: 10,
  },
  newOrderInfo: {
    fontSize: 14,
    color: '#999',
  },
  categorySection: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 10,
  },
  scrollContainer: {
    flex: 1,
  },
  paymentButtonContainer: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 20,
    alignItems: 'center',
  },
  quantityBadge: {
    position: 'absolute',
    top: 5,
    right: 5,
    backgroundColor: '#4CAF50',
    borderRadius: 10,
    paddingHorizontal: 5,
    paddingVertical: 2,
    minWidth: 30,
    alignItems: 'center',
    zIndex: 1,
  },
  quantityBadgeText: {
    color: '#fff',
    fontSize: 12,
    fontWeight: 'bold',
  },
  productCardInCart: {
    backgroundColor: '#e8f5e8',
    borderColor: '#4CAF50',
    borderWidth: 2,
  },
  
  // Clear All Tables Button Styles
  clearAllTablesButton: {
    paddingHorizontal: 15,
    paddingVertical: 12,
    marginLeft: 20, // Son masadan sonra boşluk
    borderRadius: 8,
    backgroundColor: '#ffebee', // Açık kırmızı arka plan
    borderWidth: 2,
    borderColor: '#f44336', // Kırmızı border
    alignItems: 'center',
    justifyContent: 'center',
    minWidth: 80,
    minHeight: 50,
    elevation: 4,
    shadowColor: '#f44336',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.3,
    shadowRadius: 4,
    zIndex: 10,
  },
  clearAllTablesIcon: {
    fontSize: 20,
    marginBottom: 2,
  },
  clearAllTablesText: {
    fontSize: 11,
    fontWeight: 'bold',
    color: '#f44336', // Kırmızı metin
    textAlign: 'center',
    lineHeight: 12,
  },
  recoveryLoadingText: {
    fontSize: 12,
    color: '#2196F3',
    fontStyle: 'italic',
    marginTop: 5,
  },
}); 