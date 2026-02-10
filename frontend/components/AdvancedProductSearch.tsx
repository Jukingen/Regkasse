import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TextInput,
  TouchableOpacity,
  ScrollView,
  FlatList,
  Modal,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';
import { Product } from '../services/api/productService';

interface AdvancedProductSearchProps {
  products: Product[];
  onProductSelect: (product: Product) => void;
  onSearch: (query: string) => void;
  onFilterChange: (filters: ProductFilters) => void;
}

interface ProductFilters {
  category?: string;
  priceRange?: { min: number; max: number };
  inStock?: boolean;
  taxType?: string;
}

const AdvancedProductSearch: React.FC<AdvancedProductSearchProps> = ({
  products,
  onProductSelect,
  onSearch,
  onFilterChange,
}) => {
  const { t } = useTranslation();
  const [searchQuery, setSearchQuery] = useState('');
  const [showFilters, setShowFilters] = useState(false);
  const [filters, setFilters] = useState<ProductFilters>({});
  const [filteredProducts, setFilteredProducts] = useState<Product[]>([]);
  const [recentSearches, setRecentSearches] = useState<string[]>([]);

  // Kategorileri al
  const categories = [...new Set(products.map(p => p.category))];
  const taxTypes = ['standard', 'reduced', 'special'];

  // Arama işlevi
  const handleSearch = (query: string) => {
    setSearchQuery(query);
    onSearch(query);

    // Son aramaları güncelle
    if (query.trim()) {
      setRecentSearches(prev => {
        const newSearches = [query, ...prev.filter(s => s !== query)].slice(0, 5);
        return newSearches;
      });
    }
  };

  // Filtreleri uygula
  const applyFilters = (newFilters: ProductFilters) => {
    setFilters(newFilters);
    onFilterChange(newFilters);
  };

  // Ürünleri filtrele
  useEffect(() => {
    let filtered = products;

    // Arama filtresi
    if (searchQuery) {
      filtered = filtered.filter(product =>
        product.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        product.description?.toLowerCase().includes(searchQuery.toLowerCase()) ||
        product.category?.toLowerCase().includes(searchQuery.toLowerCase())
      );
    }

    // Kategori filtresi
    if (filters.category) {
      filtered = filtered.filter(product => product.category === filters.category);
    }

    // Stok filtresi
    if (filters.inStock) {
      filtered = filtered.filter(product => product.stock > 0);
    }

    // Vergi tipi filtresi
    if (filters.taxType) {
      filtered = filtered.filter(product => product.taxType === filters.taxType);
    }

    // Fiyat aralığı filtresi
    if (filters.priceRange) {
      filtered = filtered.filter(product =>
        product.price >= filters.priceRange!.min && product.price <= filters.priceRange!.max
      );
    }

    setFilteredProducts(filtered);
  }, [products, searchQuery, filters]);

  const renderProductItem = ({ item }: { item: Product }) => (
    <TouchableOpacity
      style={styles.productItem}
      onPress={() => onProductSelect(item)}
    >
      <View style={styles.productInfo}>
        <Text style={styles.productName}>{item.name}</Text>
        <Text style={styles.productDescription}>{item.description}</Text>
        <View style={styles.productMeta}>
          <Text style={styles.productCategory}>{item.category}</Text>
          <Text style={styles.productStock}>
            {t('product.stock')}: {item.stock}
          </Text>
        </View>
      </View>
      <View style={styles.productPrice}>
        <Text style={styles.priceText}>€{item.price.toFixed(2)}</Text>
        <Text style={styles.taxText}>
          {t(`tax.${item.taxType}`)} ({Math.round(getTaxRate(item.taxType) * 100)}%)
        </Text>
      </View>
    </TouchableOpacity>
  );

  const getTaxRate = (taxType: string) => {
    switch (taxType) {
      case 'standard': return 0.20;
      case 'reduced': return 0.10;
      case 'special': return 0.13;
      default: return 0.20;
    }
  };

  return (
    <View style={styles.container}>
      {/* Arama Çubuğu */}
      <View style={styles.searchContainer}>
        <View style={styles.searchInputContainer}>
          <Ionicons name="search" size={20} color={Colors.light.textSecondary} />
          <TextInput
            style={styles.searchInput}
            placeholder={t('search.placeholder')}
            value={searchQuery}
            onChangeText={handleSearch}
            returnKeyType="search"
          />
          {searchQuery.length > 0 && (
            <TouchableOpacity onPress={() => handleSearch('')}>
              <Ionicons name="close-circle" size={20} color={Colors.light.textSecondary} />
            </TouchableOpacity>
          )}
        </View>

        <TouchableOpacity
          style={styles.filterButton}
          onPress={() => setShowFilters(true)}
        >
          <Ionicons name="filter" size={20} color={Colors.light.primary} />
        </TouchableOpacity>
      </View>

      {/* Son Aramalar */}
      {!searchQuery && recentSearches.length > 0 && (
        <View style={styles.recentSearches}>
          <Text style={styles.recentTitle}>{t('search.recentSearches')}</Text>
          <ScrollView horizontal showsHorizontalScrollIndicator={false}>
            {recentSearches.map((search, index) => (
              <TouchableOpacity
                key={index}
                style={styles.recentSearchItem}
                onPress={() => handleSearch(search)}
              >
                <Text style={styles.recentSearchText}>{search}</Text>
              </TouchableOpacity>
            ))}
          </ScrollView>
        </View>
      )}

      {/* Ürün Listesi */}
      <FlatList
        data={filteredProducts}
        keyExtractor={(item) => item.id}
        renderItem={renderProductItem}
        showsVerticalScrollIndicator={false}
        ListEmptyComponent={
          <View style={styles.emptyState}>
            <Ionicons name="search-outline" size={48} color={Colors.light.textSecondary} />
            <Text style={styles.emptyStateText}>
              {searchQuery ? t('search.noResults') : t('search.startTyping')}
            </Text>
          </View>
        }
      />

      {/* Filtre Modal */}
      <Modal
        visible={showFilters}
        animationType="slide"
        transparent
        onRequestClose={() => setShowFilters(false)}
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>{t('search.filters')}</Text>
              <TouchableOpacity onPress={() => setShowFilters(false)}>
                <Ionicons name="close" size={24} color={Colors.light.text} />
              </TouchableOpacity>
            </View>

            <ScrollView style={styles.filterContent}>
              {/* Kategori Filtresi */}
              <View style={styles.filterSection}>
                <Text style={styles.filterSectionTitle}>{t('search.categories')}</Text>
                <View style={styles.filterOptions}>
                  {categories.map(category => (
                    <TouchableOpacity
                      key={category}
                      style={[
                        styles.filterOption,
                        filters.category === category && styles.filterOptionActive
                      ]}
                      onPress={() => applyFilters({
                        ...filters,
                        category: filters.category === category ? undefined : category
                      })}
                    >
                      <Text style={[
                        styles.filterOptionText,
                        filters.category === category && styles.filterOptionTextActive
                      ]}>
                        {t(`categories.${category}`)}
                      </Text>
                    </TouchableOpacity>
                  ))}
                </View>
              </View>

              {/* Stok Filtresi */}
              <View style={styles.filterSection}>
                <Text style={styles.filterSectionTitle}>{t('search.stock')}</Text>
                <TouchableOpacity
                  style={[
                    styles.filterOption,
                    filters.inStock && styles.filterOptionActive
                  ]}
                  onPress={() => applyFilters({
                    ...filters,
                    inStock: !filters.inStock
                  })}
                >
                  <Text style={[
                    styles.filterOptionText,
                    filters.inStock && styles.filterOptionTextActive
                  ]}>
                    {t('search.inStockOnly')}
                  </Text>
                </TouchableOpacity>
              </View>

              {/* Vergi Tipi Filtresi */}
              <View style={styles.filterSection}>
                <Text style={styles.filterSectionTitle}>{t('search.taxTypes')}</Text>
                <View style={styles.filterOptions}>
                  {taxTypes.map(taxType => (
                    <TouchableOpacity
                      key={taxType}
                      style={[
                        styles.filterOption,
                        filters.taxType === taxType && styles.filterOptionActive
                      ]}
                      onPress={() => applyFilters({
                        ...filters,
                        taxType: filters.taxType === taxType ? undefined : taxType
                      })}
                    >
                      <Text style={[
                        styles.filterOptionText,
                        filters.taxType === taxType && styles.filterOptionTextActive
                      ]}>
                        {t(`tax.${taxType}`)} ({Math.round(getTaxRate(taxType) * 100)}%)
                      </Text>
                    </TouchableOpacity>
                  ))}
                </View>
              </View>
            </ScrollView>

            <View style={styles.modalFooter}>
              <TouchableOpacity
                style={styles.clearFiltersButton}
                onPress={() => {
                  setFilters({});
                  onFilterChange({});
                }}
              >
                <Text style={styles.clearFiltersText}>{t('search.clearFilters')}</Text>
              </TouchableOpacity>

              <TouchableOpacity
                style={styles.applyFiltersButton}
                onPress={() => setShowFilters(false)}
              >
                <Text style={styles.applyFiltersText}>{t('search.applyFilters')}</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: Spacing.medium,
  },
  searchInputContainer: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.medium,
    paddingHorizontal: Spacing.medium,
    marginRight: Spacing.small,
  },
  searchInput: {
    flex: 1,
    paddingVertical: Spacing.medium,
    paddingHorizontal: Spacing.small,
    ...Typography.body,
    color: Colors.light.text,
  },
  filterButton: {
    padding: Spacing.medium,
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.medium,
  },
  recentSearches: {
    marginBottom: Spacing.medium,
  },
  recentTitle: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    marginBottom: Spacing.small,
  },
  recentSearchItem: {
    backgroundColor: Colors.light.background,
    paddingHorizontal: Spacing.medium,
    paddingVertical: Spacing.small,
    borderRadius: BorderRadius.medium,
    marginRight: Spacing.small,
  },
  recentSearchText: {
    ...Typography.caption,
    color: Colors.light.text,
  },
  productItem: {
    flexDirection: 'row',
    backgroundColor: Colors.light.background,
    padding: Spacing.medium,
    borderRadius: BorderRadius.medium,
    marginBottom: Spacing.small,
  },
  productInfo: {
    flex: 1,
  },
  productName: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: '600',
  },
  productDescription: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    marginTop: Spacing.xsmall,
  },
  productMeta: {
    flexDirection: 'row',
    marginTop: Spacing.small,
  },
  productCategory: {
    ...Typography.caption,
    color: Colors.light.primary,
    marginRight: Spacing.medium,
  },
  productStock: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
  },
  productPrice: {
    alignItems: 'flex-end',
  },
  priceText: {
    ...Typography.h3,
    color: Colors.light.text,
    fontWeight: '600',
  },
  taxText: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    marginTop: Spacing.xsmall,
  },
  emptyState: {
    alignItems: 'center',
    padding: Spacing.large,
  },
  emptyStateText: {
    ...Typography.body,
    color: Colors.light.textSecondary,
    textAlign: 'center',
    marginTop: Spacing.medium,
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'flex-end',
  },
  modalContent: {
    backgroundColor: Colors.light.background,
    borderTopLeftRadius: BorderRadius.large,
    borderTopRightRadius: BorderRadius.large,
    maxHeight: '80%',
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.medium,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  modalTitle: {
    ...Typography.h3,
    color: Colors.light.text,
  },
  filterContent: {
    padding: Spacing.medium,
  },
  filterSection: {
    marginBottom: Spacing.large,
  },
  filterSectionTitle: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: '600',
    marginBottom: Spacing.medium,
  },
  filterOptions: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: Spacing.small,
  },
  filterOption: {
    paddingHorizontal: Spacing.medium,
    paddingVertical: Spacing.small,
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.medium,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  filterOptionActive: {
    backgroundColor: Colors.light.primary,
    borderColor: Colors.light.primary,
  },
  filterOptionText: {
    ...Typography.caption,
    color: Colors.light.text,
  },
  filterOptionTextActive: {
    color: 'white',
  },
  modalFooter: {
    flexDirection: 'row',
    padding: Spacing.medium,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
  },
  clearFiltersButton: {
    flex: 1,
    paddingVertical: Spacing.medium,
    marginRight: Spacing.small,
  },
  clearFiltersText: {
    ...Typography.body,
    color: Colors.light.textSecondary,
    textAlign: 'center',
  },
  applyFiltersButton: {
    flex: 1,
    backgroundColor: Colors.light.primary,
    paddingVertical: Spacing.medium,
    borderRadius: BorderRadius.medium,
    marginLeft: Spacing.small,
  },
  applyFiltersText: {
    ...Typography.body,
    color: 'white',
    textAlign: 'center',
    fontWeight: '600',
  },
});

export default AdvancedProductSearch; 