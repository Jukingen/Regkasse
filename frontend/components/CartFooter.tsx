// Türkçe Açıklama: Sepet alt bilgi (footer) component'i. Toplam, KDV ve servis bedelini gerçek zamanlı gösterir.
import React from 'react';
import { View, Text, StyleSheet } from 'react-native';

interface CartFooterProps {
  subtotal: number;
  vat: number;
  serviceFee: number;
  grandTotal: number;
}

const CartFooter: React.FC<CartFooterProps> = ({ subtotal, vat, serviceFee, grandTotal }) => {
  return (
    <View style={styles.footerBox}>
      <View style={styles.row}>
        <Text style={styles.label}>Ara Toplam:</Text>
        <Text style={styles.value}>{subtotal.toFixed(2)} €</Text>
      </View>
      <View style={styles.row}>
        <Text style={styles.label}>KDV:</Text>
        <Text style={styles.value}>{vat.toFixed(2)} €</Text>
      </View>
      <View style={styles.row}>
        <Text style={styles.label}>Servis Bedeli:</Text>
        <Text style={styles.value}>{serviceFee.toFixed(2)} €</Text>
      </View>
      <View style={styles.rowTotal}>
        <Text style={styles.totalLabel}>Genel Toplam:</Text>
        <Text style={styles.totalValue}>{grandTotal.toFixed(2)} €</Text>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  footerBox: {
    backgroundColor: '#f5f5f5',
    borderTopWidth: 1,
    borderColor: '#e0e0e0',
    padding: 14,
    borderRadius: 12,
    marginTop: 10,
    marginBottom: 4,
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 4,
  },
  rowTotal: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginTop: 8,
    borderTopWidth: 1,
    borderColor: '#ccc',
    paddingTop: 8,
  },
  label: {
    fontSize: 16,
    color: '#444',
  },
  value: {
    fontSize: 16,
    color: '#222',
    fontWeight: 'bold',
  },
  totalLabel: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#1976d2',
  },
  totalValue: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#1976d2',
  },
});

export default CartFooter; 