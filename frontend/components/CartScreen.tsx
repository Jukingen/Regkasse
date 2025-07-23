// Türkçe Açıklama: Bu component, dokunmatik dostu, sade ve büyük butonlu bir sepet/kasa ekranı sunar. Tüm hesaplamalar backend'den gelir, frontend'de tekrar hesaplanmaz.
import { Ionicons } from '@expo/vector-icons';
import React, { useState, useRef, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  Alert,
  Animated,
  Vibration,
  Modal,
  ActivityIndicator,
  TextInput,
  FlatList,
} from 'react-native';

import { useApiCart } from '../hooks/useApiCart';
import CartFooter from './CartFooter';
import { useAppState } from '../contexts/AppStateContext';
import PaymentScreen from './PaymentScreen';
import SplitBillSection from './SplitBillSection';
import ErrorModal from './ErrorModal';
import EmailInvoice from './EmailInvoice';
import WaiterShortcuts from './WaiterShortcuts';
import { useTranslation } from 'react-i18next';

// Sepet item'ını CartScreen için uygun tipe dönüştürür
function mapCartItem(item: any) {
  return {
    id: item.id,
    product: {
      id: item.productId,
      name: item.name,
      price: item.price,
      stockQuantity: 9999, // Stok backend'den gelmiyorsa dummy
      taxType: item.taxType,
    },
    quantity: item.quantity,
    unitPrice: item.price,
    taxRate: 0,
    discountAmount: 0,
    taxAmount: 0,
    totalAmount: item.price * item.quantity,
    isModified: false,
    notes: item.notes, // Yeni eklenen alan
  };
}

// Ürün kutusu: performans için React.memo ile optimize
const CartItemBox = React.memo(({ mapped, onRemove, onQtyChange, processing }: any) => (
  <View style={styles.itemBox}>
    <View style={styles.itemHeader}>
      <Text style={styles.itemName}>{mapped.product.name}</Text>
      <TouchableOpacity
        onPress={() => onRemove(mapped.id)}
        disabled={processing}
        style={styles.removeBtn}
      >
        <Ionicons name="trash" size={22} color="#fff" />
      </TouchableOpacity>
    </View>
    <View style={styles.itemDetailsRow}>
      <Text style={styles.detailText}>{t('cart.quantity', 'Miktar')}: <Text style={styles.bold}>{mapped.quantity}</Text></Text>
      <Text style={styles.detailText}>{t('cart.unitPrice', 'Birim')}: <Text style={styles.bold}>{mapped.unitPrice.toFixed(2)} €</Text></Text>
      <Text style={styles.detailText}>{t('cart.totalAmount', 'Toplam')}: <Text style={styles.bold}>{mapped.totalAmount.toFixed(2)} €</Text></Text>
    </View>
    {mapped.notes && (
      <Text style={styles.extraOption}>{t('cart.notes', 'Not')}: {mapped.notes}</Text>
    )}
    <View style={styles.quantityRow}>
      <TouchableOpacity
        onPress={() => onQtyChange(mapped.id, mapped.quantity - 1)}
        disabled={processing}
        style={styles.qtyBtn}
      >
        <Ionicons name="remove-circle" size={28} color="#e74c3c" />
      </TouchableOpacity>
      <Text style={styles.quantity}>{mapped.quantity}</Text>
      <TouchableOpacity
        onPress={() => onQtyChange(mapped.id, mapped.quantity + 1)}
        disabled={processing}
        style={styles.qtyBtn}
      >
        <Ionicons name="add-circle" size={28} color="#27ae60" />
      </TouchableOpacity>
    </View>
  </View>
));

// Tablo başlığı componenti
const CartTableHeader = () => (
  <View style={styles.tableHeaderRow}>
    <Text style={styles.tableHeaderColName}>{t('cart.product', 'Ürün')}</Text>
    <Text style={styles.tableHeaderColQty}>{t('cart.quantity', 'Miktar')}</Text>
    <Text style={styles.tableHeaderColPrice}>{t('cart.unitPrice', 'Birim Fiyat')}</Text>
    <Text style={styles.tableHeaderColTotal}>{t('cart.totalAmount', 'Toplam')}</Text>
    <Text style={styles.tableHeaderColDelete}></Text>
  </View>
);

