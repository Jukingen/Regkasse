import React, { useState, useEffect, useCallback } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  FlatList,
  ActivityIndicator,
  RefreshControl,
  Dimensions,
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { Product } from '../services/api/productService';
import { useProductsUnified } from '../hooks/useProductsUnified';
import { Colors } from '../constants/Colors';

// Ekran boyutlarını al
const { width: screenWidth } = Dimensions.get('window');
const isTablet = screenWidth > 768;

/**
 * Mobile-optimized ürün listesi komponenti
 * Grid layout ve responsive tasarım
 */
interface ProductListProps {
  categoryFilter?: string;
  stockStatusFilter?: 'in-stock' | 'out-of-stock' | 'low-stock';
  searchQuery?: string;
  onProductSelect?: (product: Product) => void;
  showStockInfo?: boolean;
  showTaxInfo?: boolean;
}

export const ProductList: React.FC<ProductListProps> = ({
  categoryFilter,
  stockStatusFilter,
  searchQuery,
  onProductSelect,
  showStockInfo = true,
  showTaxInfo = true,
}) => {
  const { t } = useTranslation();
  
  // Unified products hook'unu kullan
  const {
    products: cachedProducts,
    categories: cachedCategories,
    loading: cacheLoading,
    error: cacheError,
    refreshData,
    searchProducts
  } = useProductsUnified();
  
  const [products, setProducts] = useState<Product[]>([]);
  const [refreshing, setRefreshing] = useState(false);
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid'); // Grid/List toggle

  // Grid için sütun sayısını hesapla
  const getGridColumns = () => {
    if (isTablet) return 3;
    return 2; // Mobile: 2 sütun
  };

  // Ürünleri yükle
  const loadProducts = useCallback(async (refresh: boolean = false) => {
    try {
      let productsData: Product[];

      console.log(`🔍 ProductList: Filtering products:`, {
        categoryFilter,
        stockStatusFilter,
        searchQuery,
        totalCachedProducts: cachedProducts.length,
        productsWithCategories: cachedProducts.map(p => ({
          name: p.name,
          category: p.category,
          productCategory: p.productCategory
        }))
      });

      if (searchQuery) {
        // Arama yapılıyorsa
        productsData = searchProducts(searchQuery).filter(p => {
          const productCategory = p.productCategory || p.category;
          return categoryFilter ? productCategory === categoryFilter : true;
        });
      } else if (categoryFilter) {
        // Kategori filtresi varsa
        productsData = cachedProducts.filter(p => {
          const productCategory = p.productCategory || p.category;
          const matches = productCategory === categoryFilter;
          console.log(`🔍 Product ${p.name}: category=${productCategory}, filter=${categoryFilter}, matches=${matches}`);
          return matches;
        });
      } else if (stockStatusFilter) {
        // Stok durumu filtresi varsa
        productsData = cachedProducts.filter(p => {
          switch (stockStatusFilter) {
            case 'in-stock': return p.stockQuantity > (p.minStockLevel || 0);
            case 'out-of-stock': return p.stockQuantity === 0;
            case 'low-stock': return p.stockQuantity <= (p.minStockLevel || 0) && p.stockQuantity > 0;
            default: return true;
          }
        });
      } else {
        // Tüm ürünleri kullan
        productsData = cachedProducts;
      }

      console.log(`🔍 ProductList: Filtered ${productsData.length} products for category: ${categoryFilter || 'all'}`);
      setProducts(productsData);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : t('common.errorLoadingProducts');
      console.error('Error loading products:', err);
    }
  }, [categoryFilter, stockStatusFilter, searchQuery, cachedProducts, searchProducts, t]);

  // İlk yükleme
  useEffect(() => {
    if (cachedProducts.length > 0) {
      loadProducts(false);
    }
  }, [cachedProducts.length, loadProducts]);

  // Kategori filtresi değiştiğinde ürünleri yeniden yükle
  useEffect(() => {
    if (cachedProducts.length > 0) {
      loadProducts(false);
    }
  }, [categoryFilter, cachedProducts.length, loadProducts]);

  // Debug: API çağrılarını kontrol et
  useEffect(() => {
    console.log('🔍 ProductList: Cache state changed', {
      productsCount: cachedProducts.length,
      categoriesCount: cachedCategories.length,
      loading: cacheLoading,
      error: cacheError
    });
  }, [cachedProducts.length, cachedCategories.length, cacheLoading, cacheError]);

  // Yenileme
  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    await refreshData();
    setRefreshing(false);
  }, [refreshData]);

  // Ürün seçimi
  const handleProductSelect = (product: Product) => {
    if (onProductSelect) {
      onProductSelect(product);
    }
  };

  // Stok durumu rengi
  const getStockStatusColor = (quantity: number, minLevel: number) => {
    if (quantity <= 0) return Colors.light.error;
    if (quantity <= minLevel) return Colors.light.warning;
    return Colors.light.success;
  };

  // Vergi tipi rengi
  const getTaxTypeColor = (taxType: string) => {
    switch (taxType) {
      case 'Standard': return Colors.light.primary;
      case 'Reduced': return Colors.light.secondary;
      case 'Special': return Colors.light.info;
      case 'Exempt': return Colors.light.success;
      default: return Colors.light.text;
    }
  };

  // Grid ürün render (mobile-optimized)
  const renderGridProduct = ({ item }: { item: Product }) => (
    <TouchableOpacity
      style={styles.gridProductItem}
      onPress={() => handleProductSelect(item)}
      activeOpacity={0.7}
    >
      {/* Ürün resmi placeholder */}
      <View style={styles.productImagePlaceholder}>
        <Text style={styles.productImageText}>
          {item.name?.charAt(0)?.toUpperCase() || 'P'}
        </Text>
      </View>

      {/* Ürün bilgileri */}
      <View style={styles.gridProductInfo}>
        <Text style={styles.gridProductName} numberOfLines={2}>
          {item.name}
        </Text>
        
        <Text style={styles.gridProductPrice}>
          €{item.price?.toFixed(2) || '0.00'}
        </Text>

        {/* Kategori badge */}
        <View style={styles.categoryBadge}>
          <Text style={styles.categoryBadgeText}>
            {item.productCategory || item.category}
          </Text>
        </View>

        {/* Stok durumu */}
        {showStockInfo && (
          <View style={styles.stockBadge}>
            <View style={[
              styles.stockIndicator,
              { backgroundColor: getStockStatusColor(item.stockQuantity || 0, item.minStockLevel || 0) }
            ]} />
            <Text style={styles.stockText}>
              {item.stockQuantity || 0}
            </Text>
          </View>
        )}
      </View>
    </TouchableOpacity>
  );

  // List ürün render (mevcut)
  const renderListProduct = ({ item }: { item: Product }) => (
    <TouchableOpacity
      style={styles.listProductItem}
      onPress={() => handleProductSelect(item)}
      activeOpacity={0.7}
    >
      <View style={styles.listProductHeader}>
        <Text style={styles.listProductName} numberOfLines={2}>
          {item.name}
        </Text>
        <Text style={styles.listProductPrice}>
          €{item.price?.toFixed(2) || '0.00'}
        </Text>
      </View>

      <View style={styles.listProductDetails}>
        {item.description && (
          <Text style={styles.listProductDescription} numberOfLines={2}>
            {item.description}
          </Text>
        )}

        <View style={styles.listProductMeta}>
          <Text style={styles.listProductCategory}>
            {item.productCategory || item.category}
          </Text>
          
          {showStockInfo && (
            <View style={styles.listStockInfo}>
              <View style={[
                styles.listStockIndicator,
                { backgroundColor: getStockStatusColor(item.stockQuantity || 0, item.minStockLevel || 0) }
              ]} />
              <Text style={styles.listStockQuantity}>
                {item.stockQuantity || 0} {item.unit || 'Stück'}
              </Text>
            </View>
          )}

          {showTaxInfo && (
            <View style={styles.listTaxInfo}>
              <Text style={[
                styles.listTaxType,
                { color: getTaxTypeColor(item.taxType || 'Standard') }
              ]}>
                {t(`taxType.${(item.taxType || 'Standard').toLowerCase()}`)}
              </Text>
            </View>
          )}
        </View>
      </View>
    </TouchableOpacity>
  );

  // View mode toggle
  const renderViewModeToggle = () => (
    <View style={styles.viewModeToggle}>
      <TouchableOpacity
        style={[styles.toggleButton, viewMode === 'grid' && styles.toggleButtonActive]}
        onPress={() => setViewMode('grid')}
      >
        <Text style={[styles.toggleButtonText, viewMode === 'grid' && styles.toggleButtonTextActive]}>
          Grid
        </Text>
      </TouchableOpacity>
      <TouchableOpacity
        style={[styles.toggleButton, viewMode === 'list' && styles.toggleButtonActive]}
        onPress={() => setViewMode('list')}
      >
        <Text style={[styles.toggleButtonText, viewMode === 'list' && styles.toggleButtonTextActive]}>
          List
        </Text>
      </TouchableOpacity>
    </View>
  );

  // Yükleme komponenti
  const renderFooter = () => {
    if (!cacheLoading) return null;
    
    return (
      <View style={styles.loadingFooter}>
        <ActivityIndicator size="small" color={Colors.light.primary} />
        <Text style={styles.loadingText}>{t('common.loadingMore')}</Text>
      </View>
    );
  };

  // Hata komponenti
  if (cacheError) {
    return (
      <View style={styles.errorContainer}>
        <Text style={styles.errorText}>{cacheError}</Text>
        <TouchableOpacity style={styles.retryButton} onPress={() => refreshData()}>
          <Text style={styles.retryButtonText}>{t('common.retry')}</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      {/* View Mode Toggle */}
      {renderViewModeToggle()}

      {/* Products Grid/List */}
      <FlatList
        data={products}
        renderItem={viewMode === 'grid' ? renderGridProduct : renderListProduct}
        keyExtractor={(item) => item.id || Math.random().toString()}
        numColumns={viewMode === 'grid' ? getGridColumns() : 1}
        contentContainerStyle={[
          styles.listContainer,
          viewMode === 'grid' && styles.gridContainer
        ]}
        refreshControl={
          <RefreshControl
            refreshing={refreshing}
            onRefresh={onRefresh}
            colors={[Colors.light.primary]}
            tintColor={Colors.light.primary}
          />
        }
        ListFooterComponent={renderFooter}
        ListEmptyComponent={
          !cacheLoading ? (
            <View style={styles.emptyContainer}>
              <Text style={styles.emptyText}>
                {searchQuery ? t('common.noSearchResults') : t('common.noProductsFound')}
              </Text>
            </View>
          ) : null
        }
        showsVerticalScrollIndicator={false}
        showsHorizontalScrollIndicator={false}
      />
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: Colors.light.background,
  },
  listContainer: {
    padding: 8, // 16'dan 8'e düşürüldü
  },
  gridContainer: {
    padding: 8, // Grid modunda padding ekleniyor
  },
  productItem: {
    backgroundColor: Colors.light.surface,
    borderRadius: 8, // 12'den 8'e düşürüldü
    padding: 12, // 16'dan 12'ye düşürüldü
    marginBottom: 8, // 12'den 8'e düşürüldü
    elevation: 1, // 2'den 1'e düşürüldü
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 }, // 2'den 1'e düşürüldü
    shadowOpacity: 0.08, // 0.1'den 0.08'e düşürüldü
    shadowRadius: 2, // 4'ten 2'ye düşürüldü
  },
  listProductItem: {
    flexDirection: 'row',
    backgroundColor: Colors.light.surface,
    borderRadius: 8,
    padding: 12,
    marginBottom: 8,
    elevation: 1,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.08,
    shadowRadius: 2,
  },
  listProductHeader: {
    flex: 1,
    marginRight: 12,
  },
  listProductName: {
    fontSize: 14,
    fontWeight: '600',
    color: Colors.light.text,
    marginBottom: 4,
  },
  listProductPrice: {
    fontSize: 16,
    fontWeight: '700',
    color: Colors.light.primary,
  },
  listProductDetails: {
    flex: 1,
  },
  listProductDescription: {
    fontSize: 12,
    color: Colors.light.textSecondary,
    marginBottom: 6,
    lineHeight: 16,
  },
  listProductMeta: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 6,
  },
  listProductCategory: {
    fontSize: 10,
    color: Colors.light.textSecondary,
    backgroundColor: Colors.light.surface,
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 8,
  },
  listStockInfo: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  listStockIndicator: {
    width: 6,
    height: 6,
    borderRadius: 3,
    marginRight: 4,
  },
  listStockQuantity: {
    fontSize: 10,
    color: Colors.light.textSecondary,
  },
  listTaxInfo: {
    alignItems: 'flex-end',
  },
  listTaxType: {
    fontSize: 10,
    fontWeight: '500',
    backgroundColor: Colors.light.surface,
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 8,
  },
  gridProductItem: {
    flex: 1, // Grid modunda eşit genişlik
    backgroundColor: Colors.light.surface,
    borderRadius: 8,
    margin: 4, // Grid modunda margin ekleniyor
    elevation: 1,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.08,
    shadowRadius: 2,
  },
  productImagePlaceholder: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: Colors.light.surface,
  },
  productImageText: {
    fontSize: 24,
    color: Colors.light.primary,
  },
  gridProductInfo: {
    padding: 8,
    flex: 1,
  },
  gridProductName: {
    fontSize: 12,
    fontWeight: '600',
    color: Colors.light.text,
    marginBottom: 4,
  },
  gridProductPrice: {
    fontSize: 14,
    fontWeight: '700',
    color: Colors.light.primary,
  },
  categoryBadge: {
    backgroundColor: Colors.light.surface,
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 6,
    alignSelf: 'flex-start',
    marginTop: 6,
  },
  categoryBadgeText: {
    fontSize: 9,
    color: Colors.light.primary,
    fontWeight: '500',
  },
  stockBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 6,
    backgroundColor: Colors.light.surface,
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 6,
    alignSelf: 'flex-start',
  },
  stockIndicator: {
    width: 6,
    height: 6,
    borderRadius: 3,
    marginRight: 4,
  },
  stockText: {
    fontSize: 10,
    color: Colors.light.textSecondary,
  },
  viewModeToggle: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 8,
    backgroundColor: Colors.light.surface,
    borderRadius: 8,
    paddingVertical: 4,
    paddingHorizontal: 12,
  },
  toggleButton: {
    paddingVertical: 8,
    paddingHorizontal: 16,
    borderRadius: 6,
  },
  toggleButtonActive: {
    backgroundColor: Colors.light.primary,
  },
  toggleButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: Colors.light.textSecondary,
  },
  toggleButtonTextActive: {
    color: '#FFFFFF',
  },
  loadingFooter: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 16,
  },
  loadingText: {
    marginLeft: 8,
    color: Colors.light.textSecondary,
  },
  errorContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 32,
  },
  errorText: {
    fontSize: 16,
    color: Colors.light.error,
    textAlign: 'center',
    marginBottom: 16,
  },
  retryButton: {
    backgroundColor: Colors.light.primary,
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 8,
  },
  retryButtonText: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '600',
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 32,
  },
  emptyText: {
    fontSize: 16,
    color: Colors.light.textSecondary,
    textAlign: 'center',
  },
}); 