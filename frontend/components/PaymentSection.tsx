import { Ionicons } from '@expo/vector-icons';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  TextInput,
  Alert,
  Animated,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';
import { CartItem } from '../types/cart';

interface PaymentSectionProps {
  cart: CartItem[];
  paymentAmount: string;
  setPaymentAmount: (amount: string) => void;
  selectedPaymentMethod: 'cash' | 'card' | 'voucher';
  setSelectedPaymentMethod: (method: 'cash' | 'card' | 'voucher') => void;
  calculateTotal: () => number;
  calculateTax: () => number;
  onPayment: () => void;
  isProcessingPayment: boolean;
  showChangeResult: boolean;
  changeAmount: number;
  changeAnimation: Animated.Value;
  showChangeResult: boolean;
  setShowChangeResult: (show: boolean) => void;
  setChangeAmount: (amount: number) => void;
}

const PaymentSection: React.FC<PaymentSectionProps> = ({
  cart,
  paymentAmount,
  setPaymentAmount,
  selectedPaymentMethod,
  setSelectedPaymentMethod,
  calculateTotal,
  calculateTax,
  onPayment,
  isProcessingPayment,
  changeAmount,
  changeAnimation,
  showChangeResult,
  setShowChangeResult,
  setChangeAmount,
}) => {
  const { t } = useTranslation();

  const handleCalculateChange = () => {
    const totalWithTax = calculateTotal() + calculateTax();
    const change = parseFloat(paymentAmount) - totalWithTax;

    if (change >= 0) {
      setChangeAmount(change);
      setShowChangeResult(true);

      changeAnimation.setValue(0);
      Animated.spring(changeAnimation, {
        toValue: 1,
        useNativeDriver: true,
        tension: 200,
        friction: 4,
      }).start();

      setTimeout(() => {
        setShowChangeResult(false);
      }, 1500);
    } else {
      Alert.alert(
        t('payment.invalidAmount', 'Geçersiz Tutar'),
        t('payment.amountTooLow', 'Girilen tutar çok düşük.'),
        [{ text: t('common.ok', 'Tamam') }]
      );
    }
  };

  // ✅ Defensive Check: Prevent crash if cart items are missing
  if (!cart || !cart.length) return null;

  return (
    <View style={styles.paymentSection}>
      <Text style={styles.paymentTitle}>{t('cashRegister.payment')}</Text>

      {/* Ödeme Yöntemi */}
      <View style={styles.paymentMethodContainer}>
        <Text style={styles.paymentLabel}>{t('cashRegister.paymentMethod')}:</Text>
        <View style={styles.paymentMethodButtons}>
          {(['cash', 'card', 'voucher'] as const).map(method => (
            <TouchableOpacity
              key={method}
              style={[
                styles.paymentMethodButton,
                selectedPaymentMethod === method && styles.paymentMethodButtonActive
              ]}
              onPress={() => setSelectedPaymentMethod(method)}
            >
              <Text style={[
                styles.paymentMethodText,
                selectedPaymentMethod === method && styles.paymentMethodTextActive
              ]}>
                {t(`cashRegister.${method}`)}
              </Text>
            </TouchableOpacity>
          ))}
        </View>
      </View>

      {/* Ödeme Tutarı */}
      <View style={styles.paymentAmountContainer}>
        <Text style={styles.paymentLabel}>{t('cashRegister.amountReceived')}:</Text>

        <TextInput
          style={styles.paymentAmountInput}
          placeholder="0.00"
          value={paymentAmount}
          onChangeText={(text) => {
            const cleanedText = text.replace(/[^0-9.]/g, '');
            const parts = cleanedText.split('.');
            if (parts.length <= 2) {
              setPaymentAmount(cleanedText);
            }
          }}
          keyboardType="decimal-pad"
          onFocus={() => {
            if (cart?.items?.length > 0 && !paymentAmount) {
              const totalWithTax = calculateTotal() + calculateTax();
              setPaymentAmount(totalWithTax.toFixed(2));
            }
          }}
        />

        <View style={styles.quickAmountButtons}>
          <TouchableOpacity
            style={[
              styles.quickAmountButton,
              paymentAmount === '5' && styles.quickAmountButtonActive
            ]}
            onPress={() => setPaymentAmount('5')}
          >
            <Text style={[
              styles.quickAmountButtonText,
              paymentAmount === '5' && styles.quickAmountButtonTextActive
            ]}>5€</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={[
              styles.quickAmountButton,
              paymentAmount === '10' && styles.quickAmountButtonActive
            ]}
            onPress={() => setPaymentAmount('10')}
          >
            <Text style={[
              styles.quickAmountButtonText,
              paymentAmount === '10' && styles.quickAmountButtonTextActive
            ]}>10€</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={[
              styles.quickAmountButton,
              paymentAmount === '20' && styles.quickAmountButtonActive
            ]}
            onPress={() => setPaymentAmount('20')}
          >
            <Text style={[
              styles.quickAmountButtonText,
              paymentAmount === '20' && styles.quickAmountButtonTextActive
            ]}>20€</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={[
              styles.quickAmountButton,
              paymentAmount === '50' && styles.quickAmountButtonActive
            ]}
            onPress={() => setPaymentAmount('50')}
          >
            <Text style={[
              styles.quickAmountButtonText,
              paymentAmount === '50' && styles.quickAmountButtonTextActive
            ]}>50€</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={styles.quickAmountButton}
            onPress={() => setPaymentAmount('')}
          >
            <Text style={styles.quickAmountButtonText}>Temizle</Text>
          </TouchableOpacity>
        </View>
      </View>

      {/* Para Üstü Önizleme */}
      {paymentAmount && parseFloat(paymentAmount) > 0 && selectedPaymentMethod === 'cash' && (
        <View style={styles.changePreviewContainer}>
          <Text style={styles.changePreviewLabel}>Para Üstü:</Text>
          <Text style={[
            styles.changePreviewAmount,
            parseFloat(paymentAmount) < (calculateTotal() + calculateTax()) && styles.changePreviewAmountNegative
          ]}>
            €{(parseFloat(paymentAmount) - (calculateTotal() + calculateTax())).toFixed(2)}
          </Text>
        </View>
      )}

      {/* Para Üstü Hesaplama */}
      {paymentAmount && parseFloat(paymentAmount) > 0 && selectedPaymentMethod === 'cash' && (
        <TouchableOpacity
          style={[
            styles.changeButton,
            parseFloat(paymentAmount) < (calculateTotal() + calculateTax()) && styles.changeButtonDisabled
          ]}
          onPress={handleCalculateChange}
          disabled={parseFloat(paymentAmount) < (calculateTotal() + calculateTax())}
        >
          <Ionicons
            name="calculator-outline"
            size={16}
            color={parseFloat(paymentAmount) < (calculateTotal() + calculateTax()) ? Colors.light.textSecondary : "white"}
          />
          <Text style={[
            styles.changeButtonText,
            parseFloat(paymentAmount) < (calculateTotal() + calculateTax()) && styles.changeButtonTextDisabled
          ]}>
            {t('cashRegister.calculateChange')}
          </Text>
        </TouchableOpacity>
      )}

      {/* Para Üstü Sonucu */}
      {showChangeResult && (
        <Animated.View
          style={[
            styles.changeResultContainer,
            {
              transform: [{
                scale: changeAnimation.interpolate({
                  inputRange: [0, 1],
                  outputRange: [0.5, 1],
                })
              }],
              opacity: changeAnimation,
            }
          ]}
        >
          <Ionicons name="checkmark-circle" size={32} color={Colors.light.success} />
          <Text style={styles.changeResultTitle}>{t('cashRegister.change')}</Text>
          <Text style={styles.changeResultAmount}>€{changeAmount.toFixed(2)}</Text>
        </Animated.View>
      )}

      {/* Ödeme Butonu */}
      <TouchableOpacity
        style={[
          styles.payButton,
          isProcessingPayment && styles.payButtonProcessing
        ]}
        onPress={onPayment}
        disabled={isProcessingPayment}
      >
        {isProcessingPayment ? (
          <>
            <Ionicons name="hourglass-outline" size={24} color="white" />
            <Text style={styles.payButtonText}>Verarbeitung...</Text>
          </>
        ) : (
          <>
            <Ionicons name="card-outline" size={24} color="white" />
            <Text style={styles.payButtonText}>{t('cashRegister.processPayment')}</Text>
          </>
        )}
      </TouchableOpacity>
    </View>
  );
};

