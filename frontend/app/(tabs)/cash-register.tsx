import React, { useState, useEffect, useCallback, useMemo } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  Modal,
  TextInput,
  ActivityIndicator,
  RefreshControl,
  FlatList,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useSystem } from '../../contexts/SystemContext';
import { productService, Product } from '../../services/api/productService';
import { paymentService, PaymentRequest } from '../../services/api/paymentService';
import { tseService } from '../../services/api/tseService';
import { receiptService } from '../../services/api/receiptService';
import { reportService } from '../../services/api/reportService';
import { customerService, Customer } from '../../services/api/customerService';
import ProductSelectionModal from '../../components/ProductSelectionModal';
import PaymentModal from '../../components/PaymentModal';
import QuickAddButtons from '../../components/QuickAddButtons';
import CategoryFilter from '../../components/CategoryFilter';
import AdvancedSearch from '../../components/AdvancedSearch';
import CustomerSelection from '../../components/CustomerSelection';
import { useTranslation } from 'react-i18next';
import { Colors, Spacing, BorderRadius, Typography } from '../../constants/Colors';

interface CartItem {
  product: Product;
  quantity: number;
  total: number;
}

// Kategori tanımları
const CATEGORIES = [
  { id: 'food', name: 'Yemek', icon: 'restaurant', color: Colors.light.categoryFood },
  { id: 'drink', name: 'İçecek', icon: 'cafe', color: Colors.light.categoryDrink },
  { id: 'dessert', name: 'Tatlı', icon: 'ice-cream', color: Colors.light.categoryDessert },
  { id: 'other', name: 'Diğer', icon: 'ellipsis-horizontal', color: Colors.light.categoryOther },
];

const ITEM_HEIGHT = 80; // Sepet öğesi yüksekliği

