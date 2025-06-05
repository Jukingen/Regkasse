import React, { useState, useCallback, useMemo } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  TextInput,
  Alert,
  ActivityIndicator,
} from 'react-native';
import { useTheme } from '../../contexts/ThemeContext';
import { useFetch } from '../../hooks/useFetch';
import { API_BASE_URL } from '../../config';
import { OptimizedList } from '../../components/OptimizedList';
import { Ionicons } from '@expo/vector-icons';
import { useMemoizedCallback } from '../../hooks/useMemoizedCallback';

interface Product {
  id: string;
  name: string;
  price: number;
  stock: number;
  tax_type: 'standard' | 'reduced' | 'special';
}

interface CartItem extends Product {
  quantity: number;
}

export default function CashRegisterScreen() {
  const { theme } = useTheme();
  const styles = createStyles(theme);
  const [searchQuery, setSearchQuery] = useState('');
  const [cart, setCart] = useState<CartItem[]>([]);
  const [loading, setLoading] = useState(false);

  const { data: products, error, refetch } = useFetch<Product[]>({
    url: `${API_BASE_URL}/products`,
    options: {
      method: 'GET',
    },
  });

  const filteredProducts = useMemo(() => 
    products?.filter(product =>
      product.name.toLowerCase().includes(searchQuery.toLowerCase())
    ) ?? [],
    [products, searchQuery]
  );

  const cartTotal = useMemo(() => 
    cart.reduce((total, item) => total + (item.price * item.quantity), 0),
    [cart]
  );

  const addToCart = useMemoizedCallback((product: Product) => {
    setCart(prevCart => {
      const existingItem = prevCart.find(item => item.id === product.id);
      if (existingItem) {
        return prevCart.map(item =>
          item.id === product.id
            ? { ...item, quantity: item.quantity + 1 }
            : item
        );
      }
      return [...prevCart, { ...product, quantity: 1 }];
    });
  }, []);

  const removeFromCart = useMemoizedCallback((productId: string) => {
    setCart(prevCart => prevCart.filter(item => item.id !== productId));
  }, []);

  const updateQuantity = useMemoizedCallback((productId: string, newQuantity: number) => {
    if (newQuantity < 1) return;
    setCart(prevCart =>
      prevCart.map(item =>
        item.id === productId
          ? { ...item, quantity: newQuantity }
          : item
      )
    );
  }, []);

  const handleCheckout = useMemoizedCallback(async () => {
    if (cart.length === 0) {
      Alert.alert('Hata', 'Sepet boş!');
      return;
    }

    setLoading(true);
    try {
      const response = await fetch(`${API_BASE_URL}/sales`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          items: cart.map(item => ({
            product_id: item.id,
            quantity: item.quantity,
            tax_type: item.tax_type,
          })),
          payment: {
            method: 'cash',
            tse_required: true,
          },
        }),
      });

      if (!response.ok) {
        throw new Error('Satış işlemi başarısız oldu');
      }

      Alert.alert('Başarılı', 'Satış tamamlandı!');
      setCart([]);
    } catch (error) {
      Alert.alert('Hata', error instanceof Error ? error.message : 'Bir hata oluştu');
    } finally {
      setLoading(false);
    }
  }, [cart]);

  const renderProduct = useCallback(({ item }: { item: Product }) => (
    <TouchableOpacity
      style={styles.productItem}
      onPress={() => addToCart(item)}
    >
      <View style={styles.productInfo}>
        <Text style={styles.productName}>{item.name}</Text>
        <Text style={styles.productPrice}>{item.price.toFixed(2)} €</Text>
      </View>
      <View style={styles.productDetails}>
        <Text style={styles.stockText}>Stok: {item.stock}</Text>
        <Text style={styles.taxText}>KDV: {item.tax_type}</Text>
      </View>
    </TouchableOpacity>
  ), [theme, addToCart]);

  const renderCartItem = useCallback(({ item }: { item: CartItem }) => (
    <View style={styles.cartItem}>
      <View style={styles.cartItemInfo}>
        <Text style={styles.cartItemName}>{item.name}</Text>
        <Text style={styles.cartItemPrice}>{item.price.toFixed(2)} €</Text>
      </View>
      <View style={styles.quantityContainer}>
        <TouchableOpacity
          style={styles.quantityButton}
          onPress={() => updateQuantity(item.id, item.quantity - 1)}
        >
          <Ionicons name="remove" size={20} color={theme.text} />
        </TouchableOpacity>
        <Text style={styles.quantityText}>{item.quantity}</Text>
        <TouchableOpacity
          style={styles.quantityButton}
          onPress={() => updateQuantity(item.id, item.quantity + 1)}
        >
          <Ionicons name="add" size={20} color={theme.text} />
        </TouchableOpacity>
        <TouchableOpacity
          style={styles.removeButton}
          onPress={() => removeFromCart(item.id)}
        >
          <Ionicons name="trash-outline" size={20} color={theme.error} />
        </TouchableOpacity>
      </View>
    </View>
  ), [theme, updateQuantity, removeFromCart]);

  if (error) {
    return (
      <View style={styles.errorContainer}>
        <Text style={styles.errorText}>{error.message}</Text>
        <TouchableOpacity style={styles.retryButton} onPress={refetch}>
          <Text style={styles.retryButtonText}>Tekrar Dene</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <View style={styles.searchContainer}>
        <Ionicons name="search" size={20} color={theme.text} style={styles.searchIcon} />
        <TextInput
          style={styles.searchInput}
          placeholder="Ürün ara..."
          placeholderTextColor={theme.textSecondary}
          value={searchQuery}
          onChangeText={setSearchQuery}
        />
      </View>

      <View style={styles.content}>
        <View style={styles.productsContainer}>
          <OptimizedList
            data={filteredProducts}
            renderItem={renderProduct}
            contentContainerStyle={styles.productsList}
          />
        </View>

        <View style={styles.cartContainer}>
          <Text style={styles.cartTitle}>Sepet</Text>
          <OptimizedList
            data={cart}
            renderItem={renderCartItem}
            contentContainerStyle={styles.cartList}
            ListEmptyComponent={() => (
              <Text style={styles.emptyCartText}>Sepet boş</Text>
            )}
          />
          <View style={styles.cartFooter}>
            <Text style={styles.totalText}>
              Toplam: {cartTotal.toFixed(2)} €
            </Text>
            <TouchableOpacity
              style={[styles.checkoutButton, cart.length === 0 && styles.checkoutButtonDisabled]}
              onPress={handleCheckout}
              disabled={cart.length === 0 || loading}
            >
              {loading ? (
                <ActivityIndicator color={theme.buttonText} />
              ) : (
                <Text style={styles.checkoutButtonText}>Ödeme Al</Text>
              )}
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </View>
  );
}

