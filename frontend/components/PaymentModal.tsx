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
  Switch,
} from 'react-native';
import { useAuth } from '../contexts/AuthContext';
import { usePayment } from '../hooks/usePayment';
import { paymentService, PaymentRequest, PaymentItem } from '../services/api/paymentService';
import { cartService } from '../services/api/cartService';
import { customerService, GUEST_CUSTOMER_ID } from '../services/api/customerService';
import { validateSteuernummer, validateKassenId, validateAmount } from '../utils/validation';
import { receiptPrinter } from '../services/receiptPrinter';

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
  const { user } = useAuth();
  const [selectedPaymentMethod, setSelectedPaymentMethod] = useState<'cash' | 'card' | 'voucher' | 'transfer'>('cash');
  const [amountReceived, setAmountReceived] = useState<string>('');
  const [notes, setNotes] = useState<string>('');
  const [guestCustomerId, setGuestCustomerId] = useState<string>(GUEST_CUSTOMER_ID);
  // State for Purchase Flow
  type PurchaseState = 'input' | 'processing' | 'printing' | 'completed' | 'print_error';
  const [purchaseState, setPurchaseState] = useState<PurchaseState>('input');

  // Store paymentId for retry printing
  const [completedPaymentId, setCompletedPaymentId] = useState<string | null>(null);

  // DEV: TSE Simulation Toggle
  // Default: AÇIK (Bypass) in Development
  const [isTseSimulationEnabled, setIsTseSimulationEnabled] = useState<boolean>(__DEV__);

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

  // Load payment methods and guest customer
  useEffect(() => {
    if (visible) {
      getPaymentMethods();

      // Load guest customer ID
      customerService.getGuestCustomer()
        .then(id => setGuestCustomerId(id))
        .catch(err => console.warn('[PaymentModal] Failed to load guest customer:', err));
    }
  }, [visible, getPaymentMethods]);

  // Handler for preset buttons
  const handlePresetPress = (amount: number) => {
    setAmountReceived(amount.toString());
  };

  // Ödeme işlemi
  const handlePayment = async () => {
    try {
      // 1. Validasyonlar
      if (!tableNumber) {
        Alert.alert('Hata', 'Masa numarası gerekli');
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
      const steuernummer = 'ATU12345678';
      const kassenId = 'KASSE-001';

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

      // 2. Aktif sepet ID'sini al (Source of Truth for Cart ID)
      let currentCartId: string;
      try {
        const cart = await cartService.getCurrentCart(tableNumber);

        // Eğer backend cart boş geliyorsa ama frontend doluysa, frontend items kullanmaya devam et
        // Ancak cartId'ye ihtiyacımız var complete için.
        if (!cart || !cart.cartId) {
          throw new Error('Aktif masa sepeti bulunamadı.');
        }
        currentCartId = cart.cartId;
      } catch (cartErr) {
        console.error('Cart fetch failed:', cartErr);
        Alert.alert('Hata', 'Masa sepet bilgisi alınamadı. Lütfen sayfayı yenileyin.');
        return;
      }

      // 3. Determine customer ID (use guest if walk-in)
      const finalCustomerId = customerId && customerId !== '00000000-0000-0000-0000-000000000000'
        ? customerId
        : guestCustomerId;

      // 4. Build payment request (PascalCase keys for backend)
      const paymentItems: PaymentItem[] = cartItems.map(item => ({
        productId: item.productId,
        quantity: item.quantity,
        taxType: (item.taxType as 'standard' | 'reduced' | 'special') || 'standard'
      }));

      // NOTE: TSE Logic
      // If Simulation Enabled (Bypass) -> tseRequired: false
      // If Simulation Disabled (Real)  -> tseRequired: true
      // In PROD, always true
      const shouldRequireTse = __DEV__ ? !isTseSimulationEnabled : true;

      const paymentRequest: PaymentRequest = {
        customerId: finalCustomerId, // Always send valid customer ID (guest or registered)
        items: paymentItems,
        payment: {
          method: selectedPaymentMethod as 'cash' | 'card' | 'voucher',
          tseRequired: shouldRequireTse,
          amount: selectedPaymentMethod === 'cash' ? parseFloat(amountReceived) : undefined
        },
        tableNumber: tableNumber || 1,
        cashierId: user?.id || 'UNKNOWN', // Use logged-in user ID
        totalAmount: totalAmount,
        steuernummer: steuernummer,
        kassenId: kassenId,
        notes: notes || `Masa ${tableNumber} - ${new Date().toLocaleString('de-DE')}`
      };

      // 5. PAYMENT REQUEST (STRICT: error handling)
      setPurchaseState('processing');
      console.log('[PAYMENT] Request:', JSON.stringify(paymentRequest, null, 2));
      const response = await processPayment(paymentRequest);

      // 6. STRICT SUCCESS CHECK (Do NOT proceed if payment failed)
      if (!response.success) {
        console.error('[PAYMENT] Failed:', response);
        const errorMsg = response.message || response.error || 'Ödeme işlemi başarısız';
        Alert.alert('Ödeme Başarısız', errorMsg);
        setPurchaseState('input'); // Reset to input on failure
        return; // CRITICAL: Do NOT proceed to complete/reset
      }

      console.log('[PAYMENT] Success, paymentId:', response.paymentId);
      setCompletedPaymentId(response.paymentId);

      // 7. CART COMPLETE
      try {
        await cartService.completeCart(currentCartId, notes || '');
        console.log('[CART] Completed:', currentCartId);
      } catch (completeErr) {
        console.error('[CART] Complete failed:', completeErr);
        // ROLLBACK logic omitted for brevity as per previous logic, but strictly we should probably not rollback if payment succeeded but cart failed? 
        // For now, keeping existing flow but ensuring UI handles it.
        // If complete fails, we probably still want to print receipt if payment took money?
        // Let's assume critical failure here means we should notify user.
        Alert.alert('Uyarı', 'Ödeme alındı ancak sipariş kapatılamadı. Lütfen yöneticiye bildirin.');
      }

      // 8. CART RESET
      try {
        await cartService.resetCartAfterPayment(currentCartId, 'Payment completed');
        console.log('[CART] Reset complete');
      } catch (resetErr) {
        console.warn('[CART] Reset warning:', resetErr);
      }

      // 9. START PRINTING
      setPurchaseState('printing');
      try {
        await receiptPrinter.print(response.paymentId);
        setPurchaseState('completed');
        // Auto close after meaningful delay
        setTimeout(() => {
          handleSuccessAndClose(response.paymentId);
        }, 2000);
      } catch (printErr) {
        console.error('[PRINT] Failed:', printErr);
        setPurchaseState('print_error');
        // User will now see "Retry" or "Skip" buttons
      }

    } catch (err) {
      console.error('Handle Payment Error:', err);
      Alert.alert('Hata', err instanceof Error ? err.message : 'Bilinmeyen bir hata oluştu');
      setPurchaseState('input');
    }
  };

  // Helper to finish up
  const handleSuccessAndClose = (paymentId: string) => {
    onSuccess(paymentId);
    handleClose();
  };

  // Retrying print
  const handleRetryPrint = async () => {
    if (!completedPaymentId) return;
    setPurchaseState('printing');
    try {
      await receiptPrinter.print(completedPaymentId);
      setPurchaseState('completed');
      setTimeout(() => {
        handleSuccessAndClose(completedPaymentId);
      }, 2000);
    } catch (printErr) {
      console.error('[PRINT] Retry Failed:', printErr);
      setPurchaseState('print_error');
    }
  };

  // Skip printing
  const handleSkipPrint = () => {
    if (completedPaymentId) {
      handleSuccessAndClose(completedPaymentId);
    }
  };

  // Modal kapat
  const handleClose = () => {
    clearError();
    setSelectedPaymentMethod('cash');
    setAmountReceived('');
    setNotes('');
    setPurchaseState('input');
    setCompletedPaymentId(null);
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
            {/* DEV: TSE Simulation Toggle */}
            {__DEV__ && (
              <View style={styles.section}>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', backgroundColor: '#fff3e0', padding: 10, borderRadius: 8 }}>
                  <View>
                    <Text style={{ fontWeight: 'bold', color: '#e65100' }}>TSE Simülasyonu</Text>
                    <Text style={{ fontSize: 12, color: '#f57c00' }}>{isTseSimulationEnabled ? 'AÇIK (Bypass)' : 'KAPALI (Gerçek TSE)'}</Text>
                  </View>
                  <Switch
                    value={isTseSimulationEnabled}
                    onValueChange={setIsTseSimulationEnabled}
                    trackColor={{ false: "#767577", true: "#f57c00" }}
                    thumbColor={isTseSimulationEnabled ? "#ffb74d" : "#f4f3f4"}
                  />
                </View>
              </View>
            )}

          </ScrollView>

          {purchaseState === 'input' || purchaseState === 'processing' ? (
            <View style={styles.footer}>
              <TouchableOpacity onPress={handleClose} style={styles.cancelButton} disabled={purchaseState === 'processing'}>
                <Text style={styles.cancelButtonText}>İptal</Text>
              </TouchableOpacity>
              <TouchableOpacity
                onPress={handlePayment}
                style={[styles.payButton, purchaseState === 'processing' && styles.payButtonDisabled]}
                disabled={purchaseState === 'processing'}
              >
                {purchaseState === 'processing' ? (
                  <View style={{ flexDirection: 'row', alignItems: 'center' }}>
                    <ActivityIndicator color="#fff" style={{ marginRight: 8 }} />
                    <Text style={styles.payButtonText}>İşleniyor...</Text>
                  </View>
                ) : (
                  <Text style={styles.payButtonText}>
                    €{totalAmount.toFixed(2)} Öde
                  </Text>
                )}
              </TouchableOpacity>
            </View>
          ) : (
            // Printing / Success / Error Interaction Area
            <View style={[styles.footer, { flexDirection: 'column', alignItems: 'center' }]}>
              {purchaseState === 'printing' && (
                <View style={{ alignItems: 'center', padding: 10 }}>
                  <ActivityIndicator size="large" color="#007AFF" />
                  <Text style={{ marginTop: 10, fontSize: 16 }}>Fiş Yazdırılıyor...</Text>
                </View>
              )}

              {purchaseState === 'completed' && (
                <View style={{ alignItems: 'center', padding: 10 }}>
                  <Ionicons name="checkmark-circle" size={50} color="#4CAF50" />
                  <Text style={{ marginTop: 10, fontSize: 18, fontWeight: 'bold' }}>İşlem Tamamlandı!</Text>
                </View>
              )}

              {purchaseState === 'print_error' && (
                <View style={{ width: '100%' }}>
                  <Text style={{ textAlign: 'center', color: '#c62828', marginBottom: 10, fontWeight: 'bold' }}>
                    Fiş Yazdırılamadı!
                  </Text>
                  <View style={{ flexDirection: 'row', gap: 10, justifyContent: 'center' }}>
                    <TouchableOpacity onPress={handleSkipPrint} style={[styles.cancelButton, { backgroundColor: '#ffebee', borderColor: 'transparent', flex: 1 }]}>
                      <Text style={{ color: '#c62828', textAlign: 'center' }}>Atla</Text>
                    </TouchableOpacity>

                    <TouchableOpacity
                      onPress={() => {
                        // Mocking PDF download as retry for now, or we could add a specific download method
                        // For this patch, re-triggering print (which opens system dialog) is often synonymous with "Save as PDF" on mobile/web.
                        // But to be distinct, we'll label it as PDF.
                        handleRetryPrint();
                      }}
                      style={[styles.cancelButton, { backgroundColor: '#e3f2fd', borderColor: 'transparent', flex: 1 }]}
                    >
                      <Text style={{ color: '#1976d2', textAlign: 'center' }}>PDF İndir</Text>
                    </TouchableOpacity>

                    <TouchableOpacity onPress={handleRetryPrint} style={[styles.payButton, { flex: 1 }]}>
                      <Text style={[styles.payButtonText, { textAlign: 'center' }]}>Tekrar Dene</Text>
                    </TouchableOpacity>
                  </View>
                </View>
              )}
            </View>
          )}
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