const COLORS = {
  background: '#F7F8FA',
  card: '#FFFFFF',
  accent: '#1976D2', // Parlak mavi
  accentSoft: '#E3F2FD',
  danger: '#E53935',
  dangerSoft: '#FFEBEE',
  success: '#43A047',
  successSoft: '#E8F5E9',
  text: '#222',
  textSoft: '#666',
  border: '#E0E0E0',
  tableHeader: '#F1F6FB',
  tableRow: '#FFFFFF',
  tableRowAlt: '#F7F8FA',
};

const CartScreen: React.FC = () => {
  const { t } = useTranslation();
  // Sepet işlemleri için hook'u kullan
  const {
    cart,
    loading,
    error,
    addItem,
    removeItem,
    updateQuantity,
    clearCart,
    applyCoupon,
    removeCoupon,
    // applyCoupon, removeCoupon gibi fonksiyonlar backend'den eklenmeli
  } = useApiCart();

  const { addNotification } = useAppState();

  // Kupon kodu state'i ve uygulama durumu
  const [couponCode, setCouponCode] = useState('');
  const [couponError, setCouponError] = useState<string | null>(null);
  const [couponSuccess, setCouponSuccess] = useState<string | null>(null);
  const [isApplyingCoupon, setIsApplyingCoupon] = useState(false);

  const [showConfirmation, setShowConfirmation] = useState(false);
  const [processingItem, setProcessingItem] = useState<string | null>(null);
  const totalAnimation = useRef(new Animated.Value(1)).current;
  const [networkError, setNetworkError] = useState<string | null>(null); // Ağ hatası için state
  const [retryAction, setRetryAction] = useState<(() => void) | null>(null); // Tekrar denenecek fonksiyon
  const [showPayment, setShowPayment] = useState(false);
  const [lastPayments, setLastPayments] = useState<any>(null); // Son ödeme verisi (isteğe bağlı)
  const [splitData, setSplitData] = useState<any[]>([]); // Split bill verisi
  const [errorModal, setErrorModal] = useState({ visible: false, code: '', message: '' });
  const [emailInvoice, setEmailInvoice] = useState<{ visible: boolean; data: any }>({ visible: false, data: null });
  const [tip, setTip] = useState(0); // Bahşiş tutarı
  const [addingItemId, setAddingItemId] = useState<string | null>(null); // Sadece eklenen ürünü disable et
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);

  // Sepet değiştiğinde ilk ürünü otomatik seçili yap
  useEffect(() => {
    if (cart && cart.items.length > 0) {
      setSelectedItemId(prev => prev && cart.items.some(i => i.id === prev) ? prev : cart.items[0].id);
    } else {
      setSelectedItemId(null);
    }
  }, [cart]);

  // Snackbar için state
  const [showSnackbar, setShowSnackbar] = useState(false);
  const [snackbarMsg, setSnackbarMsg] = useState('');

  // Sepet boşsa gösterilecek mesaj
  if (!cart || cart.items.length === 0) {
    return (
      <View style={styles.emptyBox}>
        <Text style={styles.emptyEmoji}>🛒</Text>
        <Text style={styles.emptyText}>{t('cart.empty', 'Sepetiniz boş!')}</Text>
        <Text style={styles.emptyMotivation}>{t('cart.emptyMotivation', 'Hadi alışverişe başlayın ve favori ürünlerinizi ekleyin.')}</Text>
      </View>
    );
  }

  // Miktar güncelleme
  const handleQuantityChange = async (itemId: string, newQuantity: number) => {
    if (processingItem) return;
    setProcessingItem(itemId);
    try {
      if (newQuantity <= 0) {
        Alert.alert(
          t('cart.removeConfirmTitle', 'Ürünü Kaldır'),
          t('cart.removeConfirmMessage', 'Bu ürünü sepetten kaldırmak istediğinizden emin misiniz?'),
          [
            { text: t('cart.cancel', 'İptal'), style: 'cancel', onPress: () => setProcessingItem(null) },
            {
              text: t('cart.remove', 'Kaldır'),
              style: 'destructive',
              onPress: async () => {
                await removeItem(itemId);
                Vibration.vibrate(50);
                setProcessingItem(null);
              },
            },
          ]
        );
      } else {
        await updateQuantity(itemId, newQuantity);
        Vibration.vibrate(30);
        Animated.sequence([
          Animated.timing(totalAnimation, {
            toValue: 1.1,
            duration: 150,
            useNativeDriver: true,
          }),
          Animated.timing(totalAnimation, {
            toValue: 1,
            duration: 150,
            useNativeDriver: true,
          }),
        ]).start();
        setProcessingItem(null);
      }
    } catch (error) {
      Alert.alert(t('cart.connectionError', 'Bağlantı hatası! Tekrar deneyin.'));
      setProcessingItem(null);
    }
  };

  // Sepete ürün ekleme işlemini sarmalayan fonksiyon
  const handleAddItem = async (productId: string, quantity: number = 1) => {
    setAddingItemId(productId);
    try {
      await addItem(productId, quantity);
      setAddingItemId(null);
    } catch (e) {
      setAddingItemId(null);
    }
  };

  // Sepeti temizleme
  const handleClearCart = () => {
    Alert.alert(
      t('cart.clearCartTitle', 'Sepeti Temizle'),
      t('cart.clearCartMessage', 'Tüm ürünler sepetten kaldırılacak. Bu işlem geri alınamaz.'),
      [
        { text: t('cart.cancel', 'İptal'), style: 'cancel' },
        {
          text: t('cart.clear', 'Temizle'),
          style: 'destructive',
          onPress: async () => {
            try {
              await clearCart();
              Vibration.vibrate(10);
            } catch (error) {
              Alert.alert(t('cart.connectionError', 'Bağlantı hatası! Tekrar deneyin.'));
            }
          },
        },
      ]
    );
  };

  // Ödeme öncesi doğrulama
  const handleCheckout = () => {
    if (!cart || cart.items.length === 0) {
      Alert.alert(t('cart.emptyCartWarning', 'Sepetiniz boş!'));
      return;
    }
    setShowConfirmation(true);
  };

  const confirmCheckout = () => {
    setShowConfirmation(false);
    setShowPayment(true); // Ödeme ekranını aç
  };

  // PaymentScreen onConfirm callback
  const handlePaymentConfirm = async (payments: any) => {
    setShowPayment(false);
    setLastPayments(payments); // Son ödemeyi kaydet (isteğe bağlı)
    // TODO: Backend'e ödeme isteği gönder, fatura oluştur, vs.
    Alert.alert(t('cart.paymentSuccess', 'Ödeme alındı'), `${t('cart.paymentDetails', 'Ödeme detayları')}: ${JSON.stringify(payments, null, 2)}\n${t('cart.split', 'Split')}: ${JSON.stringify(splitData, null, 2)}`);
  };

  // Hızlı işlem kısayolları
  const handleQuickAction = async (action: string, amount?: number) => {
    switch (action) {
      case 'full_cash':
        // Tam nakit ödeme
        await processPayment('cash', totalAmount);
        break;
      case 'full_card':
        // Tam kart ödeme
        await processPayment('card', totalAmount);
        break;
      case 'split_half':
        // Yarı-yarı böl
        setSplitData([
          { name: 'Kişi 1', amount: amount, method: 'cash' },
          { name: 'Kişi 2', amount: amount, method: 'card' }
        ]);
        break;
      case 'add_tip':
        // Bahşiş ekle
        setTip(amount || 0);
        break;
      case 'print_receipt':
        // Fiş yazdır
        await printReceipt();
        break;
      case 'email_receipt':
        // E-posta gönder
        setEmailInvoice({
          visible: true,
          data: {
            id: 'temp-invoice-id',
            totalAmount: totalAmount,
            receiptNumber: 'TEMP-001'
          }
        });
        break;
    }
  };

  // Ödeme işlemi (placeholder)
  const processPayment = async (method: string, amount: number) => {
    try {
      // Backend'e ödeme isteği gönder
      console.log(`Processing ${method} payment for ${amount}`);
      // Başarılı ödeme simülasyonu
      Alert.alert(t('cart.paymentSuccess', 'Başarılı'), `${method} ${t('cart.paymentSuccessMessage', 'ödeme alındı')}: ${amount.toFixed(2)} €`);
    } catch (error) {
      setErrorModal({
        visible: true,
        code: 'PAYMENT_ERROR',
        message: t('cart.paymentError', 'Ödeme işlemi başarısız')
      });
    }
  };

  // Fiş yazdırma (placeholder)
  const printReceipt = async () => {
    try {
      console.log('Printing receipt...');
      Alert.alert(t('cart.printSuccess', 'Başarılı'), t('cart.printSuccessMessage', 'Fiş yazdırılıyor...'));
    } catch (error) {
      setErrorModal({
        visible: true,
        code: 'PRINT_ERROR',
        message: t('cart.printError', 'Yazdırma hatası')
      });
    }
  };

  // E-posta gönderimi
  const handleEmailSend = async (email: string) => {
    try {
      // Backend'e e-posta gönderim isteği
      console.log(`Sending invoice to ${email}`);
      return true; // Başarılı simülasyonu
    } catch (error) {
      return false;
    }
  };

  /**
   * Kupon kodunu backend ile uygular.
   */
  const handleApplyCoupon = async () => {
    setCouponError(null);
    setCouponSuccess(null);
    if (!couponCode || couponCode.length < 3) {
      setCouponError(t('cart.invalidCouponCode', 'Lütfen geçerli bir kod girin.'));
      return;
    }
    setIsApplyingCoupon(true);
    try {
      await applyCoupon(couponCode);
      setCouponSuccess(t('cart.couponApplied', 'Kupon başarıyla uygulandı!'));
      setCouponError(null);
    } catch (e: any) {
      setCouponError(t('cart.invalidOrExpiredCoupon', 'Kupon kodu geçersiz veya süresi dolmuş.'));
      setCouponSuccess(null);
    } finally {
      setIsApplyingCoupon(false);
    }
  };

  /**
   * Kupon kodunu backend ile kaldırır.
   */
  const handleRemoveCoupon = async () => {
    setCouponError(null);
    setCouponSuccess(null);
    setCouponCode('');
    try {
      await removeCoupon();
      setCouponSuccess(t('cart.couponRemoved', 'Kupon kaldırıldı.'));
    } catch (e: any) {
      setCouponError(t('cart.couponRemoveError', 'Kupon kodu kaldırılamadı.'));
    }
  };

  // Ağ hatası ile sarmalayan yardımcı fonksiyon
  const withNetworkError = (fn: (...args: any[]) => Promise<void>, retryLabel: string) => async (...args: any[]) => {
    try {
      await fn(...args);
      setNetworkError(null);
      setRetryAction(null);
    } catch (e: any) {
      setNetworkError(t('cart.networkError', 'Netzwerkfehler! Bitte überprüfen Sie Ihre Verbindung.'));
      setRetryAction(() => async () => await fn(...args));
    }
  };

  // Sepet işlemlerini ağ hatası ile sarmala
  const safeHandleQuantityChange = withNetworkError(handleQuantityChange, t('cart.updateQuantity', 'Miktar güncelle'));
  const safeHandleClearCart = withNetworkError(async () => { await handleClearCart(); }, t('cart.clearCart', 'Sepeti temizle'));
  const safeHandleApplyCoupon = withNetworkError(handleApplyCoupon, t('cart.applyCoupon', 'Kupon uygula'));
  const safeHandleRemoveCoupon = withNetworkError(handleRemoveCoupon, t('cart.removeCoupon', 'Kupon kaldır'));

  // Sepet toplamı ve alt bilgi değerleri
  const subtotal = cart?.total ?? 0;
  const vat = cart?.vat ?? 0;
  const serviceFee = Math.round(subtotal * 0.1 * 100) / 100; // %10 servis bedeli örnek
  const totalAmount = cart?.grandTotal ?? 0;
  const discount = cart?.discount ?? 0;

  // Modern kart tasarımı ile ürün kutusu
  const renderCartCard = ({ item }: any) => {
    const mapped = mapCartItem(item);
    const isSelected = selectedItemId === mapped.id;
    return (
      <TouchableOpacity
        style={[styles.cardBox, isSelected && styles.cardBoxSelected]}
        onPress={() => setSelectedItemId(mapped.id)}
        activeOpacity={0.85}
      >
        <View style={styles.cardHeader}>
          <Text style={styles.cardName}>{mapped.product.name}</Text>
          {isSelected && (
            <Ionicons name="checkmark-circle" size={22} color={COLORS.accent} style={{ marginLeft: 4 }} />
          )}
        </View>
        <View style={styles.cardDetailsRow}>
          <Text style={styles.cardDetail}>{t('cart.quantity', 'Miktar')}: <Text style={styles.bold}>{mapped.quantity}</Text></Text>
          <Text style={styles.cardDetail}>{t('cart.unitPrice', 'Birim')}: <Text style={styles.bold}>{mapped.unitPrice.toFixed(2)} €</Text></Text>
        </View>
        <View style={styles.cardDetailsRow}>
          <Text style={styles.cardDetail}>{t('cart.totalAmount', 'Toplam')}: <Text style={styles.bold}>{mapped.totalAmount.toFixed(2)} €</Text></Text>
        </View>
        {mapped.notes && (
          <Text style={styles.cardNote}>{t('cart.notes', 'Not')}: {mapped.notes}</Text>
        )}
        <View style={styles.cardActionsRow}>
          <TouchableOpacity
            style={[styles.cardQtyBtn, isSelected && styles.cardQtyBtnActive]}
            onPress={() => safeHandleQuantityChange(mapped.id, mapped.quantity - 1)}
            disabled={processingItem === mapped.id}
            accessibilityLabel={t('cart.decreaseQuantity', 'Miktarı azalt')}
          >
            <Ionicons name="remove-circle-outline" size={26} color={COLORS.danger} />
          </TouchableOpacity>
          <Text style={styles.cardQtyText}>{mapped.quantity}</Text>
          <TouchableOpacity
            style={[styles.cardQtyBtn, isSelected && styles.cardQtyBtnActive]}
            onPress={() => safeHandleQuantityChange(mapped.id, mapped.quantity + 1)}
            disabled={processingItem === mapped.id}
            accessibilityLabel={t('cart.increaseQuantity', 'Miktarı arttır')}
          >
            <Ionicons name="add-circle-outline" size={26} color={COLORS.success} />
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.cardDeleteBtn, isSelected && styles.cardDeleteBtnActive]}
            onPress={() => safeHandleQuantityChange(mapped.id, 0)}
            disabled={processingItem === mapped.id}
            accessibilityLabel={t('cart.removeItem', 'Ürünü sil')}
          >
            <Ionicons name="trash-outline" size={24} color={COLORS.danger} />
          </TouchableOpacity>
        </View>
      </TouchableOpacity>
    );
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>{t('cart.cartTitle', 'Sepet')}</Text>
      {error && <Text style={styles.errorText}>{error}</Text>}
      {/* Modern kart grid/listesi */}
      <FlatList
        data={cart.items}
        renderItem={renderCartCard}
        keyExtractor={item => item.id}
        contentContainerStyle={{ paddingBottom: 8 }}
        style={{ flex: 1 }}
        initialNumToRender={8}
        maxToRenderPerBatch={12}
        windowSize={7}
        removeClippedSubviews
        extraData={selectedItemId}
      />
      {/* Kupon/Promosyon kodu alanı */}
      <View style={styles.couponBox}>
        <Text style={styles.couponLabel}>{t('cart.discountCoupon', 'İndirim / Kupon Kodu')}</Text>
        <View style={styles.couponRow}>
          <TextInput
            style={styles.couponInput}
            placeholder={t('cart.couponCodePlaceholder', 'Kupon kodu girin')}
            value={couponCode}
            onChangeText={setCouponCode}
            editable={!isApplyingCoupon}
            autoCapitalize="characters"
          />
          <TouchableOpacity
            style={styles.couponBtn}
            onPress={safeHandleApplyCoupon}
            disabled={isApplyingCoupon || !couponCode}
          >
            <Text style={styles.couponBtnText}>{t('cart.applyCouponBtn', 'Uygula')}</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={styles.couponRemoveBtn}
            onPress={safeHandleRemoveCoupon}
            disabled={isApplyingCoupon || !couponCode}
          >
            <Ionicons name="close-circle" size={22} color="#d32f2f" />
          </TouchableOpacity>
        </View>
        {couponError && <Text style={styles.couponError}>{couponError}</Text>}
        {couponSuccess && <Text style={styles.couponSuccess}>{couponSuccess}</Text>}
      </View>
      {/* Sepet özeti ve toplamlar */}
      <View style={styles.summaryRow}>
        {discount > 0 && (
          <Text style={styles.discountText}>{t('cart.discount', 'İndirim')}: -{discount.toFixed(2)} €</Text>
        )}
        <Animated.Text style={[styles.totalText, { transform: [{ scale: totalAnimation }] }]}>{t('cart.total', 'Toplam')}: {totalAmount.toFixed(2)} €</Animated.Text>
      </View>
      {/* Sepet alt bilgi (footer) */}
      <CartFooter subtotal={subtotal} vat={vat} serviceFee={serviceFee} grandTotal={totalAmount} />
      {/* Split bill bölümü */}
      <SplitBillSection totalAmount={totalAmount} onSplitChange={setSplitData} />
      {/* Garson kısayolları */}
      <WaiterShortcuts
        totalAmount={totalAmount}
        onQuickAction={handleQuickAction}
      />
      <View style={[styles.buttonRow, { marginTop: 4 }]}> {/* marginTop küçültüldü */}
        <TouchableOpacity style={[styles.clearButton, { minHeight: 0, minWidth: 0, paddingVertical: 0, paddingHorizontal: 0 }]} onPress={safeHandleClearCart}>
          <Text style={styles.buttonText}>{t('cart.clearCartButton', 'Sepeti Temizle')}</Text>
        </TouchableOpacity>
        <TouchableOpacity style={[styles.checkoutButton, { minHeight: 0, minWidth: 0, paddingVertical: 0, paddingHorizontal: 0 }]} onPress={handleCheckout}>
          <Text style={styles.buttonText}>{t('cart.checkoutButton', 'Ödeme Yap')}</Text>
        </TouchableOpacity>
      </View>
      {/* PaymentScreen modal entegrasyonu */}
      <Modal visible={showPayment} transparent animationType="slide">
        <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: 'rgba(0,0,0,0.2)' }}>
          <PaymentScreen
            totalAmount={totalAmount}
            onConfirm={handlePaymentConfirm}
            onCancel={() => setShowPayment(false)}
          />
        </View>
      </Modal>
      {/* Hata modalı */}
      <ErrorModal
        visible={errorModal.visible}
        errorCode={errorModal.code}
        errorMessage={errorModal.message}
        onRetry={() => {
          setErrorModal({ visible: false, code: '', message: '' });
          // Ödeme işlemini tekrar dene
        }}
        onCancel={() => {
          setErrorModal({ visible: false, code: '', message: '' });
        }}
      />
      {/* E-posta fatura modalı */}
      <EmailInvoice
        visible={emailInvoice.visible}
        invoiceData={emailInvoice.data}
        onSend={handleEmailSend}
        onClose={() => setEmailInvoice({ visible: false, data: null })}
      />
      {/* Ağ bağlantısı hatası modalı */}
      <Modal visible={!!networkError} transparent animationType="fade">
        <View style={styles.networkModalContainer}>
          <View style={styles.networkModalContent}>
            <Text style={styles.networkModalTitle}>{t('cart.networkErrorTitle', 'Verbindungsfehler')}</Text>
            <Text style={styles.networkModalText}>{networkError}</Text>
            <View style={styles.networkModalBtnRow}>
              <TouchableOpacity
                style={styles.networkModalRetryBtn}
                onPress={async () => {
                  if (retryAction) await retryAction();
                  setNetworkError(null);
                }}
              >
                <Text style={styles.networkModalBtnText}>{t('cart.retry', 'Erneut versuchen')}</Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={styles.networkModalCancelBtn}
                onPress={() => setNetworkError(null)}
              >
                <Text style={styles.networkModalBtnText}>{t('cart.cancel', 'Abbrechen')}</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>
      <Modal visible={showConfirmation} transparent animationType="slide">
        <View style={styles.modalContainer}>
          <View style={styles.modalContent}>
            <Text style={styles.modalTitle}>{t('cart.checkoutConfirmationTitle', 'Ödeme Onayı')}</Text>
            <Text style={styles.modalTotal}>{t('cart.checkoutConfirmationTotal', 'Toplam')}: {totalAmount.toFixed(2)} €</Text>
            <View style={styles.buttonRow}>
              <TouchableOpacity onPress={() => setShowConfirmation(false)} style={styles.cancelBtn}>
                <Text style={styles.buttonText}>{t('cart.cancel', 'İptal')}</Text>
              </TouchableOpacity>
              <TouchableOpacity onPress={confirmCheckout} style={styles.confirmBtn}>
                <Text style={styles.buttonText}>{t('cart.confirmCheckout', 'Onayla')}</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>
      {/* Snackbar/Toast mesajı */}
      {showSnackbar && (
        <View style={styles.snackbar}>
          <Text style={styles.snackbarText}>{snackbarMsg}</Text>
        </View>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  container: { flex: 1, padding: 8, backgroundColor: COLORS.background },
  title: { fontSize: 20, fontWeight: 'bold', marginBottom: 8, textAlign: 'center', color: COLORS.accent },
  // Ürün kutusu stilleri
  itemBox: { backgroundColor: '#f8f8f8', borderRadius: 10, padding: 8, marginBottom: 8, elevation: 1 },
  itemHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 2 },
  itemName: { fontSize: 14, fontWeight: '600', flex: 1 },
  itemDetailsRow: { flexDirection: 'row', justifyContent: 'space-between', marginBottom: 2 },
  detailText: { fontSize: 12, color: '#444' },
  bold: { fontWeight: 'bold', color: COLORS.text }, // tek tanım
  extraOption: { fontSize: 11, color: '#888', marginTop: 1, fontStyle: 'italic' },
  quantityRow: { flexDirection: 'row', alignItems: 'center', marginTop: 2 },
  qtyBtn: { paddingHorizontal: 4 },
  quantity: { marginHorizontal: 4, fontSize: 14, fontWeight: 'bold' },
  price: { flex: 1, textAlign: 'right', fontSize: 14, fontWeight: '600' },
  removeBtn: { backgroundColor: '#d32f2f', borderRadius: 8, padding: 6, marginLeft: 4 },
  // Kupon alanı stilleri
  couponBox: { backgroundColor: COLORS.card, borderRadius: 8, padding: 6, marginTop: 4, marginBottom: 4, borderWidth: 1, borderColor: COLORS.border },
  couponLabel: { fontSize: 12, fontWeight: 'bold', marginBottom: 2, color: COLORS.text },
  couponRow: { flexDirection: 'row', alignItems: 'center' },
  couponInput: { flex: 1, backgroundColor: COLORS.background, borderRadius: 6, padding: 6, fontSize: 13, borderWidth: 1, borderColor: COLORS.border, marginRight: 4 },
  couponBtn: { backgroundColor: COLORS.accent, borderRadius: 6, paddingHorizontal: 8, paddingVertical: 6 },
  couponBtnText: { color: '#fff', fontWeight: 'bold', fontSize: 12 },
  couponRemoveBtn: { marginLeft: 2 },
  couponError: { color: COLORS.danger, fontSize: 12, marginTop: 2 },
  couponSuccess: { color: COLORS.success, fontSize: 12, marginTop: 2 },
  // Sepet özeti ve genel stiller
  summaryRow: { marginTop: 8, marginBottom: 4, alignItems: 'flex-end' },
  totalText: { fontSize: 16, fontWeight: 'bold', color: COLORS.accent },
  discountText: { fontSize: 13, fontWeight: 'bold', color: COLORS.danger, marginBottom: 1 },
  buttonRow: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 4 }, // marginTop küçültüldü
  clearButton: { flex: 1, backgroundColor: COLORS.danger, marginRight: 2, borderRadius: 8, padding: 10, alignItems: 'center' },
  checkoutButton: { flex: 1, backgroundColor: COLORS.accent, marginLeft: 2, borderRadius: 8, padding: 10, alignItems: 'center' },
  buttonText: { color: '#fff', fontSize: 14, fontWeight: 'bold' },
  modalContainer: { flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: 'rgba(0,0,0,0.3)' },
  modalContent: { backgroundColor: '#fff', padding: 16, borderRadius: 12, alignItems: 'center' },
  modalTitle: { fontSize: 16, fontWeight: 'bold', marginBottom: 6 },
  modalTotal: { fontSize: 14, fontWeight: '600', marginBottom: 8 },
  cancelBtn: { backgroundColor: '#eee', borderRadius: 8, padding: 8, marginRight: 4 },
  confirmBtn: { backgroundColor: '#27ae60', borderRadius: 8, padding: 8, marginLeft: 4 },
  errorText: { color: '#d32f2f', fontSize: 12, textAlign: 'center', marginBottom: 4 },
  emptyBox: { flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: COLORS.accentSoft },
  emptyEmoji: { fontSize: 54, marginBottom: 8 },
  emptyText: { color: COLORS.accent, fontSize: 18, fontWeight: 'bold', marginTop: 8 },
  emptyMotivation: { color: COLORS.textSoft, fontSize: 14, marginTop: 4, textAlign: 'center', maxWidth: 260 },
  // Ağ hatası modalı stilleri
  networkModalContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: 'rgba(0,0,0,0.3)',
  },
  networkModalContent: {
    backgroundColor: '#fff',
    padding: 12,
    borderRadius: 12,
    alignItems: 'center',
    width: 220,
    elevation: 4,
  },
  networkModalTitle: {
    fontSize: 14,
    fontWeight: 'bold',
    color: '#d32f2f',
    marginBottom: 4,
  },
  networkModalText: {
    fontSize: 12,
    color: '#444',
    marginBottom: 8,
    textAlign: 'center',
  },
  networkModalBtnRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    width: '100%',
  },
  networkModalRetryBtn: {
    backgroundColor: '#1976d2',
    borderRadius: 8,
    padding: 8,
    flex: 1,
    alignItems: 'center',
    marginRight: 4,
  },
  networkModalCancelBtn: {
    backgroundColor: '#eee',
    borderRadius: 8,
    padding: 8,
    flex: 1,
    alignItems: 'center',
    marginLeft: 4,
  },
  networkModalBtnText: {
    color: '#fff',
    fontWeight: 'bold',
    fontSize: 12,
  },
  // Tablo stilleri
  tableHeaderRow: { flexDirection: 'row', borderBottomWidth: 1, borderColor: COLORS.border, paddingBottom: 4, marginBottom: 4, backgroundColor: COLORS.tableHeader },
  tableHeaderColName: { flex: 2, textAlign: 'left', fontWeight: 'bold', fontSize: 13, color: COLORS.text },
  tableHeaderColQty: { flex: 1.2, textAlign: 'center', fontWeight: 'bold', fontSize: 13, color: COLORS.text },
  tableHeaderColPrice: { flex: 1.2, textAlign: 'center', fontWeight: 'bold', fontSize: 13, color: COLORS.text },
  tableHeaderColTotal: { flex: 1.2, textAlign: 'center', fontWeight: 'bold', fontSize: 13, color: COLORS.text },
  tableHeaderColDelete: { flex: 0.7 },
  tableRow: { flexDirection: 'row', alignItems: 'center', paddingVertical: 8, borderBottomWidth: 0.5, borderColor: COLORS.border, backgroundColor: COLORS.tableRow },
  tableColName: { flex: 2, textAlign: 'left', fontSize: 14, color: COLORS.text },
  tableColQty: { flex: 1.2, flexDirection: 'row', alignItems: 'center', justifyContent: 'center' },
  qtyBtnTouch: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: COLORS.accentSoft,
    alignItems: 'center',
    justifyContent: 'center',
    marginHorizontal: 2,
  },
  qtyText: { minWidth: 24, textAlign: 'center', fontSize: 15, color: COLORS.text },
  tableColPrice: { flex: 1.2, textAlign: 'center', fontSize: 14, color: COLORS.text },
  tableColTotal: { flex: 1.2, textAlign: 'center', fontWeight: 'bold', fontSize: 14, color: COLORS.text },
  deleteBtnTouch: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: COLORS.dangerSoft,
    alignItems: 'center',
    justifyContent: 'center',
    marginLeft: 4,
  },
  snackbar: {
    position: 'absolute',
    left: 16,
    right: 16,
    bottom: 24,
    backgroundColor: COLORS.text,
    borderRadius: 8,
    paddingVertical: 12,
    paddingHorizontal: 24,
    alignItems: 'center',
    zIndex: 100,
  },
  snackbarText: {
    color: '#fff',
    fontSize: 15,
    fontWeight: 'bold',
  },
  // Modern kart stilleri
  cardBox: {
    backgroundColor: COLORS.card,
    borderRadius: 16,
    padding: 14,
    marginBottom: 12,
    borderWidth: 2,
    borderColor: 'transparent',
    shadowColor: '#000',
    shadowOpacity: 0.04,
    shadowRadius: 8,
    shadowOffset: { width: 0, height: 2 },
    elevation: 1,
  },
  cardBoxSelected: {
    borderColor: COLORS.accent,
    backgroundColor: COLORS.accentSoft,
  },
  cardHeader: { flexDirection: 'row', alignItems: 'center', marginBottom: 2 },
  cardName: { fontSize: 16, fontWeight: 'bold', color: COLORS.text },
  cardDetailsRow: { flexDirection: 'row', justifyContent: 'space-between', marginBottom: 2 },
  cardDetail: { fontSize: 13, color: COLORS.textSoft },
  bold: { fontWeight: 'bold', color: COLORS.text }, // yeni tanım (kart için)
  cardNote: { fontSize: 12, color: COLORS.textSoft, marginTop: 2, fontStyle: 'italic' },
  cardActionsRow: { flexDirection: 'row', alignItems: 'center', marginTop: 8 },
  cardQtyBtn: {
    width: 38,
    height: 38,
    borderRadius: 19,
    backgroundColor: COLORS.background,
    alignItems: 'center',
    justifyContent: 'center',
    marginHorizontal: 2,
  },
  cardQtyBtnActive: {
    backgroundColor: COLORS.accentSoft,
  },
  cardQtyText: { minWidth: 28, textAlign: 'center', fontSize: 16, fontWeight: 'bold', color: COLORS.text },
  cardDeleteBtn: {
    width: 38,
    height: 38,
    borderRadius: 19,
    backgroundColor: COLORS.dangerSoft,
    alignItems: 'center',
    justifyContent: 'center',
    marginLeft: 8,
  },
  cardDeleteBtnActive: {
    backgroundColor: COLORS.danger,
  },
});

export default CartScreen; 