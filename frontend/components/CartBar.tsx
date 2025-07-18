// Bu komponent, sadece backend'den gelen sepet ve hesaplama verilerini gösterir. Local hesaplama yapılmaz.
import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet, ActivityIndicator, ScrollView } from 'react-native';
import { Cart, CartItem } from '../hooks/useApiCart';

interface CartBarProps {
  cart: Cart | null;
  loading: boolean;
  onRemove: (itemId: string) => void;
  onUpdateQty: (itemId: string, qty: number) => void;
  onClear: () => void;
}

const defaultCart = { id: '', items: [], total: 0, discount: 0, vat: 0, grandTotal: 0 };

const CartBar: React.FC<CartBarProps> = ({ cart, loading, onRemove, onUpdateQty, onClear }) => {
  const safeCart = cart ?? defaultCart;
  if (loading) {
    return <ActivityIndicator size="small" color="#1976d2" />;
  }
  if (!safeCart || safeCart.items.length === 0) {
    return <Text style={styles.emptyText}>Sepet boş</Text>;
  }
  return (
    <View style={styles.container}>
      <ScrollView horizontal style={styles.cartList}>
        {safeCart.items.map(item => (
          <View key={item.id} style={styles.itemBox}>
            <Text style={styles.itemName}>{item.name}</Text>
            <Text style={styles.itemPrice}>{Number(item.price ?? 0).toFixed(2)} €</Text>
            <View style={styles.qtyRow}>
              <TouchableOpacity style={styles.qtyBtn} onPress={() => onUpdateQty(item.id, item.quantity - 1)}>
                <Text style={styles.qtyBtnText}>-</Text>
              </TouchableOpacity>
              <Text style={styles.qtyText}>{item.quantity}</Text>
              <TouchableOpacity style={styles.qtyBtn} onPress={() => onUpdateQty(item.id, item.quantity + 1)}>
                <Text style={styles.qtyBtnText}>+</Text>
              </TouchableOpacity>
              <TouchableOpacity style={styles.removeBtn} onPress={() => onRemove(item.id)}>
                <Text style={styles.removeBtnText}>Sil</Text>
              </TouchableOpacity>
            </View>
          </View>
        ))}
      </ScrollView>
      <View style={styles.summaryRow}>
        <Text style={styles.totalText}>Toplam: {Number(safeCart.total ?? 0).toFixed(2)} €</Text>
        <Text style={styles.totalText}>İndirim: {Number(safeCart.discount ?? 0).toFixed(2)} €</Text>
        <Text style={styles.totalText}>KDV: {Number(safeCart.vat ?? 0).toFixed(2)} €</Text>
        <Text style={styles.totalText}>Genel Toplam: {Number(safeCart.grandTotal ?? 0).toFixed(2)} €</Text>
        <TouchableOpacity style={styles.clearBtn} onPress={onClear}>
          <Text style={styles.clearBtnText}>Sepeti Temizle</Text>
        </TouchableOpacity>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: '#f5f5f5',
    padding: 12,
    borderRadius: 12,
    marginVertical: 8,
    width: '100%',
    alignItems: 'flex-start',
  },
  cartList: {
    minHeight: 60,
    maxHeight: 120,
    width: '100%',
  },
  emptyText: {
    color: '#888',
    fontSize: 16,
    marginLeft: 8,
  },
  itemBox: {
    backgroundColor: '#fff',
    borderRadius: 8,
    padding: 8,
    marginRight: 12,
    minWidth: 120,
    alignItems: 'center',
    elevation: 1,
  },
  itemName: {
    fontWeight: 'bold',
    fontSize: 16,
  },
  itemPrice: {
    color: '#1976d2',
    fontSize: 14,
    marginBottom: 4,
  },
  qtyRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 4,
  },
  qtyBtn: {
    backgroundColor: '#e0e0e0',
    borderRadius: 6,
    paddingHorizontal: 8,
    paddingVertical: 2,
    marginHorizontal: 2,
  },
  qtyBtnText: {
    fontSize: 18,
    fontWeight: 'bold',
  },
  qtyText: {
    fontSize: 16,
    marginHorizontal: 4,
  },
  removeBtn: {
    backgroundColor: '#d32f2f',
    borderRadius: 6,
    paddingHorizontal: 8,
    paddingVertical: 2,
    marginLeft: 6,
  },
  removeBtnText: {
    color: '#fff',
    fontWeight: 'bold',
    fontSize: 14,
  },
  summaryRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    width: '100%',
    marginTop: 8,
    gap: 8,
  },
  totalText: {
    fontSize: 15,
    fontWeight: 'bold',
    color: '#1976d2',
    marginRight: 8,
  },
  clearBtn: {
    backgroundColor: '#d32f2f',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 6,
    marginLeft: 8,
  },
  clearBtnText: {
    color: '#fff',
    fontWeight: 'bold',
    fontSize: 15,
  },
});

export default CartBar; 