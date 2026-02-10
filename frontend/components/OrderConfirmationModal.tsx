// Sipariş onaylama modal'ı
// Müşteri bilgileri ve sipariş detaylarını gösterir
import React, { useState } from 'react';
import {
  Modal,
  View,
  Text,
  TextInput,
  TouchableOpacity,
  ScrollView,
  Alert,
  ActivityIndicator
} from 'react-native';
import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';
import { orderService } from '../services/api/orderService';
import { Cart, CartItem } from '../types/cart';

interface OrderConfirmationModalProps {
  visible: boolean;
  onClose: () => void;
  onSuccess: (orderId: string) => void;
  cart: Cart | null;
  tableNumber: string;
  waiterName: string;
}

export const OrderConfirmationModal: React.FC<OrderConfirmationModalProps> = ({
  visible,
  onClose,
  onSuccess,
  cart,
  tableNumber,
  waiterName
}) => {
  const [customerName, setCustomerName] = useState('');
  const [customerPhone, setCustomerPhone] = useState('');
  const [notes, setNotes] = useState('');
  const [loading, setLoading] = useState(false);

  const handleConfirmOrder = async () => {
    if (!cart || !cart.items || cart.items.length === 0) {
      Alert.alert('Hata', 'Sepette ürün bulunamadı');
      return;
    }

    if (!customerName.trim()) {
      Alert.alert('Hata', 'Müşteri adı gerekli');
      return;
    }

    setLoading(true);

    try {
      const orderResponse = await orderService.createOrderFromCart(
        tableNumber,
        waiterName,
        cart.items,
        customerName.trim(),
        customerPhone.trim() || undefined,
        notes.trim() || undefined,
        cart.cartId
      );

      setLoading(false);
      Alert.alert(
        'Başarılı!',
        `Sipariş oluşturuldu: ${orderResponse.orderId}`,
        [
          {
            text: 'Tamam',
            onPress: () => {
              onSuccess(orderResponse.orderId);
              onClose();
              // Form'u temizle
              setCustomerName('');
              setCustomerPhone('');
              setNotes('');
            }
          }
        ]
      );
    } catch (error: any) {
      setLoading(false);
      const errorMessage = error.response?.data?.Message || error.message || 'Sipariş oluşturulurken hata oluştu';
      Alert.alert('Hata', errorMessage);
    }
  };

  const calculateTotal = () => {
    if (!cart?.items) return 0;
    return cart.items.reduce((total, item) => total + (item.totalAmount || 0), 0);
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent={true}
      onRequestClose={onClose}
    >
      <View style={styles.overlay}>
        <View style={styles.modalContainer}>
          <View style={styles.header}>
            <Text style={styles.title}>Sipariş Onayla</Text>
            <TouchableOpacity onPress={onClose} style={styles.closeButton}>
              <Text style={styles.closeButtonText}>✕</Text>
            </TouchableOpacity>
          </View>

          <ScrollView style={styles.content}>
            {/* Müşteri Bilgileri */}
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>Müşteri Bilgileri</Text>
              <TextInput
                style={styles.input}
                placeholder="Müşteri Adı *"
                value={customerName}
                onChangeText={setCustomerName}
                placeholderTextColor={Colors.light.textSecondary}
              />
              <TextInput
                style={styles.input}
                placeholder="Telefon (opsiyonel)"
                value={customerPhone}
                onChangeText={setCustomerPhone}
                placeholderTextColor={Colors.light.textSecondary}
                keyboardType="phone-pad"
              />
              <TextInput
                style={styles.input}
                placeholder="Notlar (opsiyonel)"
                value={notes}
                onChangeText={setNotes}
                placeholderTextColor={Colors.light.textSecondary}
                multiline
                numberOfLines={3}
              />
            </View>

            {/* Sipariş Detayları */}
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>Sipariş Detayları</Text>
              <View style={styles.orderInfo}>
                <Text style={styles.orderInfoText}>Masa: {tableNumber}</Text>
                <Text style={styles.orderInfoText}>Garson: {waiterName}</Text>
                <Text style={styles.orderInfoText}>Ürün Sayısı: {cart?.items?.length || 0}</Text>
              </View>
            </View>

            {/* Ürün Listesi */}
            {cart?.items && cart.items.length > 0 && (
              <View style={styles.section}>
                <Text style={styles.sectionTitle}>Ürünler</Text>
                {cart.items.map((item, index) => (
                  <View key={index} style={styles.itemRow}>
                    <View style={styles.itemInfo}>
                      <Text style={styles.itemName}>{item.productName}</Text>
                      <Text style={styles.itemDetails}>
                        {item.quantity}x {item.unitPrice?.toFixed(2)}€
                      </Text>
                      {item.notes && (
                        <Text style={styles.itemNotes}>Not: {item.notes}</Text>
                      )}
                    </View>
                    <Text style={styles.itemTotal}>
                      {(item.totalAmount || 0).toFixed(2)}€
                    </Text>
                  </View>
                ))}
              </View>
            )}

            {/* Toplam */}
            <View style={styles.totalSection}>
              <Text style={styles.totalText}>
                Toplam: {calculateTotal().toFixed(2)}€
              </Text>
            </View>
          </ScrollView>

          {/* Butonlar */}
          <View style={styles.buttonContainer}>
            <TouchableOpacity
              style={[styles.button, styles.cancelButton]}
              onPress={onClose}
              disabled={loading}
            >
              <Text style={styles.cancelButtonText}>İptal</Text>
            </TouchableOpacity>
            
            <TouchableOpacity
              style={[styles.button, styles.confirmButton]}
              onPress={handleConfirmOrder}
              disabled={loading}
            >
              {loading ? (
                <ActivityIndicator color={Colors.light.white} size="small" />
              ) : (
                <Text style={styles.confirmButtonText}>Siparişi Onayla</Text>
              )}
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );
};

