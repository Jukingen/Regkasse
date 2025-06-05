import React, { useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TextInput,
  TouchableOpacity,
  FlatList,
  Alert,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';

interface CartItem {
  id: number;
  name: string;
  price: number;
  quantity: number;
}

export default function CashRegisterScreen() {
  const [cart, setCart] = useState<CartItem[]>([]);
  const [barcodeInput, setBarcodeInput] = useState('');

  const addToCart = (product: CartItem) => {
    const existingItem = cart.find(item => item.id === product.id);
    if (existingItem) {
      setCart(cart.map(item =>
        item.id === product.id
          ? { ...item, quantity: item.quantity + 1 }
          : item
      ));
    } else {
      setCart([...cart, { ...product, quantity: 1 }]);
    }
  };

  const removeFromCart = (productId: number) => {
    setCart(cart.filter(item => item.id !== productId));
  };

  const updateQuantity = (productId: number, newQuantity: number) => {
    if (newQuantity < 1) {
      removeFromCart(productId);
      return;
    }
    setCart(cart.map(item =>
      item.id === productId
        ? { ...item, quantity: newQuantity }
        : item
    ));
  };

  const getTotal = () => {
    return cart.reduce((sum, item) => sum + (item.price * item.quantity), 0);
  };

  const handleCheckout = () => {
    Alert.alert('Başarılı', 'Ödeme tamamlandı!');
    setCart([]);
  };

  const renderCartItem = ({ item }: { item: CartItem }) => (
    <View style={styles.cartItem}>
      <View style={styles.itemInfo}>
        <Text style={styles.itemName}>{item.name}</Text>
        <Text style={styles.itemPrice}>{item.price.toFixed(2)} €</Text>
      </View>
      <View style={styles.quantityContainer}>
        <TouchableOpacity
          onPress={() => updateQuantity(item.id, item.quantity - 1)}
          style={styles.quantityButton}
        >
          <Ionicons name="remove" size={20} color="#007AFF" />
        </TouchableOpacity>
        <Text style={styles.quantity}>{item.quantity}</Text>
        <TouchableOpacity
          onPress={() => updateQuantity(item.id, item.quantity + 1)}
          style={styles.quantityButton}
        >
          <Ionicons name="add" size={20} color="#007AFF" />
        </TouchableOpacity>
      </View>
      <TouchableOpacity
        onPress={() => removeFromCart(item.id)}
        style={styles.removeButton}
      >
        <Ionicons name="trash-outline" size={24} color="#FF3B30" />
      </TouchableOpacity>
    </View>
  );

  return (
    <View style={styles.container}>
      <View style={styles.leftPanel}>
        <View style={styles.barcodeContainer}>
          <TextInput
            style={styles.barcodeInput}
            placeholder="Barkod Okut veya Ürün Ara..."
            value={barcodeInput}
            onChangeText={setBarcodeInput}
          />
          <TouchableOpacity style={styles.scanButton}>
            <Ionicons name="scan-outline" size={24} color="white" />
          </TouchableOpacity>
        </View>
        <FlatList
          data={cart}
          renderItem={renderCartItem}
          keyExtractor={item => item.id.toString()}
          style={styles.cartList}
        />
        <View style={styles.totalContainer}>
          <Text style={styles.totalText}>Toplam:</Text>
          <Text style={styles.totalAmount}>{getTotal().toFixed(2)} €</Text>
        </View>
        <TouchableOpacity
          style={styles.checkoutButton}
          onPress={handleCheckout}
          disabled={cart.length === 0}
        >
          <Text style={styles.checkoutButtonText}>Ödeme Al</Text>
        </TouchableOpacity>
      </View>
      <View style={styles.rightPanel}>
        {/* Buraya hızlı ürün seçimi için butonlar eklenebilir */}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    flexDirection: 'row',
    backgroundColor: '#f5f5f5',
  },
  leftPanel: {
    flex: 2,
    padding: 10,
  },
  rightPanel: {
    flex: 1,
    padding: 10,
    borderLeftWidth: 1,
    borderLeftColor: '#ddd',
  },
  barcodeContainer: {
    flexDirection: 'row',
    marginBottom: 10,
    gap: 10,
  },
  barcodeInput: {
    flex: 1,
    height: 40,
    backgroundColor: 'white',
    borderRadius: 8,
    paddingHorizontal: 10,
  },
  scanButton: {
    width: 40,
    height: 40,
    backgroundColor: '#007AFF',
    borderRadius: 8,
    justifyContent: 'center',
    alignItems: 'center',
  },
  cartList: {
    flex: 1,
  },
  cartItem: {
    backgroundColor: 'white',
    padding: 15,
    marginBottom: 8,
    borderRadius: 8,
    flexDirection: 'row',
    alignItems: 'center',
  },
  itemInfo: {
    flex: 2,
  },
  itemName: {
    fontSize: 16,
    fontWeight: 'bold',
  },
  itemPrice: {
    fontSize: 14,
    color: '#007AFF',
    marginTop: 4,
  },
  quantityContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  quantityButton: {
    padding: 5,
  },
  quantity: {
    fontSize: 16,
    fontWeight: 'bold',
    minWidth: 30,
    textAlign: 'center',
  },
  removeButton: {
    marginLeft: 15,
  },
  totalContainer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: 'white',
    padding: 15,
    borderRadius: 8,
    marginVertical: 10,
  },
  totalText: {
    fontSize: 18,
    fontWeight: 'bold',
  },
  totalAmount: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#007AFF',
  },
  checkoutButton: {
    backgroundColor: '#34C759',
    padding: 15,
    borderRadius: 8,
    alignItems: 'center',
  },
  checkoutButtonText: {
    color: 'white',
    fontSize: 18,
    fontWeight: 'bold',
  },
}); 