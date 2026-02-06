// Compact cart summary - minimal footprint for POS speed
import React from 'react';
import { View, Text, Pressable, StyleSheet, ActivityIndicator } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftShadows } from '../constants/SoftTheme';

interface CartSummaryProps {
  cart: any;
  loading: boolean;
  error: string | null;
  paymentProcessing: boolean;
  preventDoubleClick: boolean;
  onPayment: () => void;
}

export const CartSummary: React.FC<CartSummaryProps> = ({
  cart,
  loading,
  error,
  paymentProcessing,
  preventDoubleClick,
  onPayment,
}) => {
  // Hide if no items
  if (loading || error || !cart || !cart.items || cart.items.length === 0) {
    return null;
  }

  const grandTotal = cart.grandTotal || 0;
  const isDisabled = paymentProcessing || preventDoubleClick;

  return (
    <View style={styles.container}>
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
            <Text style={styles.payText}>Processing...</Text>
          </View>
        ) : (
          <View style={styles.payContent}>
            <Text style={styles.payText}>Pay €{grandTotal.toFixed(2)}</Text>
          </View>
        )}
      </Pressable>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    paddingHorizontal: SoftSpacing.sm,
    paddingBottom: SoftSpacing.md,
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