const styles = {
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'center',
    alignItems: 'center'
  },
  modalContainer: {
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.lg,
    width: '90%',
    maxHeight: '80%',
    padding: 0
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border
  },
  title: {
    ...Typography.h2,
    color: Colors.light.text
  },
  closeButton: {
    padding: Spacing.sm
  },
  closeButtonText: {
    fontSize: 20,
    color: Colors.light.textSecondary
  },
  content: {
    flex: 1,
    padding: Spacing.lg
  },
  section: {
    marginBottom: Spacing.lg
  },
  sectionTitle: {
    ...Typography.h3,
    color: Colors.light.text,
    marginBottom: Spacing.md
  },
  input: {
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    marginBottom: Spacing.md,
    fontSize: 16,
    color: Colors.light.text
  },
  orderInfo: {
    backgroundColor: Colors.light.backgroundSecondary,
    padding: Spacing.md,
    borderRadius: BorderRadius.md
  },
  orderInfoText: {
    ...Typography.body,
    color: Colors.light.text,
    marginBottom: Spacing.sm
  },
  itemRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    paddingVertical: Spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.borderLight
  },
  itemInfo: {
    flex: 1
  },
  itemName: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: '600'
  },
  itemDetails: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    marginTop: 2
  },
  itemNotes: {
    ...Typography.caption,
    color: Colors.light.primary,
    marginTop: 2,
    fontStyle: 'italic'
  },
  itemTotal: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: '600'
  },
  totalSection: {
    alignItems: 'center',
    padding: Spacing.lg,
    backgroundColor: Colors.light.primary,
    borderRadius: BorderRadius.md,
    marginTop: Spacing.md
  },
  totalText: {
    ...Typography.h2,
    color: Colors.light.white,
    fontWeight: 'bold'
  },
  buttonContainer: {
    flexDirection: 'row',
    padding: Spacing.lg,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
    gap: Spacing.md
  },
  button: {
    flex: 1,
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    alignItems: 'center',
    justifyContent: 'center'
  },
  cancelButton: {
    backgroundColor: Colors.light.backgroundSecondary,
    borderWidth: 1,
    borderColor: Colors.light.border
  },
  cancelButtonText: {
    ...Typography.body,
    color: Colors.light.textSecondary,
    fontWeight: '600'
  },
  confirmButton: {
    backgroundColor: Colors.light.primary
  },
  confirmButtonText: {
    ...Typography.body,
    color: Colors.light.white,
    fontWeight: '600'
  }
};
