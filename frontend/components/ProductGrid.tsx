// Türkçe Açıklama: Ürün grid komponenti - Kategorilere göre ürünleri filtreler ve görsel olarak sunar
// Backend'den gelen ürün verilerini kullanarak kategori bazlı filtreleme yapar

import React from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  ScrollView,
  Vibration,
} from 'react-native';

interface Product {
  // Backend'den gelen field'lar (lowercase)
  id: string;
  name: string;
  price: number;
  category: string;
  stockQuantity: number;
  description: string;
  taxType: string;
  // Backend catalog endpoint'inden gelen field'lar
  productCategory?: string; // Backend'de ProductCategory olarak map edildi
  categoryId?: string; // Backend'de CategoryId olarak map edildi
  // Eski field'lar (geriye uyumluluk için)
  Id?: string;
  Name?: string;
  Price?: number;
  Category?: string;
  StockQuantity?: number;
  Description?: string;
  TaxType?: number;
}

interface ProductGridProps {
  products: Product[];
  selectedCategory: string;
  loading: boolean;
  error: string | null;
  cartItems: any[];
  selectedTable: number;
  onProductSelect: (product: Product) => void;
  onRefreshProducts: () => void;
  onForceRefreshProducts: () => void;
}

export const ProductGrid: React.FC<ProductGridProps> = ({
  products,
  selectedCategory,
  loading,
  error,
  cartItems,
  selectedTable,
  onProductSelect,
  onRefreshProducts,
  onForceRefreshProducts,
}) => {
  // Ürün filtreleme - Backend'den gelen category field'ını öncelikle kullan
  const filteredProducts = products.filter((product: Product) => {
    if (selectedCategory === 'all') return true;
    
    // Backend'den gelen category field'larını öncelikle kullan
    const productCategory = product.productCategory || product.category || product.Category;
    console.log(`🔍 Filtering product: ${product.name || product.Name}, category: ${productCategory}, selected: ${selectedCategory}`);
    return productCategory === selectedCategory;
  });

  const handleProductPress = (product: Product) => {
    if (!selectedTable) {
      return;
    }

    // Haptic feedback
    Vibration.vibrate(30);
    
    // Ürün seçimi
    onProductSelect(product);
  };

  const getQuantityInCart = (productId: string): number => {
    const cartItem = cartItems?.find((item: any) => item.productId === productId);
    return cartItem?.quantity || 0;
  };

  // Debug bilgisi
  console.log(`🔍 ProductGrid: ${products.length} total products, ${filteredProducts.length} filtered for category: ${selectedCategory}`);

  if (loading) {
    return (
      <View style={styles.productsSection}>
        <Text style={styles.sectionTitle}>Available Products</Text>
        <View style={styles.loadingContainer}>
          <Text style={styles.loadingText}>🔄 Loading products...</Text>
        </View>
      </View>
    );
  }

  if (error) {
    return (
      <View style={styles.productsSection}>
        <Text style={styles.sectionTitle}>Available Products</Text>
        <View style={styles.errorContainer}>
          <Text style={styles.errorText}>❌ {error}</Text>
          <View style={styles.errorActions}>
            <TouchableOpacity onPress={onRefreshProducts} style={styles.retryButton}>
              <Text style={styles.retryButtonText}>🔄 Retry</Text>
            </TouchableOpacity>
            <TouchableOpacity onPress={onForceRefreshProducts} style={styles.forceRetryButton}>
              <Text style={styles.forceRetryButtonText}>⚡ Force Refresh</Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>
    );
  }

  if (!filteredProducts || filteredProducts.length === 0) {
    return (
      <View style={styles.productsSection}>
        <Text style={styles.sectionTitle}>Available Products</Text>
        <View style={styles.noProductsContainer}>
          <Text style={styles.noProductsText}>
            📦 {selectedCategory === 'all' ? 'No products available' : `No products in ${selectedCategory} category`}
          </Text>
          <TouchableOpacity onPress={onRefreshProducts} style={styles.retryButton}>
            <Text style={styles.retryButtonText}>🔄 Refresh Products</Text>
          </TouchableOpacity>
        </View>
      </View>
    );
  }

  return (
    <View style={styles.productsSection}>
      <Text style={styles.sectionTitle}>Available Products</Text>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.productsScroll}>
        {filteredProducts.map((product: Product) => {
          const quantityInCart = getQuantityInCart(product.id || product.Id || '');
          const productCategory = product.productCategory || product.category || product.Category;
          const productName = product.name || product.Name || '';
          const productPrice = product.price || product.Price || 0;
          const productStock = product.stockQuantity || product.StockQuantity || 0;
          
          console.log(`📦 Rendering product: ${productName}, category: ${productCategory}`);
          
          return (
            <TouchableOpacity
              key={product.id || product.Id}
              style={[
                styles.productCard,
                quantityInCart > 0 && styles.productCardInCart
              ]}
              onPress={() => handleProductPress(product)}
              activeOpacity={0.7}
            >
              {/* Quantity Badge */}
              {quantityInCart > 0 && (
                <View style={styles.quantityBadge}>
                  <Text style={styles.quantityBadgeText}>{quantityInCart}x</Text>
                </View>
              )}
              
              <Text style={styles.productName}>{productName}</Text>
              <Text style={styles.productPrice}>€{productPrice.toFixed(2)}</Text>
              <Text style={styles.productStock}>Stock: {productStock}</Text>
              {productCategory && (
                <Text style={styles.productCategory}>{productCategory}</Text>
              )}
            </TouchableOpacity>
          );
        })}
      </ScrollView>
    </View>
  );
};

const styles = StyleSheet.create({
  productsSection: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 10,
    borderRadius: 5,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 15,
    color: '#333',
  },
  productsScroll: {
    flexDirection: 'row',
  },
  productCard: {
    backgroundColor: '#f8f9fa',
    padding: 15,
    borderRadius: 8,
    marginRight: 15,
    minWidth: 140,
    borderWidth: 1,
    borderColor: '#2196F3',
    elevation: 2,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
  },
  productCardInCart: {
    backgroundColor: '#e8f5e8',
    borderColor: '#4CAF50',
    borderWidth: 2,
  },
  productName: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
    marginBottom: 5,
  },
  productPrice: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#2196F3',
    marginBottom: 3,
  },
  productStock: {
    fontSize: 12,
    color: '#666',
  },
  productCategory: {
    fontSize: 12,
    color: '#666',
    marginTop: 5,
  },
  quantityBadge: {
    position: 'absolute',
    top: 5,
    right: 5,
    backgroundColor: '#4CAF50',
    borderRadius: 10,
    paddingHorizontal: 5,
    paddingVertical: 2,
    minWidth: 30,
    alignItems: 'center',
    zIndex: 1,
  },
  quantityBadgeText: {
    color: '#fff',
    fontSize: 12,
    fontWeight: 'bold',
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
  errorActions: {
    flexDirection: 'row',
    gap: 10,
    marginTop: 10,
  },
  retryButton: {
    backgroundColor: '#f44336',
    paddingHorizontal: 20,
    paddingVertical: 10,
    borderRadius: 5,
    alignSelf: 'flex-start',
  },
  retryButtonText: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '500',
  },
  forceRetryButton: {
    backgroundColor: '#2196F3',
    paddingHorizontal: 20,
    paddingVertical: 10,
    borderRadius: 5,
    alignSelf: 'flex-start',
  },
  forceRetryButtonText: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '500',
  },
  noProductsContainer: {
    padding: 20,
    alignItems: 'center',
  },
  noProductsText: {
    fontSize: 16,
    color: '#666',
    marginBottom: 5,
  },
});
