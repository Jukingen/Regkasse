import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect, useMemo } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  ScrollView,
  Alert,
  ActivityIndicator,
  TextInput,
} from 'react-native';
import { usePayment } from '../hooks/usePayment';
import { PaymentRequest, PaymentItem } from '../services/api/paymentService';
import { validateSteuernummer, validateKassenId, validateAmount } from '../utils/validation';

// Türkçe Açıklama: Ödeme alma modal'ı - Sepet içeriğini ödeme işlemine dönüştürür
interface PaymentModalProps {
  visible: boolean;
  onClose: () => void;
  onSuccess: (paymentId: string) => void;
  cartItems: Array<{
    id: string;
    productId: string;
    productName: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
    taxType?: string;
  }>;
  customerId?: string;
  tableNumber?: number;
}

export default function PaymentModal({
  visible,
  onClose,
  onSuccess,
  cartItems,
  customerId = '00000000-0000-0000-0000-000000000000', // Default Guid formatında
  tableNumber
}: PaymentModalProps) {
  const [selectedPaymentMethod, setSelectedPaymentMethod] = useState<'cash' | 'card' | 'voucher' | 'transfer'>('cash');
  const [amountReceived, setAmountReceived] = useState<string>('');
  const [notes, setNotes] = useState<string>('');

  const {
    loading,
    methodsLoading,
    error,
    paymentMethods,
    getPaymentMethods,
    processPayment,
    clearError
  } = usePayment();

  // Hesaplamalar: Single Source of Truth olarak quantity * unitPrice kullan
  const calculatedCartItems = useMemo(() => {
    return cartItems.map(item => ({
      ...item,
      lineTotal: item.quantity * item.unitPrice
    }));
  }, [cartItems]);

  const totalAmount = useMemo(() => {
    return calculatedCartItems.reduce((sum, item) => sum + item.lineTotal, 0);
  }, [calculatedCartItems]);

  const changeAmount = parseFloat(amountReceived) - totalAmount;

  // Smart Cash Presets Logic
  const getCashPresets = (total: number): number[] => {
    const presets: number[] = [];
    // Base denominations (Euro)
    const bills = [5, 10, 20, 50, 100, 200, 500];

    for (const bill of bills) {
      if (bill >= total) {
        presets.push(bill);
      }
    }
    // Limit to 4 options
    return presets.slice(0, 4);
  };

  const cashPresets = getCashPresets(totalAmount);

  // Ödeme yöntemlerini yükle
  useEffect(() => {
    if (visible) {
      getPaymentMethods();
    }
  }, [visible, getPaymentMethods]);

  // Handler for preset buttons
  const handlePresetPress = (amount: number) => {
    setAmountReceived(amount.toString());
  };

  // Ödeme işlemi
  const handlePayment = async () => {
    try {
      // Validasyon
      if (!tableNumber) {
        Alert.alert('Hata', 'Masa numarası gerekli');
        return;
      }

      if (!customerId) {
        Alert.alert('Hata', 'Müşteri ID gerekli');
        return;
      }

      // Guid formatı validasyonu
      const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
      if (!guidRegex.test(customerId)) {
        Alert.alert('Hata', 'Geçersiz Müşteri ID formatı (GUID formatında olmalı)');
        return;
      }

      if (cartItems.length === 0) {
        Alert.alert('Hata', 'Sepet boş');
        return;
      }

      if (selectedPaymentMethod === 'cash') {
        const received = parseFloat(amountReceived);
        if (isNaN(received) || received < totalAmount) {
          Alert.alert('Hata', 'Alınan tutar toplam tutardan az olamaz');
          return;
        }
      }

      // Avusturya yasal gereksinimleri validasyonu
      const steuernummer = 'ATU12345678'; // TODO: Gerçek vergi numarası kullan
      const kassenId = 'KASSE-001'; // TODO: Gerçek kasa ID'si kullan

      if (!validateSteuernummer(steuernummer)) {
        Alert.alert('Hata', 'Geçersiz Steuernummer formatı (ATU12345678)');
        return;
      }

      if (!validateKassenId(kassenId)) {
        Alert.alert('Hata', 'Geçersiz KassenId (3-50 karakter)');
        return;
      }

      if (!validateAmount(totalAmount)) {
        Alert.alert('Hata', 'Geçersiz tutar (0.01\'den büyük olmalı)');
        return;
      }

      // Sepet öğelerini PaymentItem formatına dönüştür
      const paymentItems: PaymentItem[] = cartItems.map(item => ({
        productId: item.productId,
        quantity: item.quantity,
        taxType: (item.taxType as 'standard' | 'reduced' | 'special') || 'standard'
      }));

      // Ödeme request'i oluştur - Backend'deki CreatePaymentRequest ile uyumlu
      // Varsayılan payload oluşturuyoruz
      const paymentRequest: PaymentRequest = {
        customerId: customerId || '00000000-0000-0000-0000-000000000000', // Guid formatında
        items: paymentItems,
        payment: {
          method: selectedPaymentMethod as 'cash' | 'card' | 'voucher', // Tip uyuşmazlığı varsa casting
          tseRequired: true, // Avusturya yasaları gereği
          amount: selectedPaymentMethod === 'cash' ? parseFloat(amountReceived) : undefined
        },
        // Yeni eklenen alanlar
        tableNumber: tableNumber || 1,
        cashierId: 'demo-cashier-001', // TODO: Gerçek kasiyer ID'si kullan
        totalAmount: totalAmount,

        // Avusturya yasal gereksinimleri
        steuernummer: steuernummer,
        kassenId: kassenId,

        notes: notes || `Masa ${tableNumber} - ${new Date().toLocaleString('de-DE')} `
      };

      // Ödeme işlemini gerçekleştir
      const response = await processPayment(paymentRequest);

      if (response.success) {
        Alert.alert(
          'Başarılı',
          `Ödeme tamamlandı!\nÖdeme ID: ${response.paymentId} `,
          [
            {
              text: 'Tamam',
              onPress: () => {
                onSuccess(response.paymentId);
                handleClose();
              }
            }
          ]
        );
      }
    } catch (err) {
      Alert.alert('Hata', err instanceof Error ? err.message : 'Ödeme işlemi başarısız');
    }
  };

  // Modal kapat
  const handleClose = () => {
    clearError();
    setSelectedPaymentMethod('cash');
    setAmountReceived('');
    setNotes('');
    onClose();
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent={true}
      onRequestClose={handleClose}
    >
      <View style={styles.overlay}>
        <View style={styles.modal}>
          {/* Header */}
          <View style={styles.header}>
            <Text style={styles.title}>Ödeme Al</Text>
            <TouchableOpacity onPress={handleClose} style={styles.closeButton}>
              <Ionicons name="close" size={24} color="#333" />
            </TouchableOpacity>
          </View>

          <ScrollView style={styles.content}>
            {/* Sepet Özeti */}
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>Sepet Özeti</Text>
              {calculatedCartItems.map((item, index) => (
                <View key={index} style={styles.cartItem}>
                  <Text style={styles.itemName}>{item.productName}</Text>
                  <Text style={styles.itemDetails}>
                    {item.quantity} x €{item.unitPrice.toFixed(2)} = €{item.lineTotal.toFixed(2)}
                  </Text>
                </View>
              ))}
              <View style={styles.totalRow}>
                <Text style={styles.totalLabel}>Toplam:</Text>
                <Text style={styles.totalAmount}>€{totalAmount.toFixed(2)}</Text>
              </View>
            </View>

            {/* Ödeme Yöntemleri */}
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>Ödeme Yöntemi</Text>
              <View style={styles.paymentMethodsContainer}>
                {methodsLoading ? (
                  <Text style={styles.loadingText}>Ödeme yöntemleri yükleniyor...</Text>
                ) : paymentMethods && paymentMethods.length > 0 ? (
                  paymentMethods.map((method) => (
                    <TouchableOpacity
                      key={method.id}
                      style={[
                        styles.paymentMethod,
                        selectedPaymentMethod === method.type && styles.selectedPaymentMethod
                      ]}
                      onPress={() => setSelectedPaymentMethod(method.type as any)}
                    >
                      <Ionicons
                        name={method.icon as any}
                        size={24}
                        color={selectedPaymentMethod === method.type ? '#007AFF' : '#666'}
                      />
                      <Text style={[
                        styles.paymentMethodText,
                        selectedPaymentMethod === method.type && styles.selectedPaymentMethodText
                      ]}>
                        {method.name}
                      </Text>
                    </TouchableOpacity>
                  ))
                ) : (
                  <View style={{ width: '100%', alignItems: 'center' }}>
                    <Text style={styles.errorText}>Yöntem bulunamadı</Text>
                    <TouchableOpacity onPress={getPaymentMethods} style={{ marginTop: 10 }}>
                      <Text style={{ color: '#007AFF' }}>Tekrar Dene</Text>
                    </TouchableOpacity>
                  </View>
                )}
              </View>
            </View>

            {/* Nakit Ödeme Detayları */}
            {selectedPaymentMethod === 'cash' && (
              <View style={styles.section}>
                <Text style={styles.sectionTitle}>Nakit Ödeme</Text>

                {/* Smart Cash Presets */}
                <View style={styles.presetsContainer}>
                  {cashPresets.map((preset) => (
                    <TouchableOpacity
                      key={preset}
                      style={styles.presetButton}
                      onPress={() => handlePresetPress(preset)}
                    >
                      <Text style={styles.presetButtonText}>€{preset}</Text>
                    </TouchableOpacity>
                  ))}
                </View>

                <View style={styles.inputRow}>
                  <Text style={styles.label}>Alınan Tutar:</Text>
                  <TextInput
                    style={styles.amountInput}
                    value={amountReceived}
                    onChangeText={setAmountReceived}
                    placeholder="0.00"
                    keyboardType="numeric"
                  />
                </View>
                {parseFloat(amountReceived) > 0 && (
                  <View style={styles.changeRow}>
                    <Text style={styles.changeLabel}>Para Üstü:</Text>
                    <Text style={styles.changeAmount}>€{changeAmount.toFixed(2)}</Text>
                  </View>
                )}
              </View>
            )}

            {/* Notlar */}
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>Notlar</Text>
              <TextInput
                style={styles.notesInput}
                value={notes}
                onChangeText={setNotes}
                placeholder="Ödeme notları..."
                multiline
                numberOfLines={3}
              />
            </View>

            {/* Hata Mesajı */}
            {error && (
              <View style={styles.errorContainer}>
                <Text style={styles.errorText}>{error}</Text>
              </View>
            )}
          </ScrollView>

          {/* Footer */}
          <View style={styles.footer}>
            <TouchableOpacity onPress={handleClose} style={styles.cancelButton}>
              <Text style={styles.cancelButtonText}>İptal</Text>
            </TouchableOpacity>
            <TouchableOpacity
              onPress={handlePayment}
              style={[styles.payButton, loading && styles.payButtonDisabled]}
              disabled={loading}
            >
              {loading ? (
                <ActivityIndicator color="#fff" />
              ) : (
                <Text style={styles.payButtonText}>
                  €{totalAmount.toFixed(2)} Öde
                </Text>
              )}
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'flex-end',
  },
  modal: {
    backgroundColor: '#fff',
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    maxHeight: '90%',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
  },
  title: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#333',
  },
  closeButton: {
    padding: 5,
  },
  content: {
    padding: 20,
  },
  section: {
    marginBottom: 20,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 10,
  },
  cartItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 8,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
  },
  itemName: {
    fontSize: 14,
    color: '#333',
    flex: 1,
  },
  itemDetails: {
    fontSize: 14,
    color: '#666',
  },
  totalRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: 15,
    borderTopWidth: 1,
    borderTopColor: '#eee',
  },
  totalLabel: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
  },
  totalAmount: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#007AFF',
  },
  paymentMethodsContainer: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    flexWrap: 'wrap',
  },
  paymentMethod: {
    alignItems: 'center',
    padding: 15,
    borderRadius: 10,
    borderWidth: 2,
    borderColor: '#eee',
    minWidth: 80,
  },
  selectedPaymentMethod: {
    borderColor: '#007AFF',
    backgroundColor: '#f0f8ff',
  },
  paymentMethodText: {
    marginTop: 5,
    fontSize: 12,
    color: '#666',
  },
  selectedPaymentMethodText: {
    color: '#007AFF',
    fontWeight: '600',
  },
  inputRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 10,
  },
  label: {
    fontSize: 14,
    color: '#333',
  },
  amountInput: {
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 8,
    padding: 10,
    fontSize: 16,
    textAlign: 'right',
    minWidth: 100,
  },
  changeRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: 10,
    borderTopWidth: 1,
    borderTopColor: '#eee',
  },
  changeLabel: {
    fontSize: 14,
    color: '#333',
  },
  changeAmount: {
    fontSize: 16,
    fontWeight: '600',
    color: '#28a745',
  },
  notesInput: {
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 8,
    padding: 10,
    fontSize: 14,
    minHeight: 80,
    textAlignVertical: 'top',
  },
  errorContainer: {
    backgroundColor: '#ffebee',
    padding: 15,
    borderRadius: 8,
    marginTop: 10,
  },
  errorText: {
    color: '#c62828',
    fontSize: 14,
  },
  footer: {
    flexDirection: 'row',
    padding: 20,
    borderTopWidth: 1,
    borderTopColor: '#eee',
  },
  cancelButton: {
    flex: 1,
    padding: 15,
    marginRight: 10,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#ddd',
    alignItems: 'center',
  },
  cancelButtonText: {
    fontSize: 16,
    color: '#666',
  },
  payButton: {
    flex: 2,
    padding: 15,
    backgroundColor: '#007AFF',
    borderRadius: 8,
    alignItems: 'center',
  },
  payButtonDisabled: {
    backgroundColor: '#ccc',
  },
  payButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: '#fff',
  },
  loadingText: {
    fontSize: 14,
    color: '#666',
    textAlign: 'center',
    paddingVertical: 10,
  },
  presetsContainer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 15,
    flexWrap: 'wrap',
    gap: 10,
  },
  presetButton: {
    paddingVertical: 10,
    paddingHorizontal: 15,
    backgroundColor: '#e3f2fd',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#90caf9',
    minWidth: 60,
    alignItems: 'center',
    flex: 1,
    marginHorizontal: 2,
  },
  presetButtonText: {
    color: '#1976d2',
    fontWeight: '600',
    fontSize: 14,
  },
}); 