const CashRegister: React.FC = () => {
  const { t } = useTranslation();
  const { systemConfig, isOnline } = useSystem();
  const [cart, setCart] = useState<CartItem[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(false);
  const [showProductModal, setShowProductModal] = useState(false);
  const [showPaymentModal, setShowPaymentModal] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [refreshing, setRefreshing] = useState(false);
  const [tseStatus, setTseStatus] = useState<any>(null);
  const [printerStatus, setPrinterStatus] = useState<any>(null);
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [favoriteProducts, setFavoriteProducts] = useState<Product[]>([]);

  // Sistem durumunu kontrol et
  const checkSystemStatus = useCallback(async () => {
    try {
      // TSE durumu kontrol et
      if (systemConfig.tseEnabled) {
        const tseStatusResult = await tseService.getStatus();
        setTseStatus(tseStatusResult);
        
        // TSE bağlı değilse ve online-only modda ise uyarı ver
        if (!tseStatusResult.isConnected && systemConfig.operationMode === 'online-only') {
          Alert.alert(
            t('errors.tse_required'),
            t('errors.tse_connection_required'),
            [{ text: t('common.ok') }]
          );
        }
      }

      // Yazıcı durumu kontrol et
      if (systemConfig.printerEnabled) {
        const printerStatusResult = await receiptService.getPrinterStatus();
        setPrinterStatus(printerStatusResult);
      }
    } catch (error) {
      console.error('System status check failed:', error);
    }
  }, [systemConfig, t]);

  // Ürünleri yükle
  const loadProducts = useCallback(async () => {
    try {
      setLoading(true);
      const productsData = await productService.getAllProducts();
      setProducts(productsData || []);
      
      // Favori ürünleri belirle (stokta olan, sık kullanılan)
      const favorites = (productsData || [])
        .filter(product => product.stock > 0)
        .sort((a, b) => b.stock - a.stock)
        .slice(0, 5);
      setFavoriteProducts(favorites);
    } catch (error) {
      console.error('Products load failed:', error);
      Alert.alert(
        t('errors.load_failed'),
        t('errors.products_load_failed'),
        [{ text: t('common.ok') }]
      );
    } finally {
      setLoading(false);
    }
  }, [t]);

  // Kategoriye göre filtrelenmiş ürünler
  const filteredProducts = useMemo(() => {
    let filtered = products || []; // Güvenli hale getir
    
    // Kategori filtresi
    if (selectedCategory) {
      filtered = filtered.filter(product => product.category === selectedCategory);
    }
    
    // Arama filtresi
    if (searchQuery) {
      filtered = filtered.filter(product =>
        product.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        (product.barcode && product.barcode.includes(searchQuery))
      );
    }
    
    return filtered;
  }, [products, selectedCategory, searchQuery]);

  // Sepete ürün ekle
  const addToCart = useCallback((product: Product, quantity: number = 1) => {
    setCart(prevCart => {
      const existingItem = prevCart.find(item => item.product.id === product.id);
      
      if (existingItem) {
        // Stok kontrolü
        const newQuantity = existingItem.quantity + quantity;
        if (newQuantity > product.stock) {
          Alert.alert(
            t('errors.stock_insufficient'),
            t('errors.stock_not_enough', { available: product.stock }),
            [{ text: t('common.ok') }]
          );
          return prevCart;
        }
        
        return prevCart.map(item =>
          item.product.id === product.id
            ? { ...item, quantity: newQuantity, total: product.price * newQuantity }
            : item
        );
      } else {
        // Stok kontrolü
        if (quantity > product.stock) {
          Alert.alert(
            t('errors.stock_insufficient'),
            t('errors.stock_not_enough', { available: product.stock }),
            [{ text: t('common.ok') }]
          );
          return prevCart;
        }
        
        return [...prevCart, {
          product,
          quantity,
          total: product.price * quantity
        }];
      }
    });
  }, [t]);

  // Sepetten ürün çıkar
  const removeFromCart = useCallback((productId: string) => {
    setCart(prevCart => prevCart.filter(item => item.product.id !== productId));
  }, []);

  // Sepet miktarını güncelle
  const updateCartQuantity = useCallback((productId: string, quantity: number) => {
    setCart(prevCart => {
      const item = prevCart.find(item => item.product.id === productId);
      if (!item) return prevCart;
      
      if (quantity <= 0) {
        return prevCart.filter(item => item.product.id !== productId);
      }
      
      if (quantity > item.product.stock) {
        Alert.alert(
          t('errors.stock_insufficient'),
          t('errors.stock_not_enough', { available: item.product.stock }),
          [{ text: t('common.ok') }]
        );
        return prevCart;
      }
      
      return prevCart.map(item =>
        item.product.id === productId
          ? { ...item, quantity, total: item.product.price * quantity }
          : item
      );
    });
  }, [t]);

  // Sepeti temizle
  const clearCart = useCallback(() => {
    setCart([]);
  }, []);

  // Toplam hesapla
  const calculateTotals = useCallback(() => {
    const subtotal = cart.reduce((sum, item) => sum + item.total, 0);
    const taxStandard = cart
      .filter(item => item.product.taxType === 'standard')
      .reduce((sum, item) => sum + (item.total * 0.20), 0);
    const taxReduced = cart
      .filter(item => item.product.taxType === 'reduced')
      .reduce((sum, item) => sum + (item.total * 0.10), 0);
    const taxSpecial = cart
      .filter(item => item.product.taxType === 'special')
      .reduce((sum, item) => sum + (item.total * 0.13), 0);
    const total = subtotal + taxStandard + taxReduced + taxSpecial;
    
    return { subtotal, taxStandard, taxReduced, taxSpecial, total };
  }, [cart]);

  // Ödeme işlemi
  const processPayment = useCallback(async (paymentMethod: string, amount: number) => {
    try {
      setLoading(true);
      
      // Mod kontrolü
      if (systemConfig.operationMode === 'online-only' && !isOnline) {
        Alert.alert(
          t('errors.online_only'),
          t('errors.internet_required'),
          [{ text: t('common.ok') }]
        );
        return;
      }
      
      // TSE kontrolü
      if (systemConfig.tseEnabled && systemConfig.operationMode !== 'offline-only') {
        if (!tseStatus?.isConnected) {
          Alert.alert(
            t('errors.tse_required'),
            t('errors.tse_connection_required'),
            [{ text: t('common.ok') }]
          );
          return;
        }
      }
      
      // Ödeme verilerini hazırla
      const paymentRequest: PaymentRequest = {
        items: cart.map(item => ({
          productId: item.product.id,
          quantity: item.quantity,
          price: item.product.price,
          taxType: item.product.taxType
        })),
        payment: {
          method: paymentMethod as 'cash' | 'card' | 'voucher',
          amount,
          tseRequired: systemConfig.tseEnabled
        },
        customerId: selectedCustomer?.id
      };
      
      // Ödeme işlemini gerçekleştir
      const paymentResult = await paymentService.processPayment(paymentRequest);
      
      if (paymentResult.success) {
        // Stok güncelle
        for (const item of cart) {
          try {
            await productService.updateStock(
              item.product.id,
              item.quantity,
              'subtract'
            );
          } catch (error) {
            console.error('Stock update failed for product:', item.product.id, error);
          }
        }
        
        // Fiş oluştur ve yazdır
        if (systemConfig.printerEnabled) {
          try {
            const receipt = await receiptService.createReceipt(paymentResult.paymentId);
            await receiptService.printReceipt(receipt);
          } catch (error) {
            console.error('Receipt creation/printing failed:', error);
          }
        }
        
        // Başarı mesajı
        Alert.alert(
          t('payment.success'),
          t('payment.completed_successfully'),
          [
            {
              text: t('common.ok'),
              onPress: () => {
                clearCart();
                setSelectedCustomer(null);
                setShowPaymentModal(false);
              }
            }
          ]
        );
      } else {
        Alert.alert(
          t('payment.failed'),
          paymentResult.error || t('payment.unknown_error'),
          [{ text: t('common.ok') }]
        );
      }
    } catch (error) {
      console.error('Payment processing failed:', error);
      Alert.alert(
        t('payment.failed'),
        t('payment.processing_error'),
        [{ text: t('common.ok') }]
      );
    } finally {
      setLoading(false);
    }
  }, [cart, systemConfig, isOnline, tseStatus, selectedCustomer, t]);

  // Yenileme işlemi
  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await Promise.all([
        loadProducts(),
        checkSystemStatus()
      ]);
    } catch (error) {
      console.error('Refresh failed:', error);
    } finally {
      setRefreshing(false);
    }
  }, [loadProducts, checkSystemStatus]);

  // İlk yükleme
  useEffect(() => {
    onRefresh();
  }, [onRefresh]);

  // Sistem durumu değişikliklerini izle
  useEffect(() => {
    checkSystemStatus();
  }, [checkSystemStatus]);

  const totals = calculateTotals();

  // Virtualized list için getItemLayout
  const getItemLayout = useCallback((data: any, index: number) => ({
    length: ITEM_HEIGHT,
    offset: ITEM_HEIGHT * index,
    index,
  }), []);

  // Sepet öğesi render fonksiyonu
  const renderCartItem = useCallback(({ item }: { item: CartItem }) => (
    <View style={styles.cartItem}>
      <View style={styles.itemInfo}>
        <Text style={styles.itemName}>{item.product.name}</Text>
        <Text style={styles.itemPrice}>
          {item.product.price.toFixed(2)}€ x {item.quantity}
        </Text>
        <Text style={styles.itemTotal}>
          {item.total.toFixed(2)}€
        </Text>
      </View>
      <View style={styles.itemActions}>
        <TouchableOpacity
          onPress={() => updateCartQuantity(item.product.id, item.quantity - 1)}
          style={styles.quantityButton}
        >
          <Ionicons name="remove" size={16} color={Colors.light.error} />
        </TouchableOpacity>
        <Text style={styles.quantityText}>{item.quantity}</Text>
        <TouchableOpacity
          onPress={() => updateCartQuantity(item.product.id, item.quantity + 1)}
          style={styles.quantityButton}
        >
          <Ionicons name="add" size={16} color={Colors.light.success} />
        </TouchableOpacity>
        <TouchableOpacity
          onPress={() => removeFromCart(item.product.id)}
          style={styles.removeButton}
        >
          <Ionicons name="trash-outline" size={16} color={Colors.light.error} />
        </TouchableOpacity>
      </View>
    </View>
  ), [updateCartQuantity, removeFromCart]);

  return (
    <SafeAreaView style={styles.container}>
      {/* Üst Bilgi Çubuğu */}
      <View style={styles.header}>
        <View style={styles.statusBar}>
          <View style={styles.statusItem}>
            <Ionicons 
              name={isOnline ? 'wifi' : 'wifi-outline'} 
              size={20} 
              color={isOnline ? Colors.light.online : Colors.light.offline} 
            />
            <Text style={[styles.statusText, { color: isOnline ? Colors.light.online : Colors.light.offline }]}>
              {isOnline ? t('status.online') : t('status.offline')}
            </Text>
          </View>
          
          {systemConfig.tseEnabled && (
            <View style={styles.statusItem}>
              <Ionicons 
                name={tseStatus?.isConnected ? 'hardware-chip' : 'hardware-chip-outline'} 
                size={20} 
                color={tseStatus?.isConnected ? Colors.light.online : Colors.light.error} 
              />
              <Text style={[styles.statusText, { color: tseStatus?.isConnected ? Colors.light.online : Colors.light.error }]}>
                TSE {tseStatus?.isConnected ? t('status.connected') : t('status.disconnected')}
              </Text>
            </View>
          )}
          
          {systemConfig.printerEnabled && (
            <View style={styles.statusItem}>
              <Ionicons 
                name={printerStatus?.isConnected ? 'print' : 'print-outline'} 
                size={20} 
                color={printerStatus?.isConnected ? Colors.light.online : Colors.light.error} 
              />
              <Text style={[styles.statusText, { color: printerStatus?.isConnected ? Colors.light.online : Colors.light.error }]}>
                {printerStatus?.isConnected ? t('status.connected') : t('status.disconnected')}
              </Text>
            </View>
          )}
        </View>
        
        <Text style={styles.modeText}>
          {t(`modes.${systemConfig.operationMode}`)}
        </Text>
      </View>

      <ScrollView 
        style={styles.content}
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
        }
      >
        {/* Gelişmiş Arama */}
        <AdvancedSearch
          searchQuery={searchQuery}
          onSearchChange={setSearchQuery}
          onProductSelect={(product) => addToCart(product, 1)}
          products={products}
          loading={loading}
        />

        {/* Kategori Filtresi */}
        <CategoryFilter
          categories={CATEGORIES}
          selectedCategory={selectedCategory}
          onSelectCategory={setSelectedCategory}
        />

        {/* Hızlı İşlem Butonları */}
        <QuickAddButtons
          favoriteProducts={favoriteProducts}
          onAddToCart={addToCart}
        />

        {/* Müşteri Seçimi */}
        <CustomerSelection
          selectedCustomer={selectedCustomer}
          onCustomerSelect={setSelectedCustomer}
        />

        {/* Sepet */}
        <View style={styles.cartContainer}>
          <View style={styles.cartHeader}>
            <Text style={styles.cartTitle}>{t('cart.title')}</Text>
            {cart.length > 0 && (
              <TouchableOpacity onPress={clearCart} style={styles.clearButton}>
                <Text style={styles.clearButtonText}>{t('cart.clear')}</Text>
              </TouchableOpacity>
            )}
          </View>

          {cart.length === 0 ? (
            <View style={styles.emptyCart}>
              <Ionicons name="cart-outline" size={48} color={Colors.light.textTertiary} />
              <Text style={styles.emptyCartText}>{t('cart.empty')}</Text>
            </View>
          ) : (
            <FlatList
              data={cart}
              renderItem={renderCartItem}
              keyExtractor={(item) => item.product.id}
              getItemLayout={getItemLayout}
              removeClippedSubviews={true}
              maxToRenderPerBatch={10}
              windowSize={10}
              initialNumToRender={10}
              scrollEnabled={false}
              showsVerticalScrollIndicator={false}
            />
          )}
        </View>

        {/* Toplam */}
        {cart.length > 0 && (
          <View style={styles.totalsContainer}>
            <View style={styles.totalRow}>
              <Text style={styles.totalLabel}>{t('cart.subtotal')}</Text>
              <Text style={styles.totalValue}>{totals.subtotal.toFixed(2)}€</Text>
            </View>
            <View style={styles.totalRow}>
              <Text style={styles.totalLabel}>{t('cart.tax_standard')}</Text>
              <Text style={styles.totalValue}>{totals.taxStandard.toFixed(2)}€</Text>
            </View>
            <View style={styles.totalRow}>
              <Text style={styles.totalLabel}>{t('cart.tax_reduced')}</Text>
              <Text style={styles.totalValue}>{totals.taxReduced.toFixed(2)}€</Text>
            </View>
            <View style={styles.totalRow}>
              <Text style={styles.totalLabel}>{t('cart.tax_special')}</Text>
              <Text style={styles.totalValue}>{totals.taxSpecial.toFixed(2)}€</Text>
            </View>
            <View style={[styles.totalRow, styles.grandTotal]}>
              <Text style={styles.grandTotalLabel}>{t('cart.total')}</Text>
              <Text style={styles.grandTotalValue}>{totals.total.toFixed(2)}€</Text>
            </View>
          </View>
        )}

        {/* Ödeme Butonu */}
        {cart.length > 0 && (
          <TouchableOpacity
            style={[styles.paymentButton, loading && styles.paymentButtonDisabled]}
            onPress={() => setShowPaymentModal(true)}
            disabled={loading}
          >
            {loading ? (
              <ActivityIndicator color={Colors.light.surface} />
            ) : (
              <>
                <Ionicons name="card" size={24} color={Colors.light.surface} />
                <Text style={styles.paymentButtonText}>{t('payment.process')}</Text>
              </>
            )}
          </TouchableOpacity>
        )}
      </ScrollView>

      {/* Modaller */}
      <ProductSelectionModal
        visible={showProductModal}
        onClose={() => setShowProductModal(false)}
        onSelectProduct={(product, quantity) => {
          addToCart(product, quantity);
          setShowProductModal(false);
        }}
        products={filteredProducts}
        searchQuery={searchQuery}
        loading={loading}
      />

      <PaymentModal
        visible={showPaymentModal}
        onClose={() => setShowPaymentModal(false)}
        onProcessPayment={processPayment}
        total={totals.total}
        loading={loading}
        systemConfig={systemConfig}
        isOnline={isOnline}
      />
    </SafeAreaView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: Colors.light.background,
  },
  header: {
    backgroundColor: Colors.light.surface,
    padding: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  statusBar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: Spacing.sm,
  },
  statusItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.xs,
  },
  statusText: {
    ...Typography.caption,
    fontWeight: '500',
  },
  modeText: {
    ...Typography.bodySmall,
    fontWeight: '600',
    color: Colors.light.primary,
    textAlign: 'center',
  },
  content: {
    flex: 1,
    padding: Spacing.md,
  },
  cartContainer: {
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.lg,
    padding: Spacing.md,
    marginBottom: Spacing.md,
    elevation: 2,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
  },
  cartHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: Spacing.md,
  },
  cartTitle: {
    ...Typography.h3,
    color: Colors.light.text,
  },
  clearButton: {
    padding: Spacing.sm,
  },
  clearButtonText: {
    color: Colors.light.error,
    ...Typography.bodySmall,
  },
  emptyCart: {
    alignItems: 'center',
    padding: Spacing.xl,
  },
  emptyCartText: {
    marginTop: Spacing.sm,
    ...Typography.body,
    color: Colors.light.textSecondary,
  },
  cartItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.md,
    backgroundColor: Colors.light.cartBackground,
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.sm,
    minHeight: ITEM_HEIGHT,
  },
  itemInfo: {
    flex: 1,
  },
  itemName: {
    ...Typography.body,
    fontWeight: '500',
    marginBottom: Spacing.xs,
    color: Colors.light.text,
  },
  itemPrice: {
    ...Typography.bodySmall,
    color: Colors.light.textSecondary,
    marginBottom: Spacing.xs,
  },
  itemTotal: {
    ...Typography.bodySmall,
    color: Colors.light.primary,
    fontWeight: '600',
  },
  itemActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
  },
  quantityButton: {
    padding: Spacing.xs,
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.sm,
    minWidth: 32,
    minHeight: 32,
    justifyContent: 'center',
    alignItems: 'center',
  },
  quantityText: {
    ...Typography.body,
    fontWeight: '600',
    minWidth: 24,
    textAlign: 'center',
    color: Colors.light.text,
  },
  removeButton: {
    padding: Spacing.xs,
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.sm,
    minWidth: 32,
    minHeight: 32,
    justifyContent: 'center',
    alignItems: 'center',
  },
  totalsContainer: {
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.lg,
    padding: Spacing.md,
    marginBottom: Spacing.md,
    elevation: 2,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
  },
  totalRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: Spacing.sm,
  },
  totalLabel: {
    ...Typography.bodySmall,
    color: Colors.light.textSecondary,
  },
  totalValue: {
    ...Typography.bodySmall,
    fontWeight: '500',
    color: Colors.light.text,
  },
  grandTotal: {
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
    paddingTop: Spacing.sm,
    marginTop: Spacing.sm,
  },
  grandTotalLabel: {
    ...Typography.h3,
    color: Colors.light.text,
  },
  grandTotalValue: {
    ...Typography.h3,
    fontWeight: '600',
    color: Colors.light.primary,
  },
  paymentButton: {
    backgroundColor: Colors.light.paymentButton,
    borderRadius: BorderRadius.lg,
    padding: Spacing.md,
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    gap: Spacing.sm,
    minHeight: 56,
  },
  paymentButtonDisabled: {
    backgroundColor: Colors.light.paymentButtonDisabled,
  },
  paymentButtonText: {
    color: Colors.light.surface,
    ...Typography.button,
  },
});

export default CashRegister; 