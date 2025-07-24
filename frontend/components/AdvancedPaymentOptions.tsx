import { Ionicons } from '@expo/vector-icons';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  Modal,
  TextInput,
  Alert,
  Vibration,
  ScrollView,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';

interface PaymentMethod {
  id: string;
  name: string;
  icon: string;
  color: string;
  requiresAmount: boolean;
  supportsChange: boolean;
  description: string;
}

interface AdvancedPaymentOptionsProps {
  visible: boolean;
  onClose: () => void;
  onPaymentMethodSelect: (method: PaymentMethod, amount?: number) => void;
  totalAmount: number;
}

const AdvancedPaymentOptions: React.FC<AdvancedPaymentOptionsProps> = ({
  visible,
  onClose,
  onPaymentMethodSelect,
  totalAmount,
}) => {
  const { t } = useTranslation();
  const [selectedMethod, setSelectedMethod] = useState<PaymentMethod | null>(null);
  const [paymentAmount, setPaymentAmount] = useState('');
  const [showAmountInput, setShowAmountInput] = useState(false);
  const [changeAmount, setChangeAmount] = useState(0);

  const handleMethodSelect = (method: PaymentMethod) => {
    setSelectedMethod(method);
    
    if (method.requiresAmount) {
      setShowAmountInput(true);
      setPaymentAmount(totalAmount.toString());
    } else {
      // Direkt ödeme işlemi
      onPaymentMethodSelect(method, totalAmount);
      handleClose();
    }
  };

  const handleAmountConfirm = () => {
    const amount = parseFloat(paymentAmount);
    
    if (isNaN(amount) || amount < totalAmount) {
      Alert.alert(
        t('payment.invalidAmount', 'Geçersiz Tutar'),
        t('payment.amountTooLow', 'Ödeme tutarı toplamdan az olamaz.'),
        [{ text: t('common.ok', 'Tamam') }]
      );
      return;
    }

    if (selectedMethod) {
      const change = amount - totalAmount;
      setChangeAmount(change);
      
      Alert.alert(
        'Confirm Payment',
        `Amount: €${amount.toFixed(2)}\nChange: €${change.toFixed(2)}`,
        [
          { text: 'Cancel', style: 'cancel' },
          {
            text: 'Confirm',
            onPress: () => {
              onPaymentMethodSelect(selectedMethod, amount);
              handleClose();
            }
          }
        ]
      );
    }
  };

  const handleClose = () => {
    setSelectedMethod(null);
    setPaymentAmount('');
    setShowAmountInput(false);
    setChangeAmount(0);
    onClose();
  };

  const handleQuickAmount = (multiplier: number) => {
    const amount = totalAmount * multiplier;
    setPaymentAmount(amount.toFixed(2));
  };

  const renderPaymentMethod = (method: PaymentMethod) => (
    <TouchableOpacity
      key={method.id}
      style={[
        styles.paymentMethodButton,
        { borderColor: method.color },
        selectedMethod?.id === method.id && { backgroundColor: method.color + '20' }
      ]}
      onPress={() => handleMethodSelect(method)}
    >
      <View style={styles.paymentMethodHeader}>
        <View style={[styles.paymentMethodIcon, { backgroundColor: method.color }]}>
          <Ionicons name={method.icon as any} size={24} color="white" />
        </View>
        <View style={styles.paymentMethodInfo}>
          <Text style={styles.paymentMethodName}>{method.name}</Text>
          <Text style={styles.paymentMethodDescription}>{method.description}</Text>
        </View>
        <Ionicons 
          name="chevron-forward" 
          size={20} 
          color={Colors.light.textSecondary} 
        />
      </View>
    </TouchableOpacity>
  );

  return (
    <Modal
      visible={visible}
      animationType="slide"
      onRequestClose={handleClose}
    >
      <View style={styles.container}>
        {/* Header */}
        <View style={styles.header}>
          <TouchableOpacity style={styles.closeButton} onPress={handleClose}>
            <Ionicons name="close" size={24} color="white" />
          </TouchableOpacity>
          <Text style={styles.headerTitle}>{t('payment.options', 'Payment Options')}</Text>
          <View style={styles.totalContainer}>
            <Text style={styles.totalLabel}>{t('payment.total', 'Total')}</Text>
            <Text style={styles.totalAmount}>€{totalAmount.toFixed(2)}</Text>
          </View>
        </View>

        {/* Payment Methods */}
        <ScrollView style={styles.content} showsVerticalScrollIndicator={false}>
          <View style={styles.paymentMethods}>
            {PAYMENT_METHODS.map(renderPaymentMethod)}
          </View>

          {/* Amount Input */}
          {showAmountInput && selectedMethod && (
            <View style={styles.amountSection}>
              <Text style={styles.amountTitle}>
                {t('payment.enterAmount', 'Enter')} {selectedMethod.name} {t('payment.amount', 'Amount')}
              </Text>
              
              <View style={styles.amountInputContainer}>
                <Text style={styles.currencySymbol}>€</Text>
                <TextInput
                  style={styles.amountInput}
                  value={paymentAmount}
                  onChangeText={setPaymentAmount}
                  keyboardType="numeric"
                  placeholder="0.00"
                  autoFocus
                />
              </View>

              {/* Quick Amount Buttons */}
              <View style={styles.quickAmounts}>
                <Text style={styles.quickAmountsTitle}>{t('payment.quickAmounts', 'Quick Amounts:')}</Text>
                <View style={styles.quickAmountButtons}>
                  {[1, 1.5, 2, 5, 10].map(multiplier => (
                    <TouchableOpacity
                      key={multiplier}
                      style={styles.quickAmountButton}
                      onPress={() => handleQuickAmount(multiplier)}
                    >
                      <Text style={styles.quickAmountText}>
                        {multiplier === 1 ? t('payment.exact', 'Exact') : `${multiplier}x`}
                      </Text>
                    </TouchableOpacity>
                  ))}
                </View>
              </View>

              {/* Change Calculation */}
              {paymentAmount && parseFloat(paymentAmount) > totalAmount && (
                <View style={styles.changeContainer}>
                  <Text style={styles.changeLabel}>{t('payment.change', 'Change:')}</Text>
                  <Text style={styles.changeAmount}>
                    €{(parseFloat(paymentAmount) - totalAmount).toFixed(2)}
                  </Text>
                </View>
              )}

              {/* Confirm Button */}
              <TouchableOpacity
                style={[
                  styles.confirmButton,
                  (!paymentAmount || parseFloat(paymentAmount) < totalAmount) && 
                  styles.confirmButtonDisabled
                ]}
                onPress={handleAmountConfirm}
                disabled={!paymentAmount || parseFloat(paymentAmount) < totalAmount}
              >
                <Text style={styles.confirmButtonText}>{t('payment.confirm', 'Confirm Payment')}</Text>
              </TouchableOpacity>
            </View>
          )}
        </ScrollView>

        {/* Payment Tips */}
        <View style={styles.tipsContainer}>
          <Text style={styles.tipsTitle}>{t('payment.tipsTitle', 'Payment Tips:')}</Text>
          <Text style={styles.tipsText}>
            • {t('payment.tipCash', 'Cash payments require exact amount entry')}{'\n'}
            • {t('payment.tipCard', 'Card payments are processed automatically')}{'\n'}
            • {t('payment.tipSplit', 'Split payments can be managed separately')}{'\n'}
            • {t('payment.tipRKSV', 'All payments are logged for RKSV compliance')}
          </Text>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: Colors.light.background,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.lg,
    paddingTop: Spacing.xl,
    backgroundColor: Colors.light.primary,
  },
  closeButton: {
    padding: Spacing.sm,
  },
  headerTitle: {
    ...Typography.h3,
    color: 'white',
    fontWeight: '600',
  },
  totalContainer: {
    alignItems: 'center',
  },
  totalLabel: {
    color: 'white',
    fontSize: 12,
    opacity: 0.8,
  },
  totalAmount: {
    color: 'white',
    fontSize: 18,
    fontWeight: 'bold',
  },
  content: {
    flex: 1,
    padding: Spacing.lg,
  },
  paymentMethods: {
    gap: Spacing.md,
  },
  paymentMethodButton: {
    borderWidth: 2,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    backgroundColor: Colors.light.surface,
  },
  paymentMethodHeader: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  paymentMethodIcon: {
    width: 48,
    height: 48,
    borderRadius: 24,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: Spacing.md,
  },
  paymentMethodInfo: {
    flex: 1,
  },
  paymentMethodName: {
    fontSize: 16,
    fontWeight: '600',
    color: Colors.light.text,
    marginBottom: Spacing.xs,
  },
  paymentMethodDescription: {
    ...Typography.bodySmall,
    color: Colors.light.textSecondary,
  },
  amountSection: {
    marginTop: Spacing.xl,
    padding: Spacing.lg,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.lg,
  },
  amountTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: Colors.light.text,
    marginBottom: Spacing.md,
    textAlign: 'center',
  },
  amountInputContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.md,
    paddingHorizontal: Spacing.md,
    marginBottom: Spacing.lg,
  },
  currencySymbol: {
    fontSize: 24,
    color: Colors.light.text,
    marginRight: Spacing.sm,
  },
  amountInput: {
    flex: 1,
    fontSize: 24,
    color: Colors.light.text,
    paddingVertical: Spacing.md,
  },
  quickAmounts: {
    marginBottom: Spacing.lg,
  },
  quickAmountsTitle: {
    ...Typography.body,
    color: Colors.light.text,
    marginBottom: Spacing.sm,
  },
  quickAmountButtons: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: Spacing.sm,
  },
  quickAmountButton: {
    backgroundColor: Colors.light.primary,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    borderRadius: BorderRadius.md,
  },
  quickAmountText: {
    color: 'white',
    fontSize: 14,
    fontWeight: '500',
  },
  changeContainer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.md,
    backgroundColor: Colors.light.success + '20',
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.lg,
  },
  changeLabel: {
    ...Typography.body,
    color: Colors.light.text,
  },
  changeAmount: {
    fontSize: 16,
    fontWeight: 'bold',
    color: Colors.light.success,
  },
  confirmButton: {
    backgroundColor: Colors.light.primary,
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    alignItems: 'center',
  },
  confirmButtonDisabled: {
    backgroundColor: Colors.light.border,
  },
  confirmButtonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: '600',
  },
  tipsContainer: {
    padding: Spacing.lg,
    backgroundColor: Colors.light.surface,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
  },
  tipsTitle: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: '600',
    marginBottom: Spacing.sm,
  },
  tipsText: {
    ...Typography.bodySmall,
    color: Colors.light.textSecondary,
    lineHeight: 18,
  },
});

export default AdvancedPaymentOptions; 