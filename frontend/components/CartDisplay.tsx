// Türkçe Açıklama: Sepet gösterimi ve sepet yönetimi için ayrı component
// Karmaşık cash-register.tsx dosyasından sepet logic'ini ayırır

import React from 'react';
import { View, Text, TouchableOpacity, ScrollView, StyleSheet } from 'react-native';

interface CartItem {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

interface CartDisplayProps {
  cart: any;
  selectedTable: number;
  loading: boolean;
  error: string | null;
  onQuantityUpdate: (itemId: string, newQuantity: number) => void;
  onItemRemove: (itemId: string) => void;
  onClearCart: () => void;
}

export const CartDisplay: React.FC<CartDisplayProps> = ({
  cart,
  selectedTable,
  loading,
  error,
  onQuantityUpdate,
  onItemRemove,
  onClearCart,
}) => {
  if (loading) {
    return (
      <View style={styles.cartSection}>
        <View style={styles.cartHeader}>
          <Text style={styles.sectionTitle}>Cart Items - Table {selectedTable}</Text>
        </View>
        <View style={styles.loadingContainer}>
          <Text style={styles.loadingText}>Loading cart...</Text>
        </View>
      </View>
    );
  }

  if (error) {
    return (
      <View style={styles.cartSection}>
        <View style={styles.cartHeader}>
          <Text style={styles.sectionTitle}>Cart Items - Table {selectedTable}</Text>
        </View>
        <View style={styles.errorContainer}>
          <Text style={styles.errorText}>Cart error: {error}</Text>
        </View>
      </View>
    );
  }

  if (!cart || !cart.items || cart.items.length === 0) {
    return (
      <View style={styles.cartSection}>
        <View style={styles.cartHeader}>
          <Text style={styles.sectionTitle}>Cart Items - Table {selectedTable}</Text>
        </View>
        <View style={styles.emptyCart}>
          <Text style={styles.emptyCartText}>No items in cart for table {selectedTable}</Text>
          <Text style={styles.emptyCartSubtext}>Select a table and add items to get started</Text>
        </View>
      </View>
    );
  }

  return (
    <View style={styles.cartSection}>
      <View style={styles.cartHeader}>
        <Text style={styles.sectionTitle}>Cart Items - Table {selectedTable}</Text>
        <TouchableOpacity onPress={onClearCart} style={styles.clearButton}>
          <Text style={styles.clearButtonText}>Clear Table {selectedTable}</Text>
        </TouchableOpacity>
      </View>

      <ScrollView style={styles.cartItems}>
        {cart.items.map((item: CartItem) => (
          <View key={`${item.id}-${item.quantity}-${selectedTable}`} style={styles.cartItem}>
            <View style={styles.itemInfo}>
              <Text style={styles.itemName}>{item.productName}</Text>
              <Text style={styles.itemPrice}>€{(item.unitPrice || 0).toFixed(2)}</Text>
            </View>
            
            <View style={styles.itemActions}>
              <TouchableOpacity
                style={styles.quantityButton}
                onPress={() => onQuantityUpdate(item.id, item.quantity - 1)}
              >
                <Text style={styles.quantityButtonText}>-</Text>
              </TouchableOpacity>
              
              <Text style={styles.quantityText}>{item.quantity}</Text>
              
              <TouchableOpacity
                style={styles.quantityButton}
                onPress={() => onQuantityUpdate(item.id, item.quantity + 1)}
              >
                <Text style={styles.quantityButtonText}>+</Text>
              </TouchableOpacity>
              
              <TouchableOpacity
                style={styles.removeButton}
                onPress={() => onItemRemove(item.id)}
              >
                <Text style={styles.removeButtonText}>×</Text>
              </TouchableOpacity>
            </View>
            
            <Text style={styles.itemTotal}>€{(item.totalPrice || 0).toFixed(2)}</Text>
          </View>
        ))}
      </ScrollView>
    </View>
  );
};

const styles = StyleSheet.create({
  cartSection: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 10,
  },
  cartHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 15,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
  },
  clearButton: {
    backgroundColor: '#f44336',
    paddingHorizontal: 15,
    paddingVertical: 8,
    borderRadius: 5,
  },
  clearButtonText: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '500',
  },
  cartItems: {
    maxHeight: 300,
  },
  cartItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
  },
  itemInfo: {
    flex: 1,
  },
  itemName: {
    fontSize: 16,
    fontWeight: '500',
    color: '#333',
    marginBottom: 4,
  },
  itemPrice: {
    fontSize: 14,
    color: '#666',
  },
  itemActions: {
    flexDirection: 'row',
    alignItems: 'center',
    marginHorizontal: 15,
  },
  quantityButton: {
    width: 30,
    height: 30,
    borderRadius: 15,
    backgroundColor: '#e0e0e0',
    justifyContent: 'center',
    alignItems: 'center',
  },
  quantityButtonText: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#666',
  },
  quantityText: {
    fontSize: 16,
    fontWeight: '500',
    marginHorizontal: 15,
    color: '#333',
  },
  removeButton: {
    width: 30,
    height: 30,
    borderRadius: 15,
    backgroundColor: '#ffebee',
    justifyContent: 'center',
    alignItems: 'center',
    marginLeft: 10,
  },
  removeButtonText: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#f44336',
  },
  itemTotal: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
  },
  loadingContainer: {
    padding: 20,
    alignItems: 'center',
  },
  loadingText: {
    fontSize: 16,
    color: '#666',
  },
  errorContainer: {
    backgroundColor: '#ffebee',
    padding: 15,
    borderRadius: 5,
    borderLeftWidth: 4,
    borderLeftColor: '#f44336',
  },
  errorText: {
    color: '#f44336',
    fontSize: 14,
  },
  emptyCart: {
    alignItems: 'center',
    paddingVertical: 40,
  },
  emptyCartText: {
    fontSize: 18,
    color: '#999',
    marginBottom: 10,
  },
  emptyCartSubtext: {
    fontSize: 14,
    color: '#ccc',
    textAlign: 'center',
  },
});
