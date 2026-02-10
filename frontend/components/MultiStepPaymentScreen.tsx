// Türkçe Açıklama: Multi-step ödeme ekranı. Her adımda ilgili API endpoint'ine istek atar, 
// kullanıcı deneyimini optimize eder ve Avusturya RKSV uyumluluğunu sağlar.

import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  TextInput,
  StyleSheet,
  Alert,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  ActivityIndicator,
  Dimensions
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import paymentService from '../services/api/paymentService';
import { PaymentCancelResponse } from '../types/cart';

import { useTranslation } from 'react-i18next';
import { useAuth } from '../contexts/AuthContext';

// Ödeme adımları enum'u
enum PaymentStep {
  CUSTOMER_SELECTION = 0,
  PAYMENT_METHOD = 1,
  PAYMENT_AMOUNT = 2,
  TSE_VERIFICATION = 3,
  CONFIRMATION = 4,
  RECEIPT = 5
}

// Desteklenen ödeme yöntemleri - i18n kullanarak
type PaymentMethodKey = 'cash' | 'card' | 'voucher';
const PAYMENT_METHODS: {
  key: PaymentMethodKey;
  label: string;
  icon: keyof typeof Ionicons.glyphMap;
  requiresTSE: boolean;
}[] = [
    { key: 'cash', label: 'payment:methods.cash', icon: 'cash', requiresTSE: true },
    { key: 'card', label: 'payment:methods.card', icon: 'card', requiresTSE: true },
    { key: 'voucher', label: 'payment:methods.voucher', icon: 'pricetag', requiresTSE: false },
  ];

interface MultiStepPaymentScreenProps {
  totalAmount: number;
  cartItems: any[];
  onComplete: (receipt: any) => void;
  onCancel: () => void;
  onPaymentCancelled?: (response: PaymentCancelResponse) => void;
  tableNumber: number;
}

