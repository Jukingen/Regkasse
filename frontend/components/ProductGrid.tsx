import React, { useEffect, useState, useRef } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  Image,
  ActivityIndicator,
  Alert,
  Dimensions,
  Animated,
  Vibration,
} from 'react-native';

import { Colors } from '../constants/Colors';
import { getProducts, Product } from '../services/api/productService';
import Ionicons from 'react-native-vector-icons/Ionicons';

const { width } = Dimensions.get('window');
const COLUMN_COUNT = 2;
const ITEM_WIDTH = (width - 48) / COLUMN_COUNT; // 48 = padding + gap

interface ProductGridProps {
  onProductPress?: (product: Product, onPressCallback?: () => void) => void;
  showStock?: boolean;
  categoryFilter?: string;
}

const ProductGrid: React.FC<ProductGridProps> = ({
  onProductPress,
  showStock = true,
  categoryFilter,
}) => {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [highlightedId, setHighlightedId] = useState<string | null>(null);
  const highlightAnim = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    loadProducts();
  }, [categoryFilter]);

  const loadProducts = async () => {
    try {
      setLoading(true);
      setError(null);
      const fetchedProducts = await getProducts();
      
      // Kategori filtresi uygula
      let filteredProducts = fetchedProducts;
      if (categoryFilter) {
        filteredProducts = fetchedProducts.filter(
          product => product.category === categoryFilter
        );
      }
      
      setProducts(filteredProducts);
    } catch (err) {
      setError('Ürünler yüklenemedi');
      console.error('Error loading products:', err);
    } finally {
      setLoading(false);
    }
  };

  const triggerHighlight = (productId: string) => {
    setHighlightedId(productId);
    highlightAnim.setValue(0);
    Animated.timing(highlightAnim, {
      toValue: 1,
      duration: 350,
      useNativeDriver: false,
    }).start(() => {
      setHighlightedId(null);
    });
  };

  const handleProductPress = (product: Product) => {
    if (onProductPress) {
      // Başarı animasyonu tetikle
      triggerHighlight(product.id);
      
      // Haptic feedback
      Vibration.vibrate(50);
      
      // Ürün ekleme callback'i
      onProductPress(product, () => {
        // Başarı animasyonu tamamlandıktan sonra
        setTimeout(() => {
          Vibration.vibrate(25);
        }, 350);
      });
    } else {
      Alert.alert(
        product.name,
        `${product.description}\n\nFiyat: ${product.price.toFixed(2)} €\nStok: ${product.stockQuantity} ${product.unit}\nVergi: ${product.taxType}`
      );
    }
  };

  const renderProductItem = ({ item }: { item: Product }) => {
    const isHighlighted = highlightedId === item.id;
    const animatedStyle = isHighlighted
      ? {
          backgroundColor: highlightAnim.interpolate({
            inputRange: [0, 1],
            outputRange: [Colors.light.surface, '#dcfce7'],
          }),
          transform: [
            {
              scale: highlightAnim.interpolate({
                inputRange: [0, 1],
                outputRange: [1, 1.05],
              }),
            },
          ],
        }
      : {};
    return (
      <Animated.View style={[styles.productItem, item.stockQuantity <= 0 && styles.outOfStockItem, animatedStyle]}>
        <TouchableOpacity
          onPress={() => handleProductPress(item)}
          disabled={item.stockQuantity <= 0}
          style={styles.productTouchable}
          activeOpacity={0.7}
        >
          <View style={styles.productImageContainer}>
            {item.imageUrl ? (
              <Image source={{ uri: item.imageUrl }} style={styles.productImage} />
            ) : (
              <View style={styles.placeholderImage}>
                <Text style={styles.placeholderText}>📦</Text>
              </View>
            )}
            {item.stockQuantity <= 0 && (
              <View style={styles.outOfStockBadge}>
                <Text style={styles.outOfStockText}>TÜKENDİ</Text>
              </View>
            )}
            {/* Sepete Ekle İkonu */}
            <View style={styles.addToCartIcon}>
              <Ionicons name="add-circle" size={24} color="#059669" />
            </View>
          </View>
          <View style={styles.productInfo}>
            <Text style={styles.productName} numberOfLines={2}>
              {item.name}
            </Text>
            <Text style={styles.productPrice}>{item.price.toFixed(2)} €</Text>
            {showStock && (
              <Text style={[styles.stockInfo, item.stockQuantity <= 5 && styles.lowStock]}>
                Stok: {item.stockQuantity} {item.unit}
              </Text>
            )}
            <View style={styles.taxBadge}>
              <Text style={styles.taxText}>
                {item.taxType === 'Standard' ? '20%' : item.taxType === 'Reduced' ? '10%' : '13%'}
              </Text>
            </View>
          </View>
        </TouchableOpacity>
      </Animated.View>
    );
  };

  if (loading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color={Colors.light.primary} />
        <Text style={styles.loadingText}>Ürünler yükleniyor...</Text>
      </View>
    );
  }

  if (error) {
    return (
      <View style={styles.errorContainer}>
        <Text style={styles.errorText}>{error}</Text>
        <TouchableOpacity style={styles.retryButton} onPress={loadProducts}>
          <Text style={styles.retryButtonText}>Tekrar Dene</Text>
        </TouchableOpacity>
      </View>
    );
  }

  if (products.length === 0) {
    return (
      <View style={styles.emptyContainer}>
        <Text style={styles.emptyText}>Ürün bulunamadı</Text>
        <Text style={styles.emptySubtext}>
          {categoryFilter ? 'Bu kategoride ürün yok' : 'Henüz ürün eklenmemiş'}
        </Text>
      </View>
    );
  }

  return (
    <FlatList
      data={products}
      renderItem={renderProductItem}
      keyExtractor={(item) => item.id}
      numColumns={COLUMN_COUNT}
      columnWrapperStyle={styles.row}
      contentContainerStyle={styles.container}
      showsVerticalScrollIndicator={false}
      onRefresh={loadProducts}
      refreshing={loading}
    />
  );
};

