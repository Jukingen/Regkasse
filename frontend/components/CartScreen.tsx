// Türkçe Açıklama: Bu component, dokunmatik dostu, sade ve büyük butonlu bir sepet/kasa ekranı sunar. Tüm hesaplamalar backend'den gelir, frontend'de tekrar hesaplanmaz.
import { Ionicons } from '@expo/vector-icons';
import React, { useState, useRef } from 'react';
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
      <Text style={styles.detailText}>Miktar: <Text style={styles.bold}>{mapped.quantity}</Text></Text>
      <Text style={styles.detailText}>Birim: <Text style={styles.bold}>{mapped.unitPrice.toFixed(2)} €</Text></Text>
      <Text style={styles.detailText}>Toplam: <Text style={styles.bold}>{mapped.totalAmount.toFixed(2)} €</Text></Text>
    </View>
    {mapped.notes && (
      <Text style={styles.extraOption}>Not: {mapped.notes}</Text>
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

const CartScreen: React.FC = () => {
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

  // Sepet boşsa gösterilecek mesaj
  if (loading) {
    return <ActivityIndicator size="large" color="#1976d2" style={{ marginTop: 40 }} />;
  }
  if (!cart || cart.items.length === 0) {
    return <View style={styles.emptyBox}><Text style={styles.emptyText}>Sepetiniz boş</Text></View>;
  }

  // Miktar güncelleme
  const handleQuantityChange = async (itemId: string, newQuantity: number) => {
    if (processingItem) return;
    setProcessingItem(itemId);
    try {
      if (newQuantity <= 0) {
        Alert.alert(
          'Ürünü Kaldır',
          'Bu ürünü sepetten kaldırmak istediğinizden emin misiniz?',
          [
            { text: 'İptal', style: 'cancel', onPress: () => setProcessingItem(null) },
            {
              text: 'Kaldır',
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
      Alert.alert('Bağlantı hatası! Tekrar deneyin.');
      setProcessingItem(null);
    }
  };

  // Sepete ürün ekleme işlemini sarmalayan fonksiyon
  const handleAddItem = async (productId: string, quantity: number = 1) => {
    try {
      await addItem(productId, quantity);
      // Başarılı eklemede anlık toast/snackbar bildirimi
      addNotification({
        type: 'success',
        title: 'Sepet',
        message: 'Ürün sepete eklendi',
        duration: 2000
      });
    } catch (e) {
      addNotification({
        type: 'error',
        title: 'Sepet',
        message: 'Ürün sepete eklenemedi',
        duration: 3000
      });
    }
  };

  // Sepeti temizleme
  const handleClearCart = () => {
    Alert.alert(
      'Sepeti Temizle',
      'Tüm ürünler sepetten kaldırılacak. Bu işlem geri alınamaz.',
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Temizle',
          style: 'destructive',
          onPress: async () => {
            try {
              await clearCart();
              Vibration.vibrate(10);
            } catch (error) {
              Alert.alert('Bağlantı hatası! Tekrar deneyin.');
            }
          },
        },
      ]
    );
  };

  // Ödeme öncesi doğrulama
  const handleCheckout = () => {
    if (!cart || cart.items.length === 0) {
      Alert.alert('Sepetiniz boş!');
      return;
    }
    setShowConfirmation(true);
  };

  const confirmCheckout = () => {
    setShowConfirmation(false);
    // TODO: Ödeme işlemi başlat
    Alert.alert('Ödeme işlemi başlatılacak!');
  };

  /**
   * Kupon kodunu backend ile uygular.
   */
  const handleApplyCoupon = async () => {
    setCouponError(null);
    setCouponSuccess(null);
    if (!couponCode || couponCode.length < 3) {
      setCouponError('Lütfen geçerli bir kod girin.');
      return;
    }
    setIsApplyingCoupon(true);
    try {
      await applyCoupon(couponCode);
      setCouponSuccess('Kupon başarıyla uygulandı!');
      setCouponError(null);
    } catch (e: any) {
      setCouponError('Kupon kodu geçersiz veya süresi dolmuş.');
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
      setCouponSuccess('Kupon kaldırıldı.');
    } catch (e: any) {
      setCouponError('Kupon kodu kaldırılamadı.');
    }
  };

  // Ağ hatası ile sarmalayan yardımcı fonksiyon
  const withNetworkError = (fn: (...args: any[]) => Promise<void>, retryLabel: string) => async (...args: any[]) => {
    try {
      await fn(...args);
      setNetworkError(null);
      setRetryAction(null);
    } catch (e: any) {
      setNetworkError('Netzwerkfehler! Bitte überprüfen Sie Ihre Verbindung.');
      setRetryAction(() => async () => await fn(...args));
    }
  };

  // Sepet işlemlerini ağ hatası ile sarmala
  const safeHandleQuantityChange = withNetworkError(handleQuantityChange, 'Miktar güncelle');
  const safeHandleClearCart = withNetworkError(handleClearCart, 'Sepeti temizle');
  const safeHandleApplyCoupon = withNetworkError(handleApplyCoupon, 'Kupon uygula');
  const safeHandleRemoveCoupon = withNetworkError(handleRemoveCoupon, 'Kupon kaldır');

  // Sepet toplamı ve alt bilgi değerleri
  const subtotal = cart?.total ?? 0;
  const vat = cart?.vat ?? 0;
  const serviceFee = Math.round(subtotal * 0.1 * 100) / 100; // %10 servis bedeli örnek
  const totalAmount = cart?.grandTotal ?? 0;
  const discount = cart?.discount ?? 0;

  // FlatList için render fonksiyonu
  const renderCartItem = ({ item }: any) => {
    const mapped = mapCartItem(item);
    return (
      <CartItemBox
        mapped={mapped}
        onRemove={safeHandleQuantityChange}
        onQtyChange={safeHandleQuantityChange}
        processing={processingItem === mapped.id}
      />
    );
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Sepet</Text>
      {error && <Text style={styles.errorText}>{error}</Text>}
      {/* FlatList ile performanslı ürün kutuları */}
      <FlatList
        data={cart.items}
        renderItem={renderCartItem}
        keyExtractor={item => item.id}
        contentContainerStyle={{ paddingBottom: 8 }}
        style={{ flex: 1 }}
        initialNumToRender={8}
        maxToRenderPerBatch={12}
        windowSize={7}
        removeClippedSubviews
      />
      {/* Kupon/Promosyon kodu alanı */}
      <View style={styles.couponBox}>
        <Text style={styles.couponLabel}>İndirim / Kupon Kodu</Text>
        <View style={styles.couponRow}>
          <TextInput
            style={styles.couponInput}
            placeholder="Kupon kodu girin"
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
            <Text style={styles.couponBtnText}>Uygula</Text>
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
      {/* Sepet özeti ve indirim gösterimi */}
      <View style={styles.summaryRow}>
        {discount > 0 && (
          <Text style={styles.discountText}>İndirim: -{discount.toFixed(2)} €</Text>
        )}
        <Animated.Text style={[styles.totalText, { transform: [{ scale: totalAnimation }] }]}>Toplam: {totalAmount.toFixed(2)} €</Animated.Text>
      </View>
      {/* Sepet alt bilgi (footer) */}
      <CartFooter subtotal={subtotal} vat={vat} serviceFee={serviceFee} grandTotal={totalAmount} />
      <View style={[styles.buttonRow, { marginTop: 4 }]}> {/* marginTop küçültüldü */}
        <TouchableOpacity style={[styles.clearButton, { minHeight: 0, minWidth: 0, paddingVertical: 0, paddingHorizontal: 0 }]} onPress={safeHandleClearCart}>
          <Text style={styles.buttonText}>Sepeti Temizle</Text>
        </TouchableOpacity>
        <TouchableOpacity style={[styles.checkoutButton, { minHeight: 0, minWidth: 0, paddingVertical: 0, paddingHorizontal: 0 }]} onPress={handleCheckout}>
          <Text style={styles.buttonText}>Ödeme Yap</Text>
        </TouchableOpacity>
      </View>
      {/* Ağ bağlantısı hatası modalı */}
      <Modal visible={!!networkError} transparent animationType="fade">
        <View style={styles.networkModalContainer}>
          <View style={styles.networkModalContent}>
            <Text style={styles.networkModalTitle}>Verbindungsfehler</Text>
            <Text style={styles.networkModalText}>{networkError}</Text>
            <View style={styles.networkModalBtnRow}>
              <TouchableOpacity
                style={styles.networkModalRetryBtn}
                onPress={async () => {
                  if (retryAction) await retryAction();
                  setNetworkError(null);
                }}
              >
                <Text style={styles.networkModalBtnText}>Erneut versuchen</Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={styles.networkModalCancelBtn}
                onPress={() => setNetworkError(null)}
              >
                <Text style={styles.networkModalBtnText}>Abbrechen</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>
      <Modal visible={showConfirmation} transparent animationType="slide">
        <View style={styles.modalContainer}>
          <View style={styles.modalContent}>
            <Text style={styles.modalTitle}>Ödeme Onayı</Text>
            <Text style={styles.modalTotal}>Toplam: {totalAmount.toFixed(2)} €</Text>
            <View style={styles.buttonRow}>
              <TouchableOpacity onPress={() => setShowConfirmation(false)} style={styles.cancelBtn}>
                <Text style={styles.buttonText}>İptal</Text>
              </TouchableOpacity>
              <TouchableOpacity onPress={confirmCheckout} style={styles.confirmBtn}>
                <Text style={styles.buttonText}>Onayla</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>
    </View>
  );
};

const styles = StyleSheet.create({
  container: { flex: 1, padding: 8, backgroundColor: '#fff' },
  title: { fontSize: 18, fontWeight: 'bold', marginBottom: 8, textAlign: 'center' },
  // Ürün kutusu stilleri
  itemBox: { backgroundColor: '#f8f8f8', borderRadius: 10, padding: 8, marginBottom: 8, elevation: 1 },
  itemHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 2 },
  itemName: { fontSize: 14, fontWeight: '600', flex: 1 },
  itemDetailsRow: { flexDirection: 'row', justifyContent: 'space-between', marginBottom: 2 },
  detailText: { fontSize: 12, color: '#444' },
  bold: { fontWeight: 'bold', color: '#222' },
  extraOption: { fontSize: 11, color: '#888', marginTop: 1, fontStyle: 'italic' },
  quantityRow: { flexDirection: 'row', alignItems: 'center', marginTop: 2 },
  qtyBtn: { marginHorizontal: 4, backgroundColor: '#eee', borderRadius: 12, padding: 4 },
  quantity: { marginHorizontal: 4, fontSize: 14, fontWeight: 'bold' },
  price: { flex: 1, textAlign: 'right', fontSize: 14, fontWeight: '600' },
  removeBtn: { backgroundColor: '#d32f2f', borderRadius: 8, padding: 6, marginLeft: 4 },
  // Kupon alanı stilleri
  couponBox: { backgroundColor: '#f1f1f1', borderRadius: 8, padding: 6, marginTop: 4, marginBottom: 4 },
  couponLabel: { fontSize: 12, fontWeight: 'bold', marginBottom: 2 },
  couponRow: { flexDirection: 'row', alignItems: 'center' },
  couponInput: { flex: 1, backgroundColor: '#fff', borderRadius: 6, padding: 6, fontSize: 13, borderWidth: 1, borderColor: '#ccc', marginRight: 4 },
  couponBtn: { backgroundColor: '#1976d2', borderRadius: 6, paddingHorizontal: 8, paddingVertical: 6 },
  couponBtnText: { color: '#fff', fontWeight: 'bold', fontSize: 12 },
  couponRemoveBtn: { marginLeft: 2 },
  couponError: { color: '#d32f2f', fontSize: 12, marginTop: 2 },
  couponSuccess: { color: '#388e3c', fontSize: 12, marginTop: 2 },
  // Sepet özeti ve genel stiller
  summaryRow: { marginTop: 8, marginBottom: 4, alignItems: 'flex-end' },
  totalText: { fontSize: 16, fontWeight: 'bold', color: '#00796B' },
  discountText: { fontSize: 13, fontWeight: 'bold', color: '#d32f2f', marginBottom: 1 },
  buttonRow: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 4 }, // marginTop küçültüldü
  clearButton: { flex: 1, backgroundColor: '#e74c3c', marginRight: 2, borderRadius: 1, padding: 1, alignItems: 'center' },
  checkoutButton: { flex: 1, backgroundColor: '#27ae60', marginLeft: 2, borderRadius: 1, padding: 1, alignItems: 'center' },
  buttonText: { color: '#fff', fontSize: 8, fontWeight: 'bold' },
  modalContainer: { flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: 'rgba(0,0,0,0.3)' },
  modalContent: { backgroundColor: '#fff', padding: 16, borderRadius: 12, alignItems: 'center' },
  modalTitle: { fontSize: 16, fontWeight: 'bold', marginBottom: 6 },
  modalTotal: { fontSize: 14, fontWeight: '600', marginBottom: 8 },
  cancelBtn: { backgroundColor: '#eee', borderRadius: 8, padding: 8, marginRight: 4 },
  confirmBtn: { backgroundColor: '#27ae60', borderRadius: 8, padding: 8, marginLeft: 4 },
  errorText: { color: '#d32f2f', fontSize: 12, textAlign: 'center', marginBottom: 4 },
  emptyBox: { flex: 1, justifyContent: 'center', alignItems: 'center' },
  emptyText: { color: '#888', fontSize: 14, marginTop: 20 },
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
});

export default CartScreen; 