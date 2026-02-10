import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  TextInput,
  FlatList,
  ActivityIndicator,
  Alert,
} from 'react-native';

import { Colors, Spacing, BorderRadius } from '../constants/Colors';
import { productService, Product } from '../services/api/productService';

interface ProductSelectionModalProps {
  visible: boolean;
  onClose: () => void;
  onProductSelected: (product: Product) => void;
  showLowStock?: boolean;
}

export default function ProductSelectionModal({
  visible,
  onClose,
  onProductSelected,
  showLowStock = false,
}: ProductSelectionModalProps) {
  const { t } = useTranslation();
  const [searchQuery, setSearchQuery] = useState('');
  const [products, setProducts] = useState<Product[]>([]);
  const [filteredProducts, setFilteredProducts] = useState<Product[]>([]);
  const [categories, setCategories] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<string>('All');

  useEffect(() => {
    if (visible) {
      loadData();
    }
  }, [visible]);

  useEffect(() => {
    filterProducts();
  }, [products, searchQuery, selectedCategory]);

  const loadData = async () => {
    try {
      setLoading(true);
      const [productsData, categoriesData] = await Promise.all([
        productService.getProducts(),
        productService.getCategories()
      ]);
      
      let filteredProducts = productsData.filter(p => p.isActive);
      
      if (showLowStock) {
        filteredProducts = filteredProducts.filter(p => p.stockQuantity <= p.minStockLevel);
      }
      
      setProducts(filteredProducts);
      setCategories(categoriesData);
    } catch (error) {
      Alert.alert('Error', 'Failed to load products');
    } finally {
      setLoading(false);
    }
  };

  const filterProducts = () => {
    let filtered = products;

    // Kategori filtresi
    if (selectedCategory !== 'All') {
      filtered = filtered.filter(p => p.category === selectedCategory);
    }

    // Arama filtresi
    if (searchQuery.trim()) {
      const query = searchQuery.toLowerCase();
      filtered = filtered.filter(p =>
        p.name.toLowerCase().includes(query) ||
        p.description.toLowerCase().includes(query) ||

        p.category.toLowerCase().includes(query)
      );
    }

    setFilteredProducts(filtered);
  };

  const handleProductSelect = (product: Product) => {
    if (product.stockQuantity <= 0) {
      Alert.alert('Out of Stock', 'This product is currently out of stock.');
      return;
    }
    onProductSelected(product);
    onClose();
  };

  const getStockStatusColor = (product: Product) => {
    if (product.stockQuantity <= 0) return Colors.light.error;
    if (product.stockQuantity <= product.minStockLevel) return Colors.light.warning;
    return Colors.light.success;
  };

  const getStockStatusText = (product: Product) => {
    if (product.stockQuantity <= 0) return 'Out of Stock';
    if (product.stockQuantity <= product.minStockLevel) return 'Low Stock';
    return 'In Stock';
  };

  const renderProductItem = ({ item }: { item: Product }) => (
    <TouchableOpacity
      style={[
        styles.productItem,
        item.stockQuantity <= 0 && styles.outOfStockItem
      ]}
      onPress={() => handleProductSelect(item)}
      disabled={item.stockQuantity <= 0}
    >
      <View style={styles.productInfo}>
        <View style={styles.productHeader}>
          <Text style={styles.productName}>{item.name}</Text>
          <View style={[styles.stockBadge, { backgroundColor: getStockStatusColor(item) }]}>
            <Text style={styles.stockText}>{getStockStatusText(item)}</Text>
          </View>
        </View>
        
        <Text style={styles.productDescription} numberOfLines={2}>
          {item.description}
        </Text>
        
        <View style={styles.productDetails}>
          <View style={styles.priceContainer}>
            <Ionicons name="pricetag" size={16} color={Colors.light.primary} />
            <Text style={styles.productPrice}>€{item.price.toFixed(2)}</Text>
          </View>
          
          <View style={styles.stockContainer}>
            <Ionicons name="cube" size={16} color={Colors.light.textSecondary} />
            <Text style={styles.stockQuantity}>{item.stockQuantity} {item.unit}</Text>
          </View>
        </View>
        
        <View style={styles.productMeta}>
          <Text style={styles.productCategory}>{item.category}</Text>
  
        </View>
      </View>
      
      <Ionicons 
        name="chevron-forward" 
        size={20} 
        color={item.stockQuantity <= 0 ? Colors.light.textSecondary : Colors.light.text} 
      />
    </TouchableOpacity>
  );

  const renderCategoryFilter = () => (
    <FlatList
      horizontal
      data={['All', ...categories]}
      renderItem={({ item }) => (
        <TouchableOpacity
          style={[
            styles.categoryFilterButton,
            selectedCategory === item && styles.categoryFilterButtonActive
          ]}
          onPress={() => setSelectedCategory(item)}
        >
          <Text style={[
            styles.categoryFilterText,
            selectedCategory === item && styles.categoryFilterTextActive
          ]}>
            {item}
          </Text>
        </TouchableOpacity>
      )}
      keyExtractor={(item) => item}
      style={styles.categoryFilter}
      showsHorizontalScrollIndicator={false}
    />
  );

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="pageSheet"
    >
      <View style={styles.container}>
        {/* Header */}
        <View style={styles.header}>
          <TouchableOpacity onPress={onClose} style={styles.closeButton}>
            <Ionicons name="close" size={24} color={Colors.light.text} />
          </TouchableOpacity>
          <Text style={styles.title}>
            {showLowStock ? 'Low Stock Products' : 'Select Product'}
          </Text>
          <View style={styles.placeholder} />
        </View>

        {/* Search Bar */}
        <View style={styles.searchContainer}>
          <Ionicons name="search" size={20} color={Colors.light.textSecondary} />
          <TextInput
            style={styles.searchInput}
            placeholder="Search products..."
            value={searchQuery}
            onChangeText={setSearchQuery}
            placeholderTextColor={Colors.light.textSecondary}
          />
          {searchQuery.length > 0 && (
            <TouchableOpacity onPress={() => setSearchQuery('')}>
              <Ionicons name="close-circle" size={20} color={Colors.light.textSecondary} />
            </TouchableOpacity>
          )}
        </View>

        {/* Category Filter */}
        {renderCategoryFilter()}

        {/* Product List */}
        {loading ? (
          <View style={styles.loadingContainer}>
            <ActivityIndicator size="large" color={Colors.light.primary} />
            <Text style={styles.loadingText}>Loading products...</Text>
          </View>
        ) : (
          <FlatList
            data={filteredProducts}
            renderItem={renderProductItem}
            keyExtractor={(item) => item.id}
            style={styles.productList}
            showsVerticalScrollIndicator={false}
            ListEmptyComponent={
              <View style={styles.emptyContainer}>
                <Ionicons name="cube-outline" size={64} color={Colors.light.textSecondary} />
                <Text style={styles.emptyText}>No products found</Text>
                <Text style={styles.emptySubtext}>
                  Try adjusting your search or category filter
                </Text>
              </View>
            }
          />
        )}
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: Colors.light.background,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: Spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  closeButton: {
    padding: Spacing.xs,
  },
  title: {
    fontSize: 18,
    fontWeight: '600',
    color: Colors.light.text,
  },
  placeholder: {
    width: 40,
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    margin: Spacing.lg,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  searchInput: {
    flex: 1,
    marginLeft: Spacing.sm,
    fontSize: 16,
    color: Colors.light.text,
  },
  categoryFilter: {
    paddingHorizontal: Spacing.lg,
    marginBottom: Spacing.md,
  },
  categoryFilterButton: {
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    marginRight: Spacing.sm,
    borderRadius: BorderRadius.md,
    backgroundColor: Colors.light.surface,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  categoryFilterButtonActive: {
    backgroundColor: Colors.light.primary,
    borderColor: Colors.light.primary,
  },
  categoryFilterText: {
    fontSize: 14,
    color: Colors.light.textSecondary,
  },
  categoryFilterTextActive: {
    color: 'white',
    fontWeight: '600',
  },
  productList: {
    flex: 1,
    paddingHorizontal: Spacing.lg,
  },
  productItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    marginBottom: Spacing.sm,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  outOfStockItem: {
    opacity: 0.6,
    backgroundColor: Colors.light.surface + '80',
  },
  productInfo: {
    flex: 1,
  },
  productHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: Spacing.xs,
  },
  productName: {
    fontSize: 16,
    fontWeight: '600',
    color: Colors.light.text,
    flex: 1,
  },
  stockBadge: {
    paddingHorizontal: Spacing.sm,
    paddingVertical: Spacing.xs,
    borderRadius: BorderRadius.sm,
  },
  stockText: {
    fontSize: 12,
    color: 'white',
    fontWeight: '600',
  },
  productDescription: {
    fontSize: 14,
    color: Colors.light.textSecondary,
    marginBottom: Spacing.sm,
  },
  productDetails: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: Spacing.sm,
  },
  priceContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    marginRight: Spacing.lg,
  },
  productPrice: {
    fontSize: 16,
    fontWeight: '600',
    color: Colors.light.primary,
    marginLeft: Spacing.xs,
  },
  stockContainer: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  stockQuantity: {
    fontSize: 14,
    color: Colors.light.textSecondary,
    marginLeft: Spacing.xs,
  },
  productMeta: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  productCategory: {
    fontSize: 12,
    color: Colors.light.textSecondary,
    backgroundColor: Colors.light.surface,
    paddingHorizontal: Spacing.sm,
    paddingVertical: Spacing.xs,
    borderRadius: BorderRadius.sm,
  },

  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: Spacing.md,
    fontSize: 16,
    color: Colors.light.textSecondary,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: Spacing.xl,
  },
  emptyText: {
    fontSize: 18,
    color: Colors.light.text,
    marginTop: Spacing.md,
    textAlign: 'center',
  },
  emptySubtext: {
    fontSize: 14,
    color: Colors.light.textSecondary,
    marginTop: Spacing.sm,
    textAlign: 'center',
  },
}); 