const styles = StyleSheet.create({
  paymentSection: {
    backgroundColor: Colors.light.surface,
    padding: Spacing.sm,
    borderRadius: BorderRadius.md,
    marginTop: Spacing.sm,
  },
  paymentTitle: {
    ...Typography.caption,
    color: Colors.light.text,
    marginBottom: Spacing.sm,
    fontWeight: '600',
    fontSize: 12,
  },
  paymentMethodContainer: {
    marginBottom: Spacing.sm,
  },
  paymentLabel: {
    ...Typography.caption,
    color: Colors.light.text,
    marginBottom: Spacing.xs,
    fontSize: 11,
  },
  paymentMethodButtons: {
    flexDirection: 'row',
    gap: Spacing.xs,
  },
  paymentMethodButton: {
    flex: 1,
    padding: Spacing.xs,
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.sm,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  paymentMethodButtonActive: {
    backgroundColor: Colors.light.primary,
    borderColor: Colors.light.primary,
  },
  paymentMethodText: {
    ...Typography.caption,
    color: Colors.light.text,
    fontSize: 10,
  },
  paymentMethodTextActive: {
    color: 'white',
  },
  paymentAmountContainer: {
    marginBottom: Spacing.sm,
  },
  paymentAmountInput: {
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.sm,
    padding: Spacing.sm,
    ...Typography.h3,
    color: Colors.light.text,
    textAlign: 'center',
    marginBottom: Spacing.xs,
    fontSize: 18,
  },
  quickAmountButtons: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    marginTop: Spacing.xs,
    gap: Spacing.xs,
  },
  quickAmountButton: {
    flex: 1,
    padding: Spacing.xs,
    backgroundColor: Colors.light.info,
    borderRadius: BorderRadius.sm,
    alignItems: 'center',
  },
  quickAmountButtonActive: {
    backgroundColor: Colors.light.primary,
  },
  quickAmountButtonText: {
    ...Typography.caption,
    color: 'white',
    fontWeight: '600',
    fontSize: 10,
  },
  quickAmountButtonTextActive: {
    color: 'white',
    fontWeight: 'bold',
  },
  changePreviewContainer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: Colors.light.surface,
    padding: Spacing.xs,
    borderRadius: BorderRadius.sm,
    marginTop: Spacing.xs,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  changePreviewLabel: {
    ...Typography.caption,
    color: Colors.light.text,
    fontWeight: '600',
    fontSize: 10,
  },
  changePreviewAmount: {
    ...Typography.caption,
    color: Colors.light.success,
    fontWeight: 'bold',
    fontSize: 11,
  },
  changePreviewAmountNegative: {
    color: Colors.light.error,
  },
  changeButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.info,
    padding: Spacing.xs,
    borderRadius: BorderRadius.sm,
    marginBottom: Spacing.xs,
    gap: Spacing.xs,
  },
  changeButtonDisabled: {
    backgroundColor: Colors.light.textSecondary,
    opacity: 0.6,
  },
  changeButtonText: {
    ...Typography.caption,
    color: 'white',
    fontWeight: '600',
    fontSize: 10,
  },
  changeButtonTextDisabled: {
    color: Colors.light.textSecondary,
  },
  changeResultContainer: {
    alignItems: 'center',
    backgroundColor: Colors.light.success + '20',
    padding: Spacing.sm,
    borderRadius: BorderRadius.sm,
    marginBottom: Spacing.sm,
  },
  changeResultTitle: {
    ...Typography.caption,
    color: Colors.light.success,
    marginTop: Spacing.xs,
    fontSize: 11,
  },
  changeResultAmount: {
    ...Typography.caption,
    color: Colors.light.success,
    fontWeight: 'bold',
    fontSize: 14,
  },
  payButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.primary,
    padding: Spacing.sm,
    borderRadius: BorderRadius.sm,
    gap: Spacing.xs,
  },
  payButtonProcessing: {
    backgroundColor: Colors.light.warning,
  },
  payButtonText: {
    ...Typography.caption,
    color: 'white',
    fontWeight: '600',
    fontSize: 12,
  },
});

export default PaymentSection; 