import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TextInput,
  TouchableOpacity,
  FlatList,
  Modal,
  Vibration,
  Dimensions,
  ScrollView,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';
import { Product } from '../services/api/productService';

interface QuickProductSearchProps {
  products: Product[];
  onProductSelect: (product: Product) => void;

}

const { width: screenWidth } = Dimensions.get('window');

const QuickProductSearch: React.FC<QuickProductSearchProps> = ({
  products,
  onProductSelect,

}) => {
  const { t } = useTranslation();
  const [searchQuery, setSearchQuery] = useState('');
  const [filteredProducts, setFilteredProducts] = useState<Product[]>([]);
  const [showResults, setShowResults] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [recentSearches, setRecentSearches] = useState<string[]>([]);
  const [favoriteProducts, setFavoriteProducts] = useState<Product[]>([]);
  
  const searchInputRef = useRef<TextInput>(null);

  // Kategorileri al
  const categories = ['all', ...new Set(products.map(p => p.category))];

  // Arama işlevi
  const handleSearch = (query: string) => {
    setSearchQuery(query);
    
    if (query.trim().length === 0) {
      setFilteredProducts([]);
      setShowResults(false);
      return;
    }

    let filtered = products;

    // Arama filtresi
    filtered = filtered.filter(product =>
      product.name.toLowerCase().includes(query.toLowerCase()) ||
      product.description?.toLowerCase().includes(query.toLowerCase()) ||
      
      product.category.toLowerCase().includes(query.toLowerCase())
    );

    // Kategori filtresi
    if (selectedCategory !== 'all') {
      filtered = filtered.filter(product => product.category === selectedCategory);
    }

    setFilteredProducts(filtered);
    setShowResults(true);

    // Son aramaları güncelle
    if (query.trim()) {
      setRecentSearches(prev => {
        const newSearches = [query, ...prev.filter(s => s !== query)].slice(0, 5);
        return newSearches;
      });
    }
  };

  // Ürün seçimi
  const handleProductSelect = (product: Product) => {
    Vibration.vibrate(50);
    onProductSelect(product);
    setSearchQuery('');
    setShowResults(false);
    searchInputRef.current?.blur();
  };

  // Barkod tarama
  // Türkçe Açıklama: Barcode scanner ile ilgili tüm fonksiyon, buton ve UI elementleri kaldırıldı.

  // Favori ürünleri yükle (AsyncStorage'dan)
  useEffect(() => {
    // Bu kısım gerçek uygulamada AsyncStorage'dan yüklenecek
    const loadFavorites = async () => {
      // Mock data
      const favorites = products.filter(p => p.id.includes('1')).slice(0, 5);
      setFavoriteProducts(favorites);
    };
    loadFavorites();
  }, [products]);

  const renderProductItem = ({ item }: { item: Product }) => (
    <TouchableOpacity
      style={styles.productItem}
      onPress={() => handleProductSelect(item)}
    >
      <View style={styles.productInfo}>
        <Text style={styles.productName} numberOfLines={1}>
          {item.name}
        </Text>
        <Text style={styles.productCategory} numberOfLines={1}>
          {item.category}
        </Text>
        
      </View>
      <View style={styles.productPrice}>
        <Text style={styles.priceText}>€{item.price.toFixed(2)}</Text>
                 <Text style={styles.stockText}>
           {item.stockQuantity > 0 ? `${item.stockQuantity} in stock` : 'Out of stock'}
         </Text>
      </View>
      <Ionicons name="chevron-forward" size={20} color={Colors.light.textSecondary} />
    </TouchableOpacity>
  );

  const renderCategoryButton = (category: string) => (
    <TouchableOpacity
      key={category}
      style={[
        styles.categoryButton,
        selectedCategory === category && styles.categoryButtonActive
      ]}
      onPress={() => setSelectedCategory(category)}
    >
      <Text style={[
        styles.categoryButtonText,
        selectedCategory === category && styles.categoryButtonTextActive
      ]}>
        {category === 'all' ? 'All' : category}
      </Text>
    </TouchableOpacity>
  );

  return (
    <View style={styles.container}>
      {/* Arama Çubuğu */}
      <View style={styles.searchContainer}>
        <View style={styles.searchInputContainer}>
          <Ionicons name="search" size={20} color={Colors.light.textSecondary} />
          <TextInput
            ref={searchInputRef}
            style={styles.searchInput}
            placeholder={t('search.placeholder')}
            value={searchQuery}
            onChangeText={handleSearch}
            onFocus={() => setShowResults(true)}
            returnKeyType="search"
            autoCapitalize="none"
            autoCorrect={false}
          />
          {searchQuery.length > 0 && (
            <TouchableOpacity onPress={() => handleSearch('')}>
              <Ionicons name="close-circle" size={20} color={Colors.light.textSecondary} />
            </TouchableOpacity>
          )}
        </View>
        
        {/* Barkod tarama butonu kaldırıldı */}
      </View>

      {/* Kategori Filtreleri */}
      <View style={styles.categoriesContainer}>
        <ScrollView horizontal showsHorizontalScrollIndicator={false}>
          {categories.map(renderCategoryButton)}
        </ScrollView>
      </View>

      {/* Sonuçlar */}
      {showResults && (
        <View style={styles.resultsContainer}>
          {searchQuery.length === 0 ? (
            // Ana sayfa - Favoriler ve son aramalar
            <View>
              {/* Favori Ürünler */}
              {favoriteProducts.length > 0 && (
                <View style={styles.section}>
                  <Text style={styles.sectionTitle}>Favorite Products</Text>
                  {favoriteProducts.map(product => (
                    <TouchableOpacity
                      key={product.id}
                      style={styles.quickProductItem}
                      onPress={() => handleProductSelect(product)}
                    >
                      <Ionicons name="heart" size={16} color={Colors.light.primary} />
                      <Text style={styles.quickProductName}>{product.name}</Text>
                      <Text style={styles.quickProductPrice}>€{product.price.toFixed(2)}</Text>
                    </TouchableOpacity>
                  ))}
                </View>
              )}

              {/* Son Aramalar */}
              {recentSearches.length > 0 && (
                <View style={styles.section}>
                  <Text style={styles.sectionTitle}>Recent Searches</Text>
                  {recentSearches.map((search, index) => (
                    <TouchableOpacity
                      key={index}
                      style={styles.recentSearchItem}
                      onPress={() => handleSearch(search)}
                    >
                      <Ionicons name="time-outline" size={16} color={Colors.light.textSecondary} />
                      <Text style={styles.recentSearchText}>{search}</Text>
                    </TouchableOpacity>
                  ))}
                </View>
              )}

              {/* Hızlı Eylemler */}
              <View style={styles.section}>
                <Text style={styles.sectionTitle}>Quick Actions</Text>
                {/* Barkod tarama butonu kaldırıldı */}
                <TouchableOpacity
                  style={styles.quickActionItem}
                  onPress={() => setSelectedCategory('all')}
                >
                  <Ionicons name="grid-outline" size={16} color={Colors.light.primary} />
                  <Text style={styles.quickActionText}>Browse All Products</Text>
                </TouchableOpacity>
              </View>
            </View>
          ) : (
            // Arama sonuçları
            <FlatList
              data={filteredProducts}
              keyExtractor={(item) => item.id}
              renderItem={renderProductItem}
              showsVerticalScrollIndicator={false}
              ListEmptyComponent={
                <View style={styles.emptyState}>
                  <Ionicons name="search-outline" size={48} color={Colors.light.textSecondary} />
                  <Text style={styles.emptyStateText}>
                    No products found for "{searchQuery}"
                  </Text>
                  <Text style={styles.emptyStateSubtext}>
                    Try a different search term or category
                  </Text>
                </View>
              }
            />
          )}
        </View>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: Colors.light.background,
  },
  searchContainer: {
    flexDirection: 'row',
    padding: Spacing.md,
    gap: Spacing.sm,
  },
  searchInputContainer: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    paddingHorizontal: Spacing.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  searchInput: {
    flex: 1,
    paddingVertical: Spacing.md,
    paddingHorizontal: Spacing.sm,
    fontSize: 16,
    color: Colors.light.text,
  },
  scanButton: {
    backgroundColor: Colors.light.primary,
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    justifyContent: 'center',
    alignItems: 'center',
  },
  categoriesContainer: {
    paddingHorizontal: Spacing.md,
    paddingBottom: Spacing.sm,
  },
  categoryButton: {
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    marginRight: Spacing.sm,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  categoryButtonActive: {
    backgroundColor: Colors.light.primary,
    borderColor: Colors.light.primary,
  },
  categoryButtonText: {
    color: Colors.light.text,
    fontSize: 14,
    fontWeight: '500',
  },
  categoryButtonTextActive: {
    color: 'white',
  },
  resultsContainer: {
    flex: 1,
    paddingHorizontal: Spacing.md,
  },
  section: {
    marginBottom: Spacing.lg,
  },
     sectionTitle: {
     ...Typography.h3,
     color: Colors.light.text,
     marginBottom: Spacing.sm,
   },
  quickProductItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.xs,
    gap: Spacing.sm,
  },
  quickProductName: {
    flex: 1,
    ...Typography.body,
    color: Colors.light.text,
  },
  quickProductPrice: {
    ...Typography.bodySmall,
    color: Colors.light.primary,
    fontWeight: '600',
  },
  recentSearchItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.xs,
    gap: Spacing.sm,
  },
  recentSearchText: {
    flex: 1,
    ...Typography.body,
    color: Colors.light.text,
  },
  quickActionItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.xs,
    gap: Spacing.sm,
  },
  quickActionText: {
    flex: 1,
    ...Typography.body,
    color: Colors.light.text,
  },
  productItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.xs,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  productInfo: {
    flex: 1,
  },
  productName: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: '600',
  },
  productCategory: {
    ...Typography.bodySmall,
    color: Colors.light.textSecondary,
    marginTop: Spacing.xs,
  },

  productPrice: {
    alignItems: 'flex-end',
    marginRight: Spacing.sm,
  },
  priceText: {
    ...Typography.body,
    color: Colors.light.primary,
    fontWeight: '600',
  },
  stockText: {
    ...Typography.bodySmall,
    color: Colors.light.textSecondary,
    marginTop: Spacing.xs,
  },
  emptyState: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: Spacing.xl,
  },
     emptyStateText: {
     ...Typography.h3,
     color: Colors.light.text,
     textAlign: 'center',
     marginTop: Spacing.md,
   },
  emptyStateSubtext: {
    ...Typography.body,
    color: Colors.light.textSecondary,
    textAlign: 'center',
    marginTop: Spacing.sm,
  },
});

export default QuickProductSearch; 