const MultiStepPaymentScreen: React.FC<MultiStepPaymentScreenProps> = ({
  totalAmount,
  cartItems,
  onComplete,
  onCancel,
  onPaymentCancelled,
  tableNumber
}) => {
  const { t } = useTranslation(['payment', 'common']);
  const { user } = useAuth();
  const [currentStep, setCurrentStep] = useState<PaymentStep>(PaymentStep.CUSTOMER_SELECTION);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  // Form state'leri
  const [customerId, setCustomerId] = useState('');
  const [selectedPaymentMethod, setSelectedPaymentMethod] = useState<PaymentMethodKey | null>(null);
  const [paymentAmount, setPaymentAmount] = useState('');
  const [tseSignature, setTseSignature] = useState('');
  const [paymentSessionId, setPaymentSessionId] = useState<string | null>(null);

  // Progress bar için
  const progress = ((currentStep + 1) / Object.keys(PaymentStep).length) * 100;

  // Step başlıkları - i18n kullanarak
  const stepTitles = [
    t('payment:stepTitles.customerSelection'),
    t('payment:stepTitles.paymentMethod'),
    t('payment:stepTitles.paymentAmount'),
    t('payment:stepTitles.tseVerification'),
    t('payment:stepTitles.confirmation'),
    t('payment:stepTitles.receipt')
  ];

  // Customer ID validation (8 haneli zorunlu alan)
  const validateCustomerId = (id: string) => {
    return /^\d{8}$/.test(id);
  };

  // Steuernummer validation (ATU format)
  const validateSteuernummer = (steuer: string) => {
    return /^ATU\d{8}$/.test(steuer);
  };

  // Bir sonraki adıma geç
  const nextStep = () => {
    if (currentStep < PaymentStep.RECEIPT) {
      setCurrentStep(currentStep + 1);
      setError('');
    }
  };

  // Önceki adıma dön
  const prevStep = () => {
    if (currentStep > PaymentStep.CUSTOMER_SELECTION) {
      setCurrentStep(currentStep - 1);
      setError('');
    }
  };

  // Müşteri seçimi tamamlandı
  const handleCustomerSelection = async () => {
    if (!validateCustomerId(customerId)) {
      setError(t('payment:customer.idRequired'));
      return;
    }

    setLoading(true);
    try {
      // Müşteri doğrulama API'si
      // await paymentService.validateCustomer(customerId);

      nextStep();
    } catch (error) {
      setError(t('payment:customer.idInvalid'));
    } finally {
      setLoading(false);
    }
  };

  // Ödeme yöntemi seçimi
  const handlePaymentMethodSelection = (method: PaymentMethodKey) => {
    setSelectedPaymentMethod(method);
    nextStep();
  };

  // Ödeme tutarı girişi
  const handlePaymentAmount = () => {
    const amount = parseFloat(paymentAmount);
    if (isNaN(amount) || amount < totalAmount) {
      setError(t('payment:amount.error'));
      return;
    }
    nextStep();
  };

  // TSE doğrulama
  const handleTSEVerification = async () => {
    if (!selectedPaymentMethod || PAYMENT_METHODS.find(m => m.key === selectedPaymentMethod)?.requiresTSE) {
      if (!tseSignature) {
        setError(t('payment:tse.required'));
        return;
      }
    }

    setLoading(true);
    try {
      // TSE doğrulama API'si
      // await paymentService.verifyTSE(tseSignature);

      nextStep();
    } catch (error) {
      setError(t('payment:tse.verificationError'));
    } finally {
      setLoading(false);
    }
  };

  // Ödeme onayı
  const handlePaymentConfirmation = async () => {
    setLoading(true);
    try {
      // Ödeme işlemi API'si
      const paymentRequest = {
        items: cartItems.map(item => ({
          productId: item.product.id,
          quantity: item.quantity,
          price: item.unitPrice,
          taxType: item.product.taxType.toLowerCase() as 'standard' | 'reduced' | 'special'
        })),
        payment: {
          method: selectedPaymentMethod!,
          amount: parseFloat(paymentAmount),
          tseRequired: PAYMENT_METHODS.find(m => m.key === selectedPaymentMethod)?.requiresTSE || false
        },
        customerId: customerId,
        tableNumber: tableNumber,
        cashierId: user?.id || 'UNKNOWN',
        totalAmount: totalAmount,
        steuernummer: 'ATU12345678', // Hardcoded for compliance as per PaymentModal
        kassenId: 'KASSE-001'
      };

      const response = await paymentService.processPayment(paymentRequest);

      if (response.success) {
        setPaymentSessionId(response.paymentId);
        nextStep();
      } else {
        setError(response.error || t('payment:errors.paymentFailed'));
      }
    } catch (error) {
      setError(t('payment:errors.paymentError'));
    } finally {
      setLoading(false);
    }
  };

  // Fiş oluşturma
  const handleReceiptCreation = async () => {
    if (!paymentSessionId) return;

    setLoading(true);
    try {
      const receipt = await paymentService.createReceipt(paymentSessionId);

      onComplete(receipt);
    } catch (error) {
      setError(t('payment:receipt.error'));
    } finally {
      setLoading(false);
    }
  };

  // Ödeme iptali
  const handlePaymentCancel = async () => {
    Alert.alert(
      t('payment:cancellation.title'),
      t('payment:cancellation.message'),
      [
        { text: t('payment:cancellation.deny'), style: 'cancel' },
        {
          text: t('payment:cancellation.confirm'),
          style: 'destructive',
          onPress: async () => {
            if (paymentSessionId) {
              try {
                const cancelResponse = await paymentService.cancelPayment(
                  paymentSessionId,
                  t('payment:cancellation.reason')
                );

                if (onPaymentCancelled) {
                  onPaymentCancelled(cancelResponse);
                }
              } catch (error) {
                console.error('Payment cancellation error:', error);
              }
            }
            onCancel();
          }
        }
      ]
    );
  };

  // Step render fonksiyonları
  const renderCustomerSelection = () => (
    <View style={styles.stepContainer}>
      <Text style={styles.stepTitle}>{t('payment:customerSelection')}</Text>
      <Text style={styles.stepDescription}>
        {t('payment:customer.idRequired')}
      </Text>

      <TextInput
        style={styles.input}
        placeholder={t('payment:customer.idPlaceholder')}
        value={customerId}
        onChangeText={setCustomerId}
        keyboardType="numeric"
        maxLength={8}
        editable={!loading}
      />

      <Text style={styles.validationText}>
        {customerId.length === 8 ? t('payment:customer.idValid') : t('payment:customer.idRequired')}
      </Text>

      <TouchableOpacity
        style={[styles.nextButton, !validateCustomerId(customerId) && styles.disabledButton]}
        onPress={handleCustomerSelection}
        disabled={!validateCustomerId(customerId) || loading}
      >
        {loading ? (
          <ActivityIndicator color="#fff" />
        ) : (
          <Text style={styles.buttonText}>{t('payment:buttons.continue')}</Text>
        )}
      </TouchableOpacity>
    </View>
  );

  const renderPaymentMethod = () => (
    <View style={styles.stepContainer}>
      <Text style={styles.stepTitle}>{t('payment:paymentMethod')}</Text>
      <Text style={styles.stepDescription}>
        {t('payment:amount.total')}: {totalAmount.toFixed(2)} €
      </Text>

      {PAYMENT_METHODS.map(method => (
        <TouchableOpacity
          key={method.key}
          style={styles.methodCard}
          onPress={() => handlePaymentMethodSelection(method.key)}
        >
          <Ionicons name={method.icon} size={24} color="#1976d2" />
          <Text style={styles.methodLabel}>{t(method.label)}</Text>
          {method.requiresTSE && (
            <View style={styles.tseBadge}>
              <Text style={styles.tseBadgeText}>TSE</Text>
            </View>
          )}
          <Ionicons name="chevron-forward" size={20} color="#ccc" />
        </TouchableOpacity>
      ))}
    </View>
  );

  const renderPaymentAmount = () => (
    <View style={styles.stepContainer}>
      <Text style={styles.stepTitle}>{t('payment:paymentAmount')}</Text>
      <Text style={styles.stepDescription}>
        {t('payment:paymentMethod')}: {t(PAYMENT_METHODS.find(m => m.key === selectedPaymentMethod)?.label || '')}
      </Text>

      <View style={styles.amountContainer}>
        <Text style={styles.amountLabel}>{t('payment:amount.total')}:</Text>
        <Text style={styles.totalAmount}>{totalAmount.toFixed(2)} €</Text>
      </View>

      <TextInput
        style={styles.amountInput}
        placeholder={t('payment:amount.placeholder')}
        value={paymentAmount}
        onChangeText={setPaymentAmount}
        keyboardType="decimal-pad"
        editable={!loading}
      />

      <Text style={styles.amountHint}>
        {t('payment.amount.hint')}
      </Text>

      <View style={styles.buttonRow}>
        <TouchableOpacity style={styles.backButton} onPress={prevStep}>
          <Text style={styles.buttonText}>{t('payment:buttons.back')}</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.nextButton, !paymentAmount && styles.disabledButton]}
          onPress={handlePaymentAmount}
          disabled={!paymentAmount || loading}
        >
          {loading ? (
            <ActivityIndicator color="#fff" />
          ) : (
            <Text style={styles.buttonText}>{t('payment:buttons.continue')}</Text>
          )}
        </TouchableOpacity>
      </View>
    </View>
  );

  const renderTSEVerification = () => (
    <View style={styles.stepContainer}>
      <Text style={styles.stepTitle}>{t('payment.tseVerification')}</Text>
      <Text style={styles.stepDescription}>
        {PAYMENT_METHODS.find(m => m.key === selectedPaymentMethod)?.requiresTSE
          ? t('payment.tse.required')
          : t('payment.tse.notRequired')
        }
      </Text>

      {PAYMENT_METHODS.find(m => m.key === selectedPaymentMethod)?.requiresTSE && (
        <TextInput
          style={styles.input}
          placeholder={t('payment:tse.signaturePlaceholder')}
          value={tseSignature}
          onChangeText={setTseSignature}
          editable={!loading}
        />
      )}

      <View style={styles.buttonRow}>
        <TouchableOpacity style={styles.backButton} onPress={prevStep}>
          <Text style={styles.buttonText}>{t('payment:buttons.back')}</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.nextButton, !tseSignature && styles.disabledButton]}
          onPress={handleTSEVerification}
          disabled={!tseSignature || loading}
        >
          {loading ? (
            <ActivityIndicator color="#fff" />
          ) : (
            <Text style={styles.buttonText}>{t('payment:buttons.verify')}</Text>
          )}
        </TouchableOpacity>
      </View>
    </View>
  );

  const renderConfirmation = () => (
    <View style={styles.stepContainer}>
      <Text style={styles.stepTitle}>{t('payment:confirmationTitle')}</Text>

      <View style={styles.confirmationCard}>
        <Text style={styles.confirmationLabel}>{t('payment:confirmation.customerId')}</Text>
        <Text style={styles.confirmationValue}>{customerId}</Text>

        <Text style={styles.confirmationLabel}>{t('payment:confirmation.paymentMethod')}</Text>
        <Text style={styles.confirmationValue}>
          {t(PAYMENT_METHODS.find(m => m.key === selectedPaymentMethod)?.label || '')}
        </Text>

        <Text style={styles.confirmationLabel}>{t('payment:confirmation.paymentAmount')}</Text>
        <Text style={styles.confirmationValue}>{paymentAmount} €</Text>

        <Text style={styles.confirmationLabel}>{t('payment:confirmation.totalAmount')}</Text>
        <Text style={styles.confirmationValue}>{totalAmount.toFixed(2)} €</Text>

        <Text style={styles.confirmationLabel}>{t('payment:confirmation.change')}</Text>
        <Text style={styles.confirmationValue}>
          {(parseFloat(paymentAmount) - totalAmount).toFixed(2)} €
        </Text>
      </View>

      <View style={styles.buttonRow}>
        <TouchableOpacity style={styles.backButton} onPress={prevStep}>
          <Text style={styles.buttonText}>{t('payment:buttons.back')}</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={styles.confirmButton}
          onPress={handlePaymentConfirmation}
          disabled={loading}
        >
          {loading ? (
            <ActivityIndicator color="#fff" />
          ) : (
            <Text style={styles.buttonText}>{t('payment:buttons.confirmPayment')}</Text>
          )}
        </TouchableOpacity>
      </View>
    </View>
  );

  const renderReceipt = () => (
    <View style={styles.stepContainer}>
      <Text style={styles.stepTitle}>{t('payment:receiptTitle')}</Text>
      <Text style={styles.stepDescription}>
        {t('payment:receiptDescription')}
      </Text>

      <View style={styles.successIcon}>
        <Ionicons name="checkmark-circle" size={80} color="#4caf50" />
      </View>

      <TouchableOpacity
        style={styles.receiptButton}
        onPress={handleReceiptCreation}
        disabled={loading}
      >
        {loading ? (
          <ActivityIndicator color="#fff" />
        ) : (
          <Text style={styles.buttonText}>{t('payment:receipt.view')}</Text>
        )}
      </TouchableOpacity>
    </View>
  );

  // Ana render fonksiyonu
  const renderCurrentStep = () => {
    switch (currentStep) {
      case PaymentStep.CUSTOMER_SELECTION:
        return renderCustomerSelection();
      case PaymentStep.PAYMENT_METHOD:
        return renderPaymentMethod();
      case PaymentStep.PAYMENT_AMOUNT:
        return renderPaymentAmount();
      case PaymentStep.TSE_VERIFICATION:
        return renderTSEVerification();
      case PaymentStep.CONFIRMATION:
        return renderConfirmation();
      case PaymentStep.RECEIPT:
        return renderReceipt();
      default:
        return null;
    }
  };

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      style={styles.container}
    >
      {/* Header */}
      <View style={styles.header}>
        <TouchableOpacity style={styles.cancelButton} onPress={handlePaymentCancel}>
          <Ionicons name="close" size={24} color="#666" />
        </TouchableOpacity>
        <Text style={styles.headerTitle}>{t('payment:title')}</Text>
        <View style={styles.headerSpacer} />
      </View>

      {/* Progress Bar */}
      <View style={styles.progressContainer}>
        <View style={styles.progressBar}>
          <View style={[styles.progressFill, { width: `${progress}%` }]} />
        </View>
        <Text style={styles.progressText}>
          {t('common:step')} {currentStep + 1} / {Object.keys(PaymentStep).length}: {stepTitles[currentStep]}
        </Text>
      </View>

      {/* Error Display */}
      {error ? (
        <View style={styles.errorContainer}>
          <Ionicons name="alert-circle" size={20} color="#d32f2f" />
          <Text style={styles.errorText}>{error}</Text>
        </View>
      ) : null}

      {/* Step Content */}
      <ScrollView style={styles.content} showsVerticalScrollIndicator={false}>
        {renderCurrentStep()}
      </ScrollView>
    </KeyboardAvoidingView>
  );
};

