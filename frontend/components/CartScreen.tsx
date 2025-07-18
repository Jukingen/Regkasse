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
} from 'react-native';

import { CartItem } from '../types/cart';

interface CartScreenProps {
  cart: {
    items: CartItem[];
    onUpdateQuantity: (productId: string, quantity: number) => Promise<void>;
    onRemoveItem: (productId: string) => Promise<void>;
    onClearCart: () => Promise<void>;
    onCheckout: () => void;
    isLoading?: boolean;
    error?: string | null;
    onRetry?: () => void;
    totalAmount?: number;
  };
}

const CartScreen: React.FC<CartScreenProps> = ({
  cart,
}) => {
  const [showConfirmation, setShowConfirmation] = useState(false);
  const [processingItem, setProcessingItem] = useState<string | null>(null);
  const totalAnimation = useRef(new Animated.Value(1)).current;

  // Miktar güncelleme
  const handleQuantityChange = async (productId: string, newQuantity: number) => {
    if (processingItem) return;
    setProcessingItem(productId);
    try {
      if (newQuantity <= 0) {
        Alert.alert(
          'Ürünü Kaldır',
          'Bu ürünü sepetten kaldırmak istediğinizden emin misiniz?',
          [
            { text: 'İptal', style: 'cancel' },
            {
              text: 'Kaldır',
              style: 'destructive',
              onPress: async () => {
                await cart.onRemoveItem(productId);
                Vibration.vibrate(50);
              },
            },
          ]
        );
      } else {
        const product = cart.items.find(item => item.product.id === productId)?.product;
        if (product && newQuantity > product.stockQuantity) {
          Alert.alert('Stok Uyarısı', `Stokta sadece ${product.stockQuantity} adet var`);
          Vibration.vibrate(100);
          return;
        }
        await cart.onUpdateQuantity(productId, newQuantity);
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
      }
    } catch (error) {
      Alert.alert('Bağlantı hatası! Tekrar deneyin.');
    } finally {
      setProcessingItem(null);
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
              await cart.onClearCart();
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
    if (cart.items.length === 0) {
      Alert.alert('Sepetiniz boş!');
      return;
    }
    setShowConfirmation(true);
  };

  const confirmCheckout = () => {
    setShowConfirmation(false);
    cart.onCheckout();
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Sepet</Text>
      <ScrollView style={{ flex: 1 }}>
        {cart.items.map(item => (
          <View key={item.id} style={styles.itemRow}>
            <Text style={styles.itemName}>{item.product.name}</Text>
            <View style={styles.quantityRow}>
              <TouchableOpacity
                onPress={() => handleQuantityChange(item.product.id, item.quantity - 1)}
                disabled={processingItem === item.product.id}
                style={styles.qtyBtn}
              >
                <Ionicons name="remove-circle" size={32} color="#e74c3c" />
              </TouchableOpacity>
              <Text style={styles.quantity}>{item.quantity}</Text>
              <TouchableOpacity
                onPress={() => handleQuantityChange(item.product.id, item.quantity + 1)}
                disabled={processingItem === item.product.id}
                style={styles.qtyBtn}
              >
                <Ionicons name="add-circle" size={32} color="#27ae60" />
              </TouchableOpacity>
            </View>
            <Text style={styles.price}>{item.product.price.toFixed(2)} €</Text>
          </View>
        ))}
      </ScrollView>
      <View style={styles.summaryRow}>
        <Text style={styles.totalText}>Toplam: {cart.totalAmount?.toFixed(2) ?? '0.00'} €</Text>
      </View>
      <View style={styles.buttonRow}>
        <TouchableOpacity style={styles.clearButton} onPress={handleClearCart}>
          <Text style={styles.buttonText}>Sepeti Temizle</Text>
        </TouchableOpacity>
        <TouchableOpacity style={styles.checkoutButton} onPress={handleCheckout}>
          <Text style={styles.buttonText}>Ödeme Yap</Text>
        </TouchableOpacity>
      </View>
      {/* Snackbar/Toast için örnek entegrasyon */}
      {/* <Snackbar visible={snackbarVisible} onDismiss={() => setSnackbarVisible(false)}>{snackbarMessage}</Snackbar> */}
      <Modal visible={showConfirmation} transparent animationType="slide">
        <View style={styles.modalContainer}>
          <View style={styles.modalContent}>
            <Text style={styles.modalTitle}>Ödeme Onayı</Text>
            <Text style={styles.modalTotal}>Toplam: {cart.totalAmount?.toFixed(2) ?? '0.00'} €</Text>
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
  container: { flex: 1, padding: 16, backgroundColor: '#fff' },
  title: { fontSize: 28, fontWeight: 'bold', marginBottom: 16, textAlign: 'center' },
  itemRow: { flexDirection: 'row', alignItems: 'center', marginBottom: 18, backgroundColor: '#f8f8f8', borderRadius: 12, padding: 12 },
  itemName: { flex: 2, fontSize: 20, fontWeight: '600' },
  quantityRow: { flexDirection: 'row', alignItems: 'center', flex: 1, justifyContent: 'center' },
  qtyBtn: { marginHorizontal: 8, backgroundColor: '#eee', borderRadius: 16, padding: 6 },
  quantity: { marginHorizontal: 8, fontSize: 22, fontWeight: 'bold' },
  price: { flex: 1, textAlign: 'right', fontSize: 20, fontWeight: '600' },
  summaryRow: { marginTop: 16, marginBottom: 8, alignItems: 'flex-end' },
  totalText: { fontSize: 26, fontWeight: 'bold', color: '#00796B' },
  buttonRow: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 24 },
  clearButton: { flex: 1, backgroundColor: '#e74c3c', marginRight: 8, borderRadius: 12, padding: 18, alignItems: 'center' },
  checkoutButton: { flex: 1, backgroundColor: '#27ae60', marginLeft: 8, borderRadius: 12, padding: 18, alignItems: 'center' },
  buttonText: { color: '#fff', fontSize: 22, fontWeight: 'bold' },
  modalContainer: { flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: 'rgba(0,0,0,0.3)' },
  modalContent: { backgroundColor: '#fff', padding: 32, borderRadius: 16, alignItems: 'center' },
  modalTitle: { fontSize: 24, fontWeight: 'bold', marginBottom: 12 },
  modalTotal: { fontSize: 22, fontWeight: '600', marginBottom: 18 },
  cancelBtn: { backgroundColor: '#eee', borderRadius: 10, padding: 14, marginRight: 8 },
  confirmBtn: { backgroundColor: '#27ae60', borderRadius: 10, padding: 14, marginLeft: 8 },
});

export default CartScreen; 