import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  Modal,
  FlatList,
  TextInput,
  ActivityIndicator,
  ScrollView,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { 
  getActiveProductsForHomePage, // ✅ YENİ: getAllActiveProducts yerine
  getProductsByCategory, 
  searchProducts,
  Product 
} from '../services/api/productService';
import { Colors } from '../constants/Colors';

/**
 * Ürün seçici komponenti - Fatura oluşturma için
 * RKSV uyumlu ürün seçimi ve miktar belirleme
 */
interface ProductSelectorProps {
  visible: boolean;
  onClose: () => void;
  onProductSelect: (product: Product, quantity: number) => void;
  selectedProducts?: Array<{ product: Product; quantity: number }>;
}

export const ProductSelector: React.FC<ProductSelectorProps> = ({
  visible,
  onClose,
  onProductSelect,
  selectedProducts = [],
}) => {
  const { t } = useTranslation();
  const [products, setProducts] = useState<Product[]>([]);
  const [filteredProducts, setFilteredProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);
  const [quantity, setQuantity] = useState('1');

  // Kategoriler
  const categories = [
    'all',
    'Hauptgerichte',
    'Getränke',
    'Desserts',
    'Alkoholische Getränke',
    'Snacks',
    'Suppen',
    'Vorspeisen',
    'Salate',
    'Kaffee & Tee',
    'Süßigkeiten',
    'Spezialitäten',
    'Brot & Gebäck'
  ];

  // Ürünleri yükle
  const loadProducts = async (category?: string) => {
    try {
      setLoading(true);
      let productsData: Product[];

      if (category && category !== 'all') {
        productsData = await getProductsByCategory(category);
      } else {
        // ✅ YENİ: getActiveProductsForHomePage kullan
        const response = await getActiveProductsForHomePage();
        productsData = response.flatMap(group => group.products);
      }

      setProducts(productsData);
      setFilteredProducts(productsData);
    } catch (error) {
      console.error('Error loading products:', error);
    } finally {
      setLoading(false);
    }
  };

  // Arama yap
  const handleSearch = async () => {
    if (!searchQuery.trim()) {
      setFilteredProducts(products);
      return;
    }

    try {
      const results = await searchProducts({ name: searchQuery.trim() });
      setFilteredProducts(results);
    } catch (error) {
      console.error('Search error:', error);
    }
  };

  // Kategori değişimi
  const handleCategoryChange = (category: string) => {
    setSelectedCategory(category);
    setSearchQuery('');
    loadProducts(category);
  };

  // Ürün seçimi
  const handleProductPress = (product: Product) => {
    setSelectedProduct(product);
    setQuantity('1');
  };

  // Ürün ekleme
  const handleAddProduct = () => {
    if (selectedProduct && quantity) {
      const qty = parseInt(quantity);
      if (qty > 0) {
        onProductSelect(selectedProduct, qty);
        setSelectedProduct(null);
        setQuantity('1');
        setSearchQuery('');
        setFilteredProducts(products);
      }
    }
  };

  // Modal açıldığında ürünleri yükle
  useEffect(() => {
    if (visible) {
      loadProducts();
    }
  }, [visible]);

  // Arama query değiştiğinde arama yap
  useEffect(() => {
    const timeoutId = setTimeout(() => {
      handleSearch();
    }, 500);

    return () => clearTimeout(timeoutId);
  }, [searchQuery]);

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="pageSheet"
      onRequestClose={onClose}
    >
      <View style={styles.container}>
        {/* Header */}
        <View style={styles.header}>
          <TouchableOpacity onPress={onClose} style={styles.closeButton}>
            <Ionicons name="close" size={24} color={Colors.text} />
          </TouchableOpacity>
          <Text style={styles.headerTitle}>Ürün Seç</Text>
          <View style={styles.placeholder} />
        </View>

        {/* Arama */}
        <View style={styles.searchContainer}>
          <View style={styles.searchInputContainer}>
            <Ionicons name="search" size={20} color={Colors.textSecondary} />
            <TextInput
              style={styles.searchInput}
              value={searchQuery}
              onChangeText={setSearchQuery}
              placeholder="Ürün ara..."
              placeholderTextColor={Colors.textTertiary}
            />
          </View>
        </View>

        {/* Kategori Filtreleri */}
        <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.categoryContainer}>
          {categories.map((category) => (
            <TouchableOpacity
              key={category}
              style={[
                styles.categoryButton,
                selectedCategory === category && styles.categoryButtonActive
              ]}
              onPress={() => handleCategoryChange(category)}
            >
              <Text style={[
                styles.categoryButtonText,
                selectedCategory === category && styles.categoryButtonTextActive
              ]}>
                {category === 'all' ? 'Tümü' : category}
              </Text>
            </TouchableOpacity>
          ))}
        </ScrollView>

        {/* Ürün Listesi */}
        {loading ? (
          <View style={styles.loadingContainer}>
            <ActivityIndicator size="large" color={Colors.primary} />
            <Text style={styles.loadingText}>Ürünler yükleniyor...</Text>
          </View>
        ) : (
          <FlatList
            data={filteredProducts}
            keyExtractor={(item) => item.id}
            renderItem={({ item }) => (
              <TouchableOpacity
                style={[
                  styles.productItem,
                  selectedProduct?.id === item.id && styles.productItemSelected
                ]}
                onPress={() => handleProductPress(item)}
              >
                <View style={styles.productInfo}>
                  <Text style={styles.productName} numberOfLines={2}>
                    {item.name}
                  </Text>
                  <Text style={styles.productCategory}>
                    {item.category}
                  </Text>
                  <Text style={styles.productDescription} numberOfLines={1}>
                    {item.description}
                  </Text>
                </View>
                <View style={styles.productMeta}>
                  <Text style={styles.productPrice}>
                    €{item.price.toFixed(2)}
                  </Text>
                  <Text style={styles.productStock}>
                    Stok: {item.stockQuantity} {item.unit}
                  </Text>
                  <View style={[
                    styles.taxBadge,
                    { backgroundColor: getTaxTypeColor(item.taxType) }
                  ]}>
                    <Text style={styles.taxBadgeText}>
                      {item.taxType}
                    </Text>
                  </View>
                </View>
              </TouchableOpacity>
            )}
            contentContainerStyle={styles.productList}
            showsVerticalScrollIndicator={false}
          />
        )}

        {/* Seçili Ürün Detayı */}
        {selectedProduct && (
          <View style={styles.selectedProductContainer}>
            <View style={styles.selectedProductInfo}>
              <Text style={styles.selectedProductName}>
                {selectedProduct.name}
              </Text>
              <Text style={styles.selectedProductPrice}>
                €{selectedProduct.price.toFixed(2)}
              </Text>
            </View>
            <View style={styles.quantityContainer}>
              <Text style={styles.quantityLabel}>Miktar:</Text>
              <TextInput
                style={styles.quantityInput}
                value={quantity}
                onChangeText={setQuantity}
                keyboardType="numeric"
                placeholder="1"
                placeholderTextColor={Colors.textTertiary}
              />
              <Text style={styles.quantityUnit}>
                {selectedProduct.unit}
              </Text>
            </View>
            <TouchableOpacity
              style={styles.addButton}
              onPress={handleAddProduct}
            >
              <Ionicons name="add" size={20} color={Colors.white} />
              <Text style={styles.addButtonText}>Sepete Ekle</Text>
            </TouchableOpacity>
          </View>
        )}
      </View>
    </Modal>
  );
};