const styles = StyleSheet.create({
  container: {
    padding: 16,
  },
  row: {
    justifyContent: 'space-between',
    marginBottom: 16,
  },
  productItem: {
    width: ITEM_WIDTH,
    backgroundColor: Colors.light.surface,
    borderRadius: 16,
    padding: 12,
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 4,
    },
    shadowOpacity: 0.1,
    shadowRadius: 6,
    elevation: 6,
    marginBottom: 16,
  },
  productTouchable: {
    flex: 1,
  },
  outOfStockItem: {
    opacity: 0.6,
  },
  productImageContainer: {
    position: 'relative',
    marginBottom: 12,
  },
  productImage: {
    width: '100%',
    height: 120,
    borderRadius: 12,
    resizeMode: 'cover',
  },
  placeholderImage: {
    width: '100%',
    height: 120,
    borderRadius: 12,
    backgroundColor: Colors.light.border,
    justifyContent: 'center',
    alignItems: 'center',
  },
  placeholderText: {
    fontSize: 40,
  },
  outOfStockBadge: {
    position: 'absolute',
    top: 8,
    right: 8,
    backgroundColor: Colors.light.error,
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
  },
  outOfStockText: {
    color: Colors.light.surface,
    fontSize: 10,
    fontWeight: 'bold',
  },
  addToCartIcon: {
    position: 'absolute',
    bottom: 8,
    right: 8,
    backgroundColor: 'rgba(255, 255, 255, 0.9)',
    borderRadius: 12,
    padding: 4,
  },
  productInfo: {
    flex: 1,
  },
  productName: {
    fontSize: 16,
    fontWeight: '600',
    color: '#1e293b',
    marginBottom: 8,
    lineHeight: 20,
  },
  productPrice: {
    fontSize: 18,
    fontWeight: '700',
    color: '#059669',
    marginBottom: 6,
  },
  stockInfo: {
    fontSize: 12,
    color: '#64748b',
    marginBottom: 8,
  },
  lowStock: {
    color: '#f59e0b',
    fontWeight: '600',
  },
  taxBadge: {
    alignSelf: 'flex-start',
    backgroundColor: '#f1f5f9',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
  },
  taxText: {
    fontSize: 11,
    color: '#64748b',
    fontWeight: '600',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
  },
  loadingText: {
    marginTop: 12,
    fontSize: 16,
    color: Colors.light.textSecondary,
  },
  errorContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
  },
  errorText: {
    fontSize: 16,
    color: Colors.light.error,
    textAlign: 'center',
    marginBottom: 16,
  },
  retryButton: {
    backgroundColor: Colors.light.primary,
    paddingHorizontal: 20,
    paddingVertical: 10,
    borderRadius: 8,
  },
  retryButtonText: {
    color: Colors.light.surface,
    fontSize: 14,
    fontWeight: '600',
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
  },
  emptyText: {
    fontSize: 18,
    color: Colors.light.text,
    fontWeight: '600',
    marginBottom: 8,
  },
  emptySubtext: {
    fontSize: 14,
    color: Colors.light.textSecondary,
    textAlign: 'center',
  },
});

export default ProductGrid; 