import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  TextInput,
  Alert,
  ScrollView,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';

interface ChangeCalculatorProps {
  total: number;
  onAmountChange: (amount: number) => void;
  onCalculateChange: (change: number) => void;
  onConfirm: () => void;
  visible: boolean;
  onClose: () => void;
}

const ChangeCalculator: React.FC<ChangeCalculatorProps> = ({
  total,
  onAmountChange,
  onCalculateChange,
  onConfirm,
  visible,
  onClose,
}) => {
  const { t } = useTranslation();
  const [amount, setAmount] = useState('');
  const [change, setChange] = useState(0);
  const [quickAmounts, setQuickAmounts] = useState<number[]>([]);

  // Hızlı tutar butonları için tutarları hesapla
  useEffect(() => {
    const amounts = [];
    const roundedTotal = Math.ceil(total);
    
    // Toplam tutarın üzerindeki yuvarlak tutarlar
    amounts.push(roundedTotal);
    amounts.push(roundedTotal + 5);
    amounts.push(roundedTotal + 10);
    amounts.push(roundedTotal + 20);
    amounts.push(roundedTotal + 50);
    amounts.push(roundedTotal + 100);
    
    setQuickAmounts(amounts);
  }, [total]);

  // Tutar değiştiğinde para üstünü hesapla
  useEffect(() => {
    const numAmount = parseFloat(amount) || 0;
    const calculatedChange = numAmount - total;
    setChange(calculatedChange >= 0 ? calculatedChange : 0);
    onAmountChange(numAmount);
    onCalculateChange(calculatedChange >= 0 ? calculatedChange : 0);
  }, [amount, total, onAmountChange, onCalculateChange]);

  // Hızlı tutar seçimi
  const handleQuickAmount = (quickAmount: number) => {
    setAmount(quickAmount.toString());
  };

  // Para üstü dağılımını hesapla
  const calculateChangeBreakdown = (changeAmount: number) => {
    const breakdown = {
      '500': 0,
      '200': 0,
      '100': 0,
      '50': 0,
      '20': 0,
      '10': 0,
      '5': 0,
      '2': 0,
      '1': 0,
      '0.50': 0,
      '0.20': 0,
      '0.10': 0,
      '0.05': 0,
      '0.02': 0,
      '0.01': 0,
    };

    let remaining = changeAmount;
    const denominations = [500, 200, 100, 50, 20, 10, 5, 2, 1, 0.5, 0.2, 0.1, 0.05, 0.02, 0.01];

    for (const denom of denominations) {
      if (remaining >= denom) {
        const count = Math.floor(remaining / denom);
        breakdown[denom.toString() as keyof typeof breakdown] = count;
        remaining = Math.round((remaining - (count * denom)) * 100) / 100;
      }
    }

    return breakdown;
  };

  const changeBreakdown = calculateChangeBreakdown(change);

  return (
    <View style={[styles.container, { display: visible ? 'flex' : 'none' }]}>
      <View style={styles.header}>
        <Text style={styles.title}>{t('change.calculator', 'Para Üstü Hesaplama')}</Text>
        <TouchableOpacity onPress={onClose} style={styles.closeButton}>
          <Ionicons name="close" size={24} color={Colors.light.text} />
        </TouchableOpacity>
      </View>

      <ScrollView style={styles.content} showsVerticalScrollIndicator={false}>
        {/* Toplam Tutar */}
        <View style={styles.totalSection}>
          <Text style={styles.totalLabel}>{t('change.total_amount', 'Toplam Tutar')}</Text>
          <Text style={styles.totalAmount}>{total.toFixed(2)}€</Text>
        </View>

        {/* Müşteri Verdiği Tutar */}
        <View style={styles.amountSection}>
          <Text style={styles.sectionTitle}>{t('change.customer_amount', 'Müşteri Verdiği Tutar')}</Text>
          <TextInput
            style={styles.amountInput}
            value={amount}
            onChangeText={setAmount}
            keyboardType="numeric"
            placeholder="0.00"
            placeholderTextColor={Colors.light.textSecondary}
          />
        </View>

        {/* Hızlı Tutar Butonları */}
        <View style={styles.quickAmountsSection}>
          <Text style={styles.sectionTitle}>{t('change.quick_amounts', 'Hızlı Tutar Seçenekleri')}</Text>
          <View style={styles.quickAmountsGrid}>
            {quickAmounts.map((quickAmount) => (
              <TouchableOpacity
                key={quickAmount}
                style={[
                  styles.quickAmountButton,
                  parseFloat(amount) === quickAmount && styles.quickAmountButtonSelected,
                ]}
                onPress={() => handleQuickAmount(quickAmount)}
              >
                <Text
                  style={[
                    styles.quickAmountText,
                    parseFloat(amount) === quickAmount && styles.quickAmountTextSelected,
                  ]}
                >
                  {quickAmount.toFixed(2)}€
                </Text>
              </TouchableOpacity>
            ))}
          </View>
        </View>

        {/* Para Üstü */}
        <View style={styles.changeSection}>
          <Text style={styles.sectionTitle}>{t('change.change_amount', 'Para Üstü')}</Text>
          <Text style={[styles.changeAmount, change < 0 && styles.changeAmountNegative]}>
            {change >= 0 ? '+' : ''}{change.toFixed(2)}€
          </Text>
        </View>

        {/* Para Üstü Dağılımı */}
        {change > 0 && (
          <View style={styles.breakdownSection}>
            <Text style={styles.sectionTitle}>{t('change.breakdown', 'Para Üstü Dağılımı')}</Text>
            <View style={styles.breakdownGrid}>
              {Object.entries(changeBreakdown)
                .filter(([_, count]) => count > 0)
                .map(([denom, count]) => (
                  <View key={denom} style={styles.breakdownItem}>
                    <Text style={styles.denominationText}>{denom}€</Text>
                    <Text style={styles.countText}>x{count}</Text>
                  </View>
                ))}
            </View>
          </View>
        )}

        {/* Uyarı */}
        {change < 0 && (
          <View style={styles.warningSection}>
            <Ionicons name="warning" size={20} color={Colors.light.error} />
            <Text style={styles.warningText}>{t('change.insufficient_amount', 'Yetersiz Tutar')}</Text>
          </View>
        )}
      </ScrollView>

      {/* Onay Butonu */}
      <View style={styles.footer}>
        <TouchableOpacity
          style={[
            styles.confirmButton,
            (change < 0 || !amount) && styles.confirmButtonDisabled,
          ]}
          onPress={onConfirm}
          disabled={change < 0 || !amount}
        >
          <Ionicons name="checkmark" size={20} color="white" />
          <Text style={styles.confirmButtonText}>{t('change.confirm', 'Onayla')}</Text>
        </TouchableOpacity>
      </View>
    </View>
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
    padding: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  title: {
    ...Typography.h2,
    color: Colors.light.text,
  },
  closeButton: {
    padding: Spacing.xs,
  },
  content: {
    flex: 1,
    padding: Spacing.md,
  },
  totalSection: {
    alignItems: 'center',
    padding: Spacing.lg,
    backgroundColor: Colors.light.primary,
    borderRadius: BorderRadius.lg,
    marginBottom: Spacing.md,
  },
  totalLabel: {
    ...Typography.body,
    color: 'white',
    marginBottom: Spacing.xs,
  },
  totalAmount: {
    ...Typography.h1,
    color: 'white',
    fontWeight: 'bold',
  },
  amountSection: {
    marginBottom: Spacing.lg,
  },
  sectionTitle: {
    ...Typography.h3,
    color: Colors.light.text,
    marginBottom: Spacing.sm,
  },
  amountInput: {
    borderWidth: 2,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    fontSize: 24,
    textAlign: 'center',
    fontWeight: 'bold',
    color: Colors.light.text,
  },
  quickAmountsSection: {
    marginBottom: Spacing.lg,
  },
  quickAmountsGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: Spacing.sm,
  },
  quickAmountButton: {
    flex: 1,
    minWidth: '30%',
    padding: Spacing.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.md,
    alignItems: 'center',
  },
  quickAmountButtonSelected: {
    backgroundColor: Colors.light.primary,
    borderColor: Colors.light.primary,
  },
  quickAmountText: {
    ...Typography.body,
    color: Colors.light.text,
  },
  quickAmountTextSelected: {
    color: 'white',
    fontWeight: 'bold',
  },
  changeSection: {
    alignItems: 'center',
    padding: Spacing.lg,
    backgroundColor: Colors.light.success,
    borderRadius: BorderRadius.lg,
    marginBottom: Spacing.lg,
  },
  changeAmount: {
    ...Typography.h1,
    color: 'white',
    fontWeight: 'bold',
  },
  changeAmountNegative: {
    color: Colors.light.error,
  },
  breakdownSection: {
    marginBottom: Spacing.lg,
  },
  breakdownGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: Spacing.sm,
  },
  breakdownItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.sm,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.sm,
    minWidth: '45%',
  },
  denominationText: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: 'bold',
    marginRight: Spacing.xs,
  },
  countText: {
    ...Typography.body,
    color: Colors.light.textSecondary,
  },
  warningSection: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    backgroundColor: '#FFEBEE',
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.lg,
  },
  warningText: {
    ...Typography.body,
    color: Colors.light.error,
    marginLeft: Spacing.sm,
  },
  footer: {
    padding: Spacing.md,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
  },
  confirmButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.primary,
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    gap: Spacing.sm,
  },
  confirmButtonDisabled: {
    backgroundColor: '#CCCCCC',
  },
  confirmButtonText: {
    ...Typography.button,
    color: 'white',
    fontWeight: 'bold',
  },
});

export default ChangeCalculator; 