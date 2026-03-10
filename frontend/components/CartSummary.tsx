// Professional POS cart summary with subtotal, tax, and grand total breakdown
import React, { useMemo } from 'react';
import { View, Text, Pressable, StyleSheet, ActivityIndicator } from 'react-native';
import { SoftColors, SoftRadius, SoftShadows, SoftSpacing, SoftState, SoftTypography } from '../constants/SoftTheme';
import { formatPrice } from '../utils/formatPrice';
import { getCartDisplayTotals } from '../contexts/CartContext';

interface CartSummaryProps {
  cart: any;
  loading: boolean;
  error: string | null;
  paymentProcessing?: boolean;
  preventDoubleClick?: boolean;
  onPayment?: () => void;
}

export const CartSummary: React.FC<CartSummaryProps> = ({
  cart,
  loading,
  error,
  paymentProcessing = false,
  preventDoubleClick = false,
  onPayment,
}) => {
  const totals = useMemo(() => getCartDisplayTotals(cart), [cart]);

  // Hide if no items
  if (loading || error || !cart || !cart.items || cart.items.length === 0) {
    return null;
  }

  const isDisabled = paymentProcessing || preventDoubleClick;

  return (
    <View style={styles.container}>
      {/* Breakdown Section */}
      <View style={styles.breakdown}>
        {/* Subtotal */}
        <View style={styles.row}>
          <Text style={styles.label}>
            Zwischensumme ({totals.itemCount} Artikel)
          </Text>
          <Text style={styles.value}>
            {formatPrice(totals.subtotalGross)}
          </Text>
        </View>

        {/* Tax (embedded in gross - backend'den, FE hesaplamaz) */}
        <View style={styles.row}>
          <Text style={styles.label}>MwSt.</Text>
          <Text style={styles.value}>
            {formatPrice(totals.includedTaxTotal)}
          </Text>
        </View>

        {/* Divider */}
        <View style={styles.divider} />

        {/* Grand Total */}
        <View style={[styles.row, styles.totalRow]}>
          <Text style={styles.totalLabel}>GESAMT</Text>
          <Text style={styles.totalValue}>
            {formatPrice(totals.grandTotalGross)}
          </Text>
        </View>
      </View>

      {/* Payment Button (if onPayment provided) */}
      {onPayment && (
        <Pressable
          style={({ pressed, focused }) => [
            styles.payButton,
            isDisabled && styles.payButtonDisabled,
            pressed && !isDisabled && styles.payButtonPressed,
            focused && !isDisabled && SoftState.focusVisible,
          ]}
          onPress={onPayment}
          disabled={isDisabled}
          hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
          accessibilityLabel={`Bezahlen ${formatPrice(totals.grandTotalGross)}`}
          accessibilityRole="button"
          accessibilityState={{ disabled: isDisabled }}
        >
          {isDisabled ? (
            <View style={styles.payContent}>
              <ActivityIndicator size="small" color={SoftColors.textInverse} />
              <Text style={styles.payText}>Verarbeitung...</Text>
            </View>
          ) : (
            <View style={styles.payContent}>
              <Text style={styles.payText}>
                Bezahlen {formatPrice(totals.grandTotalGross)}
              </Text>
            </View>
          )}
        </Pressable>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: SoftColors.bgSecondary,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.md,
    borderTopWidth: 1,
    borderTopColor: SoftColors.borderLight,
  },
  breakdown: {
    marginBottom: SoftSpacing.md,
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: SoftSpacing.sm,
  },
  label: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textSecondary,
  },
  value: {
    ...SoftTypography.bodySmall,
    fontWeight: '600',
    color: SoftColors.textPrimary,
  },
  divider: {
    height: 1,
    backgroundColor: SoftColors.borderLight,
    marginVertical: SoftSpacing.sm,
  },
  totalRow: {
    backgroundColor: SoftColors.successBg,
    padding: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    marginTop: SoftSpacing.xs,
    marginBottom: 0,
  },
  totalLabel: {
    ...SoftTypography.h3,
    color: SoftColors.success,
  },
  totalValue: {
    ...SoftTypography.priceTotal,
    color: SoftColors.success,
  },
  payButton: {
    backgroundColor: SoftColors.accent,
    paddingVertical: SoftSpacing.md,
    minHeight: 48,
    justifyContent: 'center',
    borderRadius: SoftRadius.md,
    alignItems: 'center',
    ...SoftShadows.sm,
  },
  payButtonDisabled: {
    backgroundColor: SoftColors.textMuted,
  },
  payButtonPressed: SoftState.pressedScale,
  payContent: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
  },
  payText: {
    ...SoftTypography.body,
    fontWeight: '700',
    color: SoftColors.textInverse,
  },
});