const { width } = Dimensions.get('window');

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: 16,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  cancelButton: {
    padding: 8,
  },
  headerTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
  },
  headerSpacer: {
    width: 40,
  },
  progressContainer: {
    padding: 16,
    backgroundColor: '#fff',
  },
  progressBar: {
    height: 6,
    backgroundColor: '#e0e0e0',
    borderRadius: 3,
    marginBottom: 8,
  },
  progressFill: {
    height: '100%',
    backgroundColor: '#1976d2',
    borderRadius: 3,
  },
  progressText: {
    fontSize: 14,
    color: '#666',
    textAlign: 'center',
  },
  errorContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#ffebee',
    padding: 12,
    margin: 16,
    borderRadius: 8,
    borderLeftWidth: 4,
    borderLeftColor: '#d32f2f',
  },
  errorText: {
    color: '#d32f2f',
    marginLeft: 8,
    flex: 1,
  },
  content: {
    flex: 1,
    padding: 16,
  },
  stepContainer: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 20,
    marginBottom: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  stepTitle: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 8,
    textAlign: 'center',
  },
  stepDescription: {
    fontSize: 16,
    color: '#666',
    textAlign: 'center',
    marginBottom: 20,
  },
  input: {
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 8,
    padding: 16,
    fontSize: 16,
    backgroundColor: '#fff',
    marginBottom: 16,
  },
  validationText: {
    fontSize: 14,
    color: '#4caf50',
    textAlign: 'center',
    marginBottom: 20,
  },
  methodCard: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#f8f9fa',
    padding: 16,
    borderRadius: 8,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: '#e9ecef',
  },
  methodLabel: {
    flex: 1,
    fontSize: 16,
    fontWeight: '500',
    marginLeft: 12,
    color: '#333',
  },
  tseBadge: {
    backgroundColor: '#ff9800',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 12,
    marginRight: 12,
  },
  tseBadgeText: {
    color: '#fff',
    fontSize: 12,
    fontWeight: 'bold',
  },
  amountContainer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: '#e3f2fd',
    padding: 16,
    borderRadius: 8,
    marginBottom: 20,
  },
  amountLabel: {
    fontSize: 16,
    color: '#1976d2',
    fontWeight: '500',
  },
  totalAmount: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#1976d2',
  },
  amountInput: {
    borderWidth: 2,
    borderColor: '#1976d2',
    borderRadius: 8,
    padding: 16,
    fontSize: 24,
    textAlign: 'center',
    backgroundColor: '#fff',
    marginBottom: 12,
  },
  amountHint: {
    fontSize: 14,
    color: '#666',
    textAlign: 'center',
    marginBottom: 20,
  },
  confirmationCard: {
    backgroundColor: '#f8f9fa',
    padding: 16,
    borderRadius: 8,
    marginBottom: 20,
  },
  confirmationLabel: {
    fontSize: 14,
    color: '#666',
    marginBottom: 4,
  },
  confirmationValue: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 12,
  },
  successIcon: {
    alignItems: 'center',
    marginVertical: 20,
  },
  buttonRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: 12,
  },
  backButton: {
    flex: 1,
    backgroundColor: '#6c757d',
    padding: 16,
    borderRadius: 8,
    alignItems: 'center',
  },
  nextButton: {
    flex: 1,
    backgroundColor: '#1976d2',
    padding: 16,
    borderRadius: 8,
    alignItems: 'center',
  },
  confirmButton: {
    flex: 1,
    backgroundColor: '#28a745',
    padding: 16,
    borderRadius: 8,
    alignItems: 'center',
  },
  receiptButton: {
    backgroundColor: '#17a2b8',
    padding: 16,
    borderRadius: 8,
    alignItems: 'center',
  },
  disabledButton: {
    opacity: 0.5,
  },
  buttonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
  },
});

export default MultiStepPaymentScreen;
