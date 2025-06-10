import React, { useState, useEffect, useCallback } from 'react';
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
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useSystem } from '../../contexts/SystemContext';
import { productService, Product } from '../../services/api/productService';
import { paymentService, PaymentRequest } from '../../services/api/paymentService';
import { tseService } from '../../services/api/tseService';
import { receiptService } from '../../services/api/receiptService';
import { reportService } from '../../services/api/reportService';
import ProductSelectionModal from '../../components/ProductSelectionModal';
import PaymentModal from '../../components/PaymentModal';
import { useTranslation } from 'react-i18next';

interface CartItem {
  product: Product;
  quantity: number;
  total: number;
}

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
      setProducts(productsData);
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
        }
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
  }, [cart, systemConfig, isOnline, tseStatus, t]);

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

  return (
    <SafeAreaView style={styles.container}>
      {/* Üst Bilgi Çubuğu */}
      <View style={styles.header}>
        <View style={styles.statusBar}>
          <View style={styles.statusItem}>
            <Ionicons 
              name={isOnline ? 'wifi' : 'wifi-outline'} 
              size={20} 
              color={isOnline ? '#4CAF50' : '#FF9800'} 
            />
            <Text style={[styles.statusText, { color: isOnline ? '#4CAF50' : '#FF9800' }]}>
              {isOnline ? t('status.online') : t('status.offline')}
            </Text>
          </View>
          
          {systemConfig.tseEnabled && (
            <View style={styles.statusItem}>
              <Ionicons 
                name={tseStatus?.isConnected ? 'hardware-chip' : 'hardware-chip-outline'} 
                size={20} 
                color={tseStatus?.isConnected ? '#4CAF50' : '#F44336'} 
              />
              <Text style={[styles.statusText, { color: tseStatus?.isConnected ? '#4CAF50' : '#F44336' }]}>
                TSE {tseStatus?.isConnected ? t('status.connected') : t('status.disconnected')}
              </Text>
            </View>
          )}
          
          {systemConfig.printerEnabled && (
            <View style={styles.statusItem}>
              <Ionicons 
                name={printerStatus?.isConnected ? 'print' : 'print-outline'} 
                size={20} 
                color={printerStatus?.isConnected ? '#4CAF50' : '#F44336'} 
              />
              <Text style={[styles.statusText, { color: printerStatus?.isConnected ? '#4CAF50' : '#F44336' }]}>
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
        {/* Arama Çubuğu */}
        <View style={styles.searchContainer}>
          <TextInput
            style={styles.searchInput}
            placeholder={t('search.products')}
            value={searchQuery}
            onChangeText={setSearchQuery}
          />
          <TouchableOpacity 
            style={styles.searchButton}
            onPress={() => setShowProductModal(true)}
          >
            <Ionicons name="add" size={24} color="white" />
          </TouchableOpacity>
        </View>

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
              <Ionicons name="cart-outline" size={48} color="#ccc" />
              <Text style={styles.emptyCartText}>{t('cart.empty')}</Text>
            </View>
          ) : (
            <View style={styles.cartItems}>
              {cart.map((item) => (
                <View key={item.product.id} style={styles.cartItem}>
                  <View style={styles.itemInfo}>
                    <Text style={styles.itemName}>{item.product.name}</Text>
                    <Text style={styles.itemPrice}>
                      {item.product.price.toFixed(2)}€ x {item.quantity}
                    </Text>
                  </View>
                  <View style={styles.itemActions}>
                    <TouchableOpacity
                      onPress={() => updateCartQuantity(item.product.id, item.quantity - 1)}
                      style={styles.quantityButton}
                    >
                      <Ionicons name="remove" size={16} color="#F44336" />
                    </TouchableOpacity>
                    <Text style={styles.quantityText}>{item.quantity}</Text>
                    <TouchableOpacity
                      onPress={() => updateCartQuantity(item.product.id, item.quantity + 1)}
                      style={styles.quantityButton}
                    >
                      <Ionicons name="add" size={16} color="#4CAF50" />
                    </TouchableOpacity>
                    <TouchableOpacity
                      onPress={() => removeFromCart(item.product.id)}
                      style={styles.removeButton}
                    >
                      <Ionicons name="trash-outline" size={16} color="#F44336" />
                    </TouchableOpacity>
                  </View>
                </View>
              ))}
            </View>
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
              <ActivityIndicator color="white" />
            ) : (
              <>
                <Ionicons name="card" size={24} color="white" />
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
        products={products}
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
    backgroundColor: '#f5f5f5',
  },
  header: {
    backgroundColor: 'white',
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  statusBar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 8,
  },
  statusItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  statusText: {
    fontSize: 12,
    fontWeight: '500',
  },
  modeText: {
    fontSize: 14,
    fontWeight: '600',
    color: '#2196F3',
    textAlign: 'center',
  },
  content: {
    flex: 1,
    padding: 16,
  },
  searchContainer: {
    flexDirection: 'row',
    marginBottom: 16,
    gap: 8,
  },
  searchInput: {
    flex: 1,
    backgroundColor: 'white',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  searchButton: {
    backgroundColor: '#2196F3',
    borderRadius: 8,
    padding: 12,
    justifyContent: 'center',
    alignItems: 'center',
  },
  cartContainer: {
    backgroundColor: 'white',
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
  },
  cartHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 16,
  },
  cartTitle: {
    fontSize: 18,
    fontWeight: '600',
  },
  clearButton: {
    padding: 8,
  },
  clearButtonText: {
    color: '#F44336',
    fontSize: 14,
  },
  emptyCart: {
    alignItems: 'center',
    padding: 32,
  },
  emptyCartText: {
    marginTop: 8,
    fontSize: 16,
    color: '#666',
  },
  cartItems: {
    gap: 12,
  },
  cartItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 12,
    backgroundColor: '#f9f9f9',
    borderRadius: 8,
  },
  itemInfo: {
    flex: 1,
  },
  itemName: {
    fontSize: 16,
    fontWeight: '500',
    marginBottom: 4,
  },
  itemPrice: {
    fontSize: 14,
    color: '#666',
  },
  itemActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  quantityButton: {
    padding: 4,
  },
  quantityText: {
    fontSize: 16,
    fontWeight: '600',
    minWidth: 24,
    textAlign: 'center',
  },
  removeButton: {
    padding: 4,
  },
  totalsContainer: {
    backgroundColor: 'white',
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
  },
  totalRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 8,
  },
  totalLabel: {
    fontSize: 14,
    color: '#666',
  },
  totalValue: {
    fontSize: 14,
    fontWeight: '500',
  },
  grandTotal: {
    borderTopWidth: 1,
    borderTopColor: '#e0e0e0',
    paddingTop: 8,
    marginTop: 8,
  },
  grandTotalLabel: {
    fontSize: 18,
    fontWeight: '600',
  },
  grandTotalValue: {
    fontSize: 18,
    fontWeight: '600',
    color: '#2196F3',
  },
  paymentButton: {
    backgroundColor: '#4CAF50',
    borderRadius: 12,
    padding: 16,
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    gap: 8,
  },
  paymentButtonDisabled: {
    backgroundColor: '#ccc',
  },
  paymentButtonText: {
    color: 'white',
    fontSize: 18,
    fontWeight: '600',
  },
});

export default CashRegister; 