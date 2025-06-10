import React, { useState } from 'react';
import {
  Modal,
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  TextInput,
  Alert,
  ActivityIndicator,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';

interface PaymentModalProps {
  visible: boolean;
  onClose: () => void;
  onProcessPayment: (method: string, amount: number) => Promise<void>;
  total: number;
  loading: boolean;
  systemConfig: any;
  isOnline: boolean;
}

const PaymentModal: React.FC<PaymentModalProps> = ({
  visible,
  onClose,
  onProcessPayment,
  total,
  loading,
  systemConfig,
  isOnline,
}) => {
  const { t } = useTranslation();
  const [selectedMethod, setSelectedMethod] = useState<'cash' | 'card' | 'voucher'>('cash');
  const [amount, setAmount] = useState(total.toString());

  const handlePayment = async () => {
    const paymentAmount = parseFloat(amount);
    
    if (isNaN(paymentAmount) || paymentAmount < total) {
      Alert.alert(
        t('payment.invalid_amount'),
        t('payment.amount_too_low'),
        [{ text: t('common.ok') }]
      );
      return;
    }

    await onProcessPayment(selectedMethod, paymentAmount);
  };

  const getPaymentMethods = () => {
    const methods = [];
    
    // Nakit her zaman mevcut
    methods.push({
      id: 'cash',
      name: t('payment.cash'),
      icon: 'cash-outline',
      available: true,
    });
    
    // Kart ödemesi sadece online modda
    if (systemConfig.operationMode !== 'offline-only' && isOnline) {
      methods.push({
        id: 'card',
        name: t('payment.card'),
        icon: 'card-outline',
        available: true,
      });
    }
    
    // Voucher sadece online modda
    if (systemConfig.operationMode !== 'offline-only' && isOnline) {
      methods.push({
        id: 'voucher',
        name: t('payment.voucher'),
        icon: 'gift-outline',
        available: true,
      });
    }
    
    return methods;
  };

  const paymentMethods = getPaymentMethods();

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent={true}
      onRequestClose={onClose}
    >
      <View style={styles.overlay}>
        <View style={styles.modal}>
          <View style={styles.header}>
            <Text style={styles.title}>{t('payment.title')}</Text>
            <TouchableOpacity onPress={onClose} style={styles.closeButton}>
              <Ionicons name="close" size={24} color="#666" />
            </TouchableOpacity>
          </View>

          <View style={styles.content}>
            {/* Toplam */}
            <View style={styles.totalSection}>
              <Text style={styles.totalLabel}>{t('payment.total')}</Text>
              <Text style={styles.totalAmount}>{total.toFixed(2)}€</Text>
            </View>

            {/* Ödeme Yöntemi */}
            <View style={styles.methodSection}>
              <Text style={styles.sectionTitle}>{t('payment.method')}</Text>
              <View style={styles.methodList}>
                {paymentMethods.map((method) => (
                  <TouchableOpacity
                    key={method.id}
                    style={[
                      styles.methodItem,
                      selectedMethod === method.id && styles.methodItemSelected,
                      !method.available && styles.methodItemDisabled,
                    ]}
                    onPress={() => method.available && setSelectedMethod(method.id as any)}
                    disabled={!method.available}
                  >
                    <Ionicons
                      name={method.icon as any}
                      size={24}
                      color={selectedMethod === method.id ? '#2196F3' : '#666'}
                    />
                    <Text
                      style={[
                        styles.methodText,
                        selectedMethod === method.id && styles.methodTextSelected,
                        !method.available && styles.methodTextDisabled,
                      ]}
                    >
                      {method.name}
                    </Text>
                    {selectedMethod === method.id && (
                      <Ionicons name="checkmark-circle" size={20} color="#2196F3" />
                    )}
                  </TouchableOpacity>
                ))}
              </View>
            </View>

            {/* Tutar */}
            <View style={styles.amountSection}>
              <Text style={styles.sectionTitle}>{t('payment.amount')}</Text>
              <TextInput
                style={styles.amountInput}
                value={amount}
                onChangeText={setAmount}
                keyboardType="numeric"
                placeholder={total.toFixed(2)}
              />
            </View>

            {/* Mod Bilgisi */}
            <View style={styles.modeInfo}>
              <Ionicons
                name={isOnline ? 'wifi' : 'wifi-outline'}
                size={16}
                color={isOnline ? '#4CAF50' : '#FF9800'}
              />
              <Text style={styles.modeText}>
                {t(`modes.${systemConfig.operationMode}`)}
              </Text>
            </View>
          </View>

          {/* Butonlar */}
          <View style={styles.footer}>
            <TouchableOpacity
              style={styles.cancelButton}
              onPress={onClose}
              disabled={loading}
            >
              <Text style={styles.cancelButtonText}>{t('common.cancel')}</Text>
            </TouchableOpacity>
            
            <TouchableOpacity
              style={[styles.confirmButton, loading && styles.confirmButtonDisabled]}
              onPress={handlePayment}
              disabled={loading}
            >
              {loading ? (
                <ActivityIndicator color="white" />
              ) : (
                <>
                  <Ionicons name="checkmark" size={20} color="white" />
                  <Text style={styles.confirmButtonText}>{t('payment.confirm')}</Text>
                </>
              )}
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  modal: {
    backgroundColor: 'white',
    borderRadius: 16,
    width: '90%',
    maxWidth: 400,
    maxHeight: '80%',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  title: {
    fontSize: 20,
    fontWeight: '600',
  },
  closeButton: {
    padding: 4,
  },
  content: {
    padding: 20,
  },
  totalSection: {
    alignItems: 'center',
    marginBottom: 24,
  },
  totalLabel: {
    fontSize: 16,
    color: '#666',
    marginBottom: 8,
  },
  totalAmount: {
    fontSize: 32,
    fontWeight: '700',
    color: '#2196F3',
  },
  methodSection: {
    marginBottom: 24,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 12,
  },
  methodList: {
    gap: 8,
  },
  methodItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 16,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    gap: 12,
  },
  methodItemSelected: {
    borderColor: '#2196F3',
    backgroundColor: '#f3f8ff',
  },
  methodItemDisabled: {
    opacity: 0.5,
  },
  methodText: {
    flex: 1,
    fontSize: 16,
    color: '#333',
  },
  methodTextSelected: {
    color: '#2196F3',
    fontWeight: '600',
  },
  methodTextDisabled: {
    color: '#999',
  },
  amountSection: {
    marginBottom: 16,
  },
  amountInput: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    padding: 12,
    fontSize: 18,
    textAlign: 'center',
  },
  modeInfo: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    padding: 12,
    backgroundColor: '#f5f5f5',
    borderRadius: 8,
  },
  modeText: {
    fontSize: 14,
    color: '#666',
  },
  footer: {
    flexDirection: 'row',
    padding: 20,
    borderTopWidth: 1,
    borderTopColor: '#e0e0e0',
    gap: 12,
  },
  cancelButton: {
    flex: 1,
    padding: 16,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    alignItems: 'center',
  },
  cancelButtonText: {
    fontSize: 16,
    color: '#666',
  },
  confirmButton: {
    flex: 2,
    backgroundColor: '#4CAF50',
    padding: 16,
    borderRadius: 8,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
  },
  confirmButtonDisabled: {
    backgroundColor: '#ccc',
  },
  confirmButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: 'white',
  },
});

export default PaymentModal; 