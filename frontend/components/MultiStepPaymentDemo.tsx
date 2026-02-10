// Türkçe Açıklama: Multi-step ödeme ekranını test etmek için demo bileşeni

import React, { useState } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Modal } from 'react-native';
import { useTranslation } from 'react-i18next';
import MultiStepPaymentScreen from './MultiStepPaymentScreen';

const MultiStepDemo: React.FC = () => {
  const { t } = useTranslation();
  const [showPayment, setShowPayment] = useState(false);
  const [cartItems] = useState([
    {
      id: '1',
      product: {
        id: 'prod-1',
        name: 'Test Ürün 1',
        price: 15.50,
        taxType: 'Standard'
      },
      quantity: 2,
      unitPrice: 15.50,
      totalAmount: 31.00
    },
    {
      id: '2',
      product: {
        id: 'prod-2',
        name: 'Test Ürün 2',
        price: 8.75,
        taxType: 'Reduced'
      },
      quantity: 1,
      unitPrice: 8.75,
      totalAmount: 8.75
    }
  ]);

  const totalAmount = cartItems.reduce((sum, item) => sum + item.totalAmount, 0);

  const handlePaymentComplete = (receipt: any) => {
    console.log('Ödeme tamamlandı:', receipt);
    setShowPayment(false);
    // Burada başarılı ödeme sonrası işlemler yapılabilir
  };

  const handlePaymentCancel = () => {
    console.log('Ödeme iptal edildi');
    setShowPayment(false);
  };

  const handlePaymentCancelled = (response: any) => {
    console.log('Ödeme iptal yanıtı:', response);
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>{t('payment.title')} Demo</Text>

      <View style={styles.cartSummary}>
        <Text style={styles.summaryTitle}>{t('cashRegister.cart')}:</Text>
        {cartItems.map(item => (
          <View key={item.id} style={styles.cartItem}>
            <Text style={styles.itemName}>{item.product.name}</Text>
            <Text style={styles.itemDetails}>
              {item.quantity} x {item.unitPrice.toFixed(2)} € = {item.totalAmount.toFixed(2)} €
            </Text>
          </View>
        ))}
        <View style={styles.totalRow}>
          <Text style={styles.totalLabel}>{t('cashRegister.total')}:</Text>
          <Text style={styles.totalAmount}>{totalAmount.toFixed(2)} €</Text>
        </View>
      </View>

      <TouchableOpacity
        style={styles.paymentButton}
        onPress={() => setShowPayment(true)}
      >
        <Text style={styles.buttonText}>{t('cashRegister.checkout')}</Text>
      </TouchableOpacity>

      <Modal
        visible={showPayment}
        animationType="slide"
        presentationStyle="pageSheet"
      >
        <MultiStepPaymentScreen
          totalAmount={totalAmount}
          cartItems={cartItems}
          onComplete={handlePaymentComplete}
          onCancel={handlePaymentCancel}
          onPaymentCancelled={handlePaymentCancelled}
          tableNumber={1}
        />
      </Modal>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 20,
    backgroundColor: '#f5f5f5',
  },
  title: {
    fontSize: 24,
    fontWeight: 'bold',
    textAlign: 'center',
    marginBottom: 30,
    color: '#333',
  },
  cartSummary: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 20,
    marginBottom: 30,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  summaryTitle: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: 15,
    color: '#333',
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
    fontSize: 16,
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
    borderTopWidth: 2,
    borderTopColor: '#e0e0e0',
  },
  totalLabel: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
  },
  totalAmount: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#1976d2',
  },
  paymentButton: {
    backgroundColor: '#28a745',
    padding: 16,
    borderRadius: 8,
    alignItems: 'center',
  },
  buttonText: {
    color: '#fff',
    fontSize: 18,
    fontWeight: '600',
  },
});

export default MultiStepDemo;
