// Professional POS cart summary with subtotal, tax, and grand total breakdown
import React, { useMemo } from 'react';
import { View, Text, Pressable, StyleSheet, ActivityIndicator } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftShadows } from '../constants/SoftTheme';
import { formatPrice } from '../utils/formatPrice';
import { calculateCartTotals } from '../contexts/CartContext';

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
  // Calculate totals from items
  const totals = useMemo(() => {
    const items = cart?.items ?? [];
    return calculateCartTotals(items);
  }, [cart?.items, cart?.updatedAt]);

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
            {formatPrice(totals.subtotal)}
          </Text>
        </View>

        {/* Tax */}
        <View style={styles.row}>
          <Text style={styles.label}>MwSt. (20%)</Text>
          <Text style={styles.value}>
            {formatPrice(totals.tax)}
          </Text>
        </View>

        {/* Divider */}
        <View style={styles.divider} />

        {/* Grand Total */}
        <View style={[styles.row, styles.totalRow]}>
          <Text style={styles.totalLabel}>GESAMT</Text>
          <Text style={styles.totalValue}>
            {formatPrice(totals.grandTotal)}
          </Text>
        </View>
      </View>

      {/* Payment Button (if onPayment provided) */}
      {onPayment && (
        <Pressable
          style={({ pressed }) => [
            styles.payButton,
            isDisabled && styles.payButtonDisabled,
            pressed && !isDisabled && styles.payButtonPressed,
          ]}
          onPress={onPayment}
          disabled={isDisabled}
        >
          {isDisabled ? (
            <View style={styles.payContent}>
              <ActivityIndicator size="small" color={SoftColors.textInverse} />
              <Text style={styles.payText}>Verarbeitung...</Text>
            </View>
          ) : (
            <View style={styles.payContent}>
              <Text style={styles.payText}>
                Bezahlen {formatPrice(totals.grandTotal)}
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
    backgroundColor: '#F9FAFB',
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.md,
    borderTopWidth: 2,
    borderTopColor: '#E5E7EB',
  },
  breakdown: {
    marginBottom: SoftSpacing.md,
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  label: {
    fontSize: 13,
    color: '#6B7280',
  },
  value: {
    fontSize: 13,
    fontWeight: '600',
    color: '#1F2937',
  },
  divider: {
    height: 1,
    backgroundColor: '#D1D5DB',
    marginVertical: 12,
  },
  totalRow: {
    backgroundColor: '#ECFDF5', // Light green bg
    padding: 12,
    borderRadius: SoftRadius.md,
    marginTop: 4,
    marginBottom: 0,
  },
  totalLabel: {
    fontSize: 16,
    fontWeight: '700',
    color: '#065F46',
  },
  totalValue: {
    fontSize: 18,
    fontWeight: '700',
    color: '#059669',
  },
  payButton: {
    backgroundColor: SoftColors.accent,
    paddingVertical: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    alignItems: 'center',
    ...SoftShadows.sm,
  },
  payButtonDisabled: {
    backgroundColor: SoftColors.textMuted,
  },
  payButtonPressed: {
    opacity: 0.9,
  },
  payContent: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
  },
  payText: {
    fontSize: 15,
    fontWeight: '700',
    color: SoftColors.textInverse,
  },
});
