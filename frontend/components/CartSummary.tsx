// Türkçe Açıklama: Sepet özeti ve ödeme butonu için ayrı component
// Karmaşık cash-register.tsx dosyasından ödeme logic'ini ayırır

import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';

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
  if (loading || error || !cart || !cart.items || cart.items.length === 0) {
    return null;
  }

  const subtotal = cart.subtotal || cart.grandTotal || 0;
  const totalTax = cart.totalTax || (subtotal * 0.2);
  const grandTotal = cart.grandTotal || (subtotal + totalTax);

  return (
    <>
      {/* Cart Summary */}
      <View style={styles.summarySection}>
        <View style={styles.summaryRow}>
          <Text style={styles.summaryLabel}>Subtotal:</Text>
          <Text style={styles.summaryValue}>€{subtotal.toFixed(2)}</Text>
        </View>
        <View style={styles.summaryRow}>
          <Text style={styles.summaryLabel}>Tax (20%):</Text>
          <Text style={styles.summaryValue}>€{totalTax.toFixed(2)}</Text>
        </View>
        <View style={styles.summaryRow}>
          <Text style={styles.summaryLabel}>Total:</Text>
          <Text style={styles.summaryValue}>€{grandTotal.toFixed(2)}</Text>
        </View>
      </View>

      {/* New Order Status Indicator */}
      {(!cart || !cart.items || cart.items.length === 0) && (
        <View style={styles.newOrderSection}>
          <View style={styles.newOrderStatus}>
            <Text style={styles.newOrderTitle}>🆕 New Order Ready</Text>
            <Text style={styles.newOrderSubtitle}>Table is ready for new items</Text>
            <Text style={styles.newOrderInfo}>Previous order completed successfully</Text>
          </View>
        </View>
      )}

      {/* Payment Button */}
      <View style={styles.paymentButtonContainer}>
        <TouchableOpacity
          style={[
            styles.paymentButton,
            (paymentProcessing || preventDoubleClick) && styles.paymentButtonDisabled
          ]}
          onPress={onPayment}
          disabled={paymentProcessing || preventDoubleClick}
          activeOpacity={paymentProcessing || preventDoubleClick ? 1.0 : 0.8}
        >
          <View style={styles.paymentButtonContent}>
            {(paymentProcessing || preventDoubleClick) && (
              <View style={styles.loadingSpinner}>
                <Text style={styles.spinnerText}>⏳</Text>
              </View>
            )}
            <Text style={styles.paymentButtonText}>
              {paymentProcessing ? 'Processing Payment...' : 
               preventDoubleClick ? 'Payment in Progress...' : 
               `Complete Payment - €${grandTotal.toFixed(2)}`}
            </Text>
          </View>
        </TouchableOpacity>

        {/* Error Display */}
        {error && (
          <View style={styles.errorContainer}>
            <Text style={styles.errorText}>{error}</Text>
          </View>
        )}
      </View>
    </>
  );
};

const styles = StyleSheet.create({
  summarySection: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 10,
  },
  summaryRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 10,
  },
  summaryLabel: {
    fontSize: 16,
    color: '#666',
  },
  summaryValue: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
  },
  newOrderSection: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 10,
    borderRadius: 5,
    borderLeftWidth: 4,
    borderLeftColor: '#4CAF50',
  },
  newOrderStatus: {
    alignItems: 'center',
  },
  newOrderTitle: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#4CAF50',
    marginBottom: 5,
  },
  newOrderSubtitle: {
    fontSize: 16,
    color: '#666',
    marginBottom: 10,
  },
  newOrderInfo: {
    fontSize: 14,
    color: '#999',
  },
  paymentButtonContainer: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 20,
    alignItems: 'center',
  },
  paymentButton: {
    backgroundColor: '#4CAF50',
    paddingVertical: 15,
    borderRadius: 5,
    alignItems: 'center',
    marginBottom: 15,
    minWidth: 200,
  },
  paymentButtonDisabled: {
    backgroundColor: '#ccc',
  },
  paymentButtonContent: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  loadingSpinner: {
    marginRight: 10,
  },
  spinnerText: {
    fontSize: 20,
  },
  paymentButtonText: {
    color: '#fff',
    fontSize: 18,
    fontWeight: 'bold',
  },
  errorContainer: {
    backgroundColor: '#ffebee',
    padding: 15,
    borderRadius: 5,
    borderLeftWidth: 4,
    borderLeftColor: '#f44336',
    width: '100%',
  },
  errorText: {
    color: '#f44336',
    fontSize: 14,
  },
});
