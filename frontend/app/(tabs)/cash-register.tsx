import { Ionicons } from '@expo/vector-icons';
import React, { useRef, useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  Modal,
  ScrollView,
  Animated,
  Vibration,
  Alert,
} from 'react-native';

import AdvancedInvoiceModal from '../../components/AdvancedInvoiceModal';
import AdvancedPaymentOptions from '../../components/AdvancedPaymentOptions';
import CartQuickActions from '../../components/CartQuickActions';
import CouponModal from '../../components/CouponModal';
import CustomerModal from '../../components/CustomerModal';
import CustomerSelectionModal from '../../components/CustomerSelectionModal';
import EnhancedCart from '../../components/EnhancedCart';
import FavoritesSection from '../../components/FavoritesSection';
import HeaderSection from '../../components/HeaderSection';
import OrderManager from '../../components/OrderManager';
import PaymentSection from '../../components/PaymentSection';
import ProductGrid from '../../components/ProductGrid';
import ProductSelectionModal from '../../components/ProductSelectionModal';
import QuickAccessPanel from '../../components/QuickAccessPanel';
import QuickProductSearch from '../../components/QuickProductSearch';
import TableManager from '../../components/TableManager';
import { TseStatusIndicator } from '../../components/TseStatusIndicator';
import VoiceCommands from '../../components/VoiceCommands';
import { Colors, Spacing, BorderRadius } from '../../constants/Colors';
import { useAuth } from '../../contexts/AuthContext';
import { useCashRegister } from '../../hooks/useCashRegister';
import { loginWithDemoUser } from '../../services/api/authService';