// Vergi tipi rengi
const getTaxTypeColor = (taxType: string) => {
  switch (taxType) {
    case 'Standard': return Colors.primary;
    case 'Reduced': return Colors.secondary;
    case 'Special': return Colors.accent;
    default: return Colors.text;
  }
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: Colors.background,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border,
    backgroundColor: Colors.surface,
  },
  closeButton: {
    padding: 8,
  },
  headerTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: Colors.text,
  },
  placeholder: {
    width: 40,
  },
  searchContainer: {
    padding: 16,
    backgroundColor: Colors.surface,
  },
  searchInputContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: Colors.background,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: Colors.border,
    paddingHorizontal: 12,
  },
  searchInput: {
    flex: 1,
    height: 48,
    fontSize: 16,
    color: Colors.text,
    marginLeft: 8,
  },
  categoryContainer: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    backgroundColor: Colors.surface,
  },
  categoryButton: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    marginRight: 8,
    borderRadius: 20,
    backgroundColor: Colors.surfaceVariant,
  },
  categoryButtonActive: {
    backgroundColor: Colors.primary,
  },
  categoryButtonText: {
    fontSize: 14,
    color: Colors.textSecondary,
  },
  categoryButtonTextActive: {
    color: Colors.white,
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: 16,
    fontSize: 16,
    color: Colors.textSecondary,
  },
  productList: {
    padding: 16,
  },
  productItem: {
    backgroundColor: Colors.surface,
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
    flexDirection: 'row',
    justifyContent: 'space-between',
    elevation: 2,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
  },
  productItemSelected: {
    borderWidth: 2,
    borderColor: Colors.primary,
  },
  productInfo: {
    flex: 1,
    marginRight: 16,
  },
  productName: {
    fontSize: 16,
    fontWeight: '600',
    color: Colors.text,
    marginBottom: 4,
  },
  productCategory: {
    fontSize: 12,
    color: Colors.textSecondary,
    marginBottom: 4,
  },
  productDescription: {
    fontSize: 14,
    color: Colors.textSecondary,
    lineHeight: 18,
  },
  productMeta: {
    alignItems: 'flex-end',
  },
  productPrice: {
    fontSize: 18,
    fontWeight: '700',
    color: Colors.primary,
    marginBottom: 4,
  },
  productStock: {
    fontSize: 12,
    color: Colors.textSecondary,
    marginBottom: 8,
  },
  taxBadge: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 12,
  },
  taxBadgeText: {
    fontSize: 10,
    fontWeight: '600',
    color: Colors.white,
  },
  selectedProductContainer: {
    backgroundColor: Colors.surface,
    padding: 16,
    borderTopWidth: 1,
    borderTopColor: Colors.border,
  },
  selectedProductInfo: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 16,
  },
  selectedProductName: {
    fontSize: 16,
    fontWeight: '600',
    color: Colors.text,
    flex: 1,
  },
  selectedProductPrice: {
    fontSize: 18,
    fontWeight: '700',
    color: Colors.primary,
  },
  quantityContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 16,
  },
  quantityLabel: {
    fontSize: 16,
    color: Colors.text,
    marginRight: 12,
  },
  quantityInput: {
    borderWidth: 1,
    borderColor: Colors.border,
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
    width: 80,
    textAlign: 'center',
    fontSize: 16,
    color: Colors.text,
    backgroundColor: Colors.background,
  },
  quantityUnit: {
    fontSize: 16,
    color: Colors.textSecondary,
    marginLeft: 8,
  },
  addButton: {
    backgroundColor: Colors.primary,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 12,
    borderRadius: 8,
  },
  addButtonText: {
    color: Colors.white,
    fontSize: 16,
    fontWeight: '600',
    marginLeft: 8,
  },
});