const createStyles = (theme: any) => StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: theme.background,
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 16,
    backgroundColor: theme.card,
    borderBottomWidth: 1,
    borderBottomColor: theme.border,
  },
  searchIcon: {
    marginRight: 8,
  },
  searchInput: {
    flex: 1,
    height: 40,
    color: theme.text,
    fontSize: 16,
  },
  content: {
    flex: 1,
    flexDirection: 'row',
  },
  productsContainer: {
    flex: 1,
    borderRightWidth: 1,
    borderRightColor: theme.border,
  },
  productsList: {
    padding: 16,
  },
  productItem: {
    backgroundColor: theme.card,
    borderRadius: 8,
    padding: 16,
    marginBottom: 12,
    elevation: 2,
    shadowColor: theme.shadow,
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
  },
  productInfo: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  productName: {
    fontSize: 16,
    fontWeight: '600',
    color: theme.text,
  },
  productPrice: {
    fontSize: 16,
    fontWeight: '600',
    color: theme.primary,
  },
  productDetails: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  stockText: {
    fontSize: 14,
    color: theme.textSecondary,
  },
  taxText: {
    fontSize: 14,
    color: theme.textSecondary,
  },
  cartContainer: {
    width: 300,
    backgroundColor: theme.card,
  },
  cartTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: theme.text,
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: theme.border,
  },
  cartList: {
    padding: 16,
  },
  cartItem: {
    marginBottom: 12,
  },
  cartItemInfo: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  cartItemName: {
    fontSize: 16,
    color: theme.text,
  },
  cartItemPrice: {
    fontSize: 16,
    color: theme.primary,
  },
  quantityContainer: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  quantityButton: {
    padding: 8,
    backgroundColor: theme.buttonBackground,
    borderRadius: 4,
  },
  quantityText: {
    fontSize: 16,
    color: theme.text,
    marginHorizontal: 12,
    minWidth: 24,
    textAlign: 'center',
  },
  removeButton: {
    padding: 8,
    marginLeft: 8,
  },
  cartFooter: {
    padding: 16,
    borderTopWidth: 1,
    borderTopColor: theme.border,
  },
  totalText: {
    fontSize: 18,
    fontWeight: '600',
    color: theme.text,
    marginBottom: 16,
  },
  checkoutButton: {
    backgroundColor: theme.primary,
    padding: 16,
    borderRadius: 8,
    alignItems: 'center',
  },
  checkoutButtonDisabled: {
    opacity: 0.5,
  },
  checkoutButtonText: {
    color: theme.buttonText,
    fontSize: 16,
    fontWeight: '600',
  },
  emptyCartText: {
    fontSize: 16,
    color: theme.textSecondary,
    textAlign: 'center',
    marginTop: 32,
  },
  errorContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 32,
  },
  errorText: {
    fontSize: 16,
    color: theme.error,
    textAlign: 'center',
    marginBottom: 16,
  },
  retryButton: {
    backgroundColor: theme.primary,
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 8,
  },
  retryButtonText: {
    color: theme.buttonText,
    fontSize: 16,
    fontWeight: '600',
  },
}); 