export default function CashRegisterScreen() {
  const { t } = useTranslation();
  const { user, login } = useAuth();
  const [showOrderManager, setShowOrderManager] = useState(false);
  const [showTableManager, setShowTableManager] = useState(false);
  // Türkçe Açıklama: Quick actions (hızlı eylemler) ve arama bölümü kasiyer arayüzünden tamamen kaldırıldı.
  const [showVoiceCommands, setShowVoiceCommands] = useState(false);
  const [showAdvancedPayment, setShowAdvancedPayment] = useState(false);
  const [showAdvancedInvoice, setShowAdvancedInvoice] = useState(false);
  const [showCouponModal, setShowCouponModal] = useState(false);
  const [showCustomerModal, setShowCustomerModal] = useState(false);
  const [showCustomerSelectionModal, setShowCustomerSelectionModal] = useState(false);
  const [showProductSelectionModal, setShowProductSelectionModal] = useState(false);
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [appliedCoupon, setAppliedCoupon] = useState<Coupon | null>(null);
  const [couponDiscount, setCouponDiscount] = useState(0);
  // TSE durumu için state
  const [tseStatus, setTseStatus] = useState<any>(undefined);

  // Animasyonlar
  const successAnimation = useRef(new Animated.Value(0)).current;
  const changeAnimation = useRef(new Animated.Value(0)).current;
  const cartItemAnimation = useRef(new Animated.Value(0)).current;

  // Otomatik login
  useEffect(() => {
    const autoLogin = async () => {
      try {
        if (!user) {
          console.log('Auto-login with demo user...');
          await loginWithDemoUser();
          console.log('Auto-login successful');
        }
      } catch (error) {
        console.error('Auto-login failed:', error);
      }
    };

    autoLogin();
  }, [user]);

  const cashRegister = useCashRegister(user);

  // Ürün ekleme başarı animasyonu
  const triggerAddSuccessAnimation = () => {
    cartItemAnimation.setValue(0);
    Animated.spring(cartItemAnimation, {
      toValue: 1,
      tension: 100,
      friction: 8,
      useNativeDriver: true,
    }).start();
  };

  // Ürün ekleme işlemi
  const handleProductPress = (product: Product) => {
    cashRegister.addToCart(product, () => {
      triggerAddSuccessAnimation();
      Vibration.vibrate(50);
    });
  };

  // Sepetten ürün kaldır
  const handleRemoveFromCart = async (itemId: string) => {
    if (cashRegister.currentCart) {
      const cartItem = cashRegister.currentCart.items.find(item => item.id === itemId);
      if (cartItem) {
        await cashRegister.removeItemFromBackendCart(cartItem.id);
      }
    }
  };

  // Sepet miktarını güncelle
  const handleUpdateQuantity = async (itemId: string, newQuantity: number) => {
    if (cashRegister.currentCart) {
      const cartItem = cashRegister.currentCart.items.find(item => item.id === itemId);
      if (cartItem) {
        if (newQuantity <= 0) {
          await cashRegister.removeItemFromBackendCart(cartItem.id);
        } else {
          await cashRegister.updateCartQuantity(cartItem.product.id, newQuantity);
        }
      }
    }
  };

  // Barkod tarama işlemi
  // Türkçe Açıklama: Barcode scanner ile ilgili tüm import, state, fonksiyon, modal ve butonlar kaldırıldı.
  // Hızlı Eylemler (Scanner) ile ilgili butonlar zaten kaldırılmıştı.

  // Sesli komut işlemi
  const handleVoiceCommand = (command: string, params?: any) => {
    switch (command) {
      case 'add_product':
        // Ürün ekleme komutu
        break;
      case 'clear_cart':
        cashRegister.clearCart();
        break;
      case 'process_payment':
        setShowAdvancedPayment(true);
        break;
      case 'create_invoice':
        handleInvoiceCreateClick();
        break;
      case 'print_receipt':
        // Fiş yazdırma
        break;
    }
    setShowVoiceCommands(false);
  };

  // TSE durumu değişikliği
  const handleTseStatusChange = (status: TseStatus) => {
    setTseStatus(status);
  };

  // Fatura oluşturma butonuna tıklama
  const handleInvoiceCreateClick = () => {
    if (!tseStatus?.canCreateInvoices) {
      Alert.alert(
        'TSE Error',
        tseStatus?.errorMessage || 'TSE cihazı bağlı değil. Fatura kesilemez.',
        [
          { text: 'TSE Durumunu Kontrol Et', onPress: () => checkTseStatus() },
          { text: 'Tamam', style: 'default' },
        ]
      );
      return;
    }
    setShowAdvancedInvoice(true);
  };

  // Kupon uygulama işlemi
  const handleCouponApplied = (coupon: Coupon, discountAmount: number) => {
    setAppliedCoupon(coupon);
    setCouponDiscount(discountAmount);
    Alert.alert('Kupon Uygulandı', `${coupon.name} kuponu başarıyla uygulandı. İndirim: €${discountAmount.toFixed(2)}`);
  };

  // Müşteri seçimi işlemi
  const handleCustomerSelected = (customer: Customer) => {
    setSelectedCustomer(customer);
    Alert.alert('Müşteri Seçildi', `${customer.name} seçildi. Kategori: ${customer.category}`);
  };

  // Gelişmiş ödeme işlemi
  const handlePaymentMethodSelect = (method: any, amount?: number) => {
    cashRegister.setSelectedPaymentMethod(method.id);
    if (amount) {
      cashRegister.setPaymentAmount(amount.toString());
    }
    cashRegister.handlePayment();
  };

  // Fatura oluşturma işlemi
  const handleInvoiceCreate = async (invoiceData: any) => {
    try {
      // TSE durumunu tekrar kontrol et
      const currentTseStatus = await checkTseStatus();
      if (!currentTseStatus.canCreateInvoices) {
        throw new Error(currentTseStatus.errorMessage || 'TSE cihazı bağlı değil. Fatura kesilemez.');
      }

      // Fatura verilerini hazırla
      const request = {
        subtotal: invoiceData.subtotal,
        taxAmount: invoiceData.taxAmount,
        totalAmount: invoiceData.totalAmount,
        dueDate: invoiceData.dueDate.toISOString(),
        customerName: invoiceData.customer.name,
        customerEmail: invoiceData.customer.email,
        customerPhone: invoiceData.customer.phone,
        customerAddress: invoiceData.customer.address,
        customerTaxNumber: invoiceData.customer.taxNumber,
        companyName: 'Registrierkasse GmbH', // Varsayılan firma bilgileri
        companyTaxNumber: 'ATU12345678',
        companyAddress: 'Hauptstraße 1, 1010 Wien, Österreich',
        companyPhone: '+43 1 234 567 890',
        companyEmail: 'info@registrierkasse.at',
        items: invoiceData.items,
        taxDetails: invoiceData.taxDetails,
        notes: invoiceData.notes,
        termsAndConditions: invoiceData.termsAndConditions,
      };

      // Backend'e gönder
      const invoice = await createInvoice(request);

      // Otomatik gönderim
      if (invoiceData.autoSend && invoiceData.sendToEmail) {
        await sendInvoiceEmail(invoice.id, {
          email: invoiceData.sendToEmail,
          subject: `Invoice: ${invoice.invoiceNumber}`,
          message: 'Please find attached invoice.',
        });
      }

      // Sepeti temizle
      cashRegister.clearCart();

      console.log('Invoice created:', invoice.invoiceNumber);
      return invoice;
    } catch (error: any) {
      console.error('Failed to create invoice:', error);
      
      // TSE hatası ise özel mesaj göster
      if (error.message?.includes('TSE')) {
        Alert.alert(
          'TSE Error',
          error.message,
          [
            { text: 'TSE Durumunu Kontrol Et', onPress: () => checkTseStatus() },
            { text: 'Tamam', style: 'default' },
          ]
        );
      } else {
        Alert.alert('Error', 'Failed to create invoice. Please try again.');
      }
      throw error;
    }
  };

  return (
    <View style={styles.container}>
      {/* Basit Header */}
      <View style={styles.header}>
        <Text style={styles.headerTitle}>Kasse</Text>
        <Text style={styles.headerSubtitle}>
          {user?.firstName} {user?.lastName}
        </Text>
      </View>

      {/* Ana İçerik - Ürünler ve Sepet Yan Yana */}
      <View style={styles.mainContent}>
        {/* Sol Panel - Ürün Listesi */}
        <View style={styles.productsPanel}>
          <Text style={styles.panelTitle}>Ürünler</Text>
          <ProductGrid
            onProductPress={(product) => handleProductPress(product)}
            showStock={false}
          />
        </View>

        {/* Sağ Panel - Sepet ve Toplam */}
        <View style={styles.cartPanel}>
          <Text style={styles.panelTitle}>Sepet</Text>
          
          {/* Sepet İçeriği */}
          <View style={styles.cartContent}>
            {cashRegister.cart?.items?.length > 0 ? (
              <ScrollView style={styles.cartItemsList}>
                {cashRegister.cart.items.map((item) => (
                  <Animated.View 
                    key={item.id} 
                    style={[
                      styles.cartItem,
                      {
                        transform: [
                          {
                            scale: cartItemAnimation.interpolate({
                              inputRange: [0, 1],
                              outputRange: [0.8, 1],
                            }),
                          },
                        ],
                      },
                    ]}
                  >
                    <View style={styles.cartItemInfo}>
                      <Text style={styles.cartItemName}>{item.productName}</Text>
                      <Text style={styles.cartItemPrice}>€{item.unitPrice.toFixed(2)}</Text>
                      <Text style={styles.cartItemTotal}>€{(item.unitPrice * item.quantity).toFixed(2)}</Text>
                    </View>
                    <View style={styles.cartItemControls}>
                      <TouchableOpacity
                        style={[styles.quantityButton, styles.decreaseButton]}
                        onPress={() => handleUpdateQuantity(item.id, item.quantity - 1)}
                      >
                        <Ionicons name="remove" size={16} color="#ffffff" />
                      </TouchableOpacity>
                      <Text style={styles.quantityText}>{item.quantity}</Text>
                      <TouchableOpacity
                        style={[styles.quantityButton, styles.increaseButton]}
                        onPress={() => handleUpdateQuantity(item.id, item.quantity + 1)}
                      >
                        <Ionicons name="add" size={16} color="#ffffff" />
                      </TouchableOpacity>
                      <TouchableOpacity
                        style={[styles.quantityButton, styles.deleteButton]}
                        onPress={() => handleRemoveFromCart(item.id)}
                      >
                        <Ionicons name="trash-outline" size={16} color="#ffffff" />
                      </TouchableOpacity>
                    </View>
                  </Animated.View>
                ))}
              </ScrollView>
            ) : (
              <View style={styles.emptyCart}>
                <Ionicons name="cart-outline" size={80} color="#94a3b8" />
                <Text style={styles.emptyCartText}>Sepetiniz boş!</Text>
                <Text style={styles.emptyCartSubtext}>Hızlı bir ürün seçin ve kasaya ekleyin.</Text>
                <Text style={styles.emptyCartMotivation}>🚀 Hızlı satış yapmaya hazır!</Text>
              </View>
            )}
          </View>

          {/* Toplam Fiyat */}
          <View style={styles.totalSection}>
            <Text style={styles.totalLabel}>Toplam:</Text>
            <Text style={styles.totalAmount}>€{cashRegister.calculateTotal().toFixed(2)}</Text>
          </View>

          {/* Ana Butonlar */}
          <View style={styles.actionButtons}>
            <TouchableOpacity
              style={[styles.actionButton, styles.clearButton]}
              onPress={cashRegister.clearCart}
            >
              <Text style={styles.actionButtonText}>Temizle</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={[styles.actionButton, styles.paymentButton]}
              onPress={() => setShowAdvancedPayment(true)}
            >
              <Text style={styles.actionButtonText}>Ödeme</Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>

      {/* Modaller - Sadece Gerekli Olanlar */}
      <AdvancedPaymentOptions
        visible={showAdvancedPayment}
        onClose={() => setShowAdvancedPayment(false)}
        onPaymentMethodSelect={handlePaymentMethodSelect}
        totalAmount={cashRegister.calculateTotal()}
      />

      <AdvancedInvoiceModal
        visible={showAdvancedInvoice}
        onClose={() => setShowAdvancedInvoice(false)}
        onInvoiceCreate={handleInvoiceCreate}
        cart={cashRegister.cart}
        totalAmount={cashRegister.calculateTotal()}
        taxAmount={cashRegister.calculateTax()}
        subtotal={cashRegister.calculateTotal()}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f8fafc',
  },
  header: {
    backgroundColor: '#ffffff',
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#e2e8f0',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 4,
    elevation: 2,
  },
  headerTitle: {
    fontSize: 28,
    fontWeight: '800',
    color: '#1e293b',
    textAlign: 'center',
  },
  headerSubtitle: {
    fontSize: 16,
    color: '#64748b',
    textAlign: 'center',
    marginTop: 4,
  },
  mainContent: {
    flex: 1,
    flexDirection: 'row',
  },
  productsPanel: {
    flex: 1,
    backgroundColor: '#ffffff',
    margin: 8,
    borderRadius: 16,
    padding: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.08,
    shadowRadius: 8,
    elevation: 4,
  },
  cartPanel: {
    flex: 1,
    backgroundColor: '#ffffff',
    margin: 8,
    borderRadius: 16,
    padding: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.08,
    shadowRadius: 8,
    elevation: 4,
  },
  panelTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: '#1e293b',
    marginBottom: 16,
    textAlign: 'center',
  },
  cartContent: {
    flex: 1,
  },
  cartItemsList: {
    flex: 1,
  },
  cartItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 16,
    backgroundColor: '#f8fafc',
    borderRadius: 16,
    marginBottom: 12,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 4,
    elevation: 2,
  },
  cartItemInfo: {
    flex: 1,
    marginRight: 12,
  },
  cartItemName: {
    fontSize: 16,
    fontWeight: '600',
    color: '#1e293b',
    marginBottom: 4,
  },
  cartItemPrice: {
    fontSize: 14,
    color: '#64748b',
    marginBottom: 2,
  },
  cartItemTotal: {
    fontSize: 16,
    fontWeight: '700',
    color: '#059669',
  },
  cartItemControls: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  quantityButton: {
    width: 36,
    height: 36,
    borderRadius: 18,
    justifyContent: 'center',
    alignItems: 'center',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 3,
    elevation: 3,
  },
  decreaseButton: {
    backgroundColor: '#f59e0b',
  },
  increaseButton: {
    backgroundColor: '#059669',
  },
  deleteButton: {
    backgroundColor: '#ef4444',
  },
  quantityButtonText: {
    color: '#ffffff',
    fontSize: 18,
    fontWeight: '700',
  },
  quantityText: {
    fontSize: 18,
    fontWeight: '700',
    color: '#1e293b',
    marginHorizontal: 12,
    minWidth: 24,
    textAlign: 'center',
  },
  emptyCart: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingHorizontal: 20,
  },
  emptyCartText: {
    fontSize: 20,
    color: '#64748b',
    fontWeight: '600',
    marginTop: 16,
    marginBottom: 8,
  },
  emptyCartSubtext: {
    fontSize: 16,
    color: '#94a3b8',
    textAlign: 'center',
    marginBottom: 12,
  },
  emptyCartMotivation: {
    fontSize: 18,
    color: '#059669',
    fontWeight: '700',
    textAlign: 'center',
  },
  totalSection: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 16,
    backgroundColor: '#f1f5f9',
    borderRadius: 12,
    marginTop: 16,
  },
  totalLabel: {
    fontSize: 18,
    fontWeight: '600',
    color: '#1e293b',
  },
  totalAmount: {
    fontSize: 24,
    fontWeight: '800',
    color: '#059669',
  },
  actionButtons: {
    flexDirection: 'row',
    marginTop: 16,
    gap: 12,
  },
  actionButton: {
    flex: 1,
    padding: 16,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
  },
  clearButton: {
    backgroundColor: '#ef4444',
  },
  paymentButton: {
    backgroundColor: '#059669',
  },
  actionButtonText: {
    color: '#ffffff',
    fontSize: 18,
    fontWeight: '700',
  },
}); 