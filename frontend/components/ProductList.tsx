import React, { useState, useEffect, useCallback } from 'react';
import {
  View,
  Text,
  FlatList,
  TouchableOpacity,
  StyleSheet,
  ActivityIndicator,
  RefreshControl,
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { Product } from '../services/api/productService';
import { useProductsUnified } from '../hooks/useProductsUnified';
import { Colors } from '../constants/Colors';

/**
 * Ürün listesi komponenti - RKSV uyumlu ürün yönetimi
 * Filtreleme ve arama özellikleri ile, sayfalama olmadan
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
  
  // Unified products hook'unu kullan - tek kaynak, duplicate fetch yok
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

  // Ürünleri yükle - cache hook'undan gelen verileri kullan
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
        // Arama yapılıyorsa - unified hook cache üzerinden
        productsData = searchProducts(searchQuery).filter(p => {
          const productCategory = p.productCategory || p.category;
          return categoryFilter ? productCategory === categoryFilter : true;
        });
      } else if (categoryFilter) {
        // Kategori filtresi varsa - cache'den filtrele
        productsData = cachedProducts.filter(p => {
          const productCategory = p.productCategory || p.category;
          const matches = productCategory === categoryFilter;
          console.log(`🔍 Product ${p.name}: category=${productCategory}, filter=${categoryFilter}, matches=${matches}`);
          return matches;
        });
      } else if (stockStatusFilter) {
        // Stok durumu filtresi varsa - cache'den filtrele
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
  }, [categoryFilter, stockStatusFilter, searchQuery]); // ✅ YENİ: Minimal dependency - cachedProducts ve t kaldırıldı

  // İlk yükleme - cache'den veriler hazır olduğunda
  useEffect(() => {
    if (cachedProducts.length > 0) {
      loadProducts(false);
    }
  }, [cachedProducts.length, loadProducts]); // loadProducts dependency eklendi

  // Kategori filtresi değiştiğinde ürünleri yeniden yükle
  useEffect(() => {
    if (cachedProducts.length > 0) {
      loadProducts(false);
    }
  }, [categoryFilter, cachedProducts.length, loadProducts]); // categoryFilter değişikliğini dinle

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

  // RKSV compliance status rengi
  const getComplianceStatusColor = (isFiscalCompliant: boolean, isTaxable: boolean) => {
    if (!isFiscalCompliant) return Colors.light.error;
    if (!isTaxable) return Colors.light.warning;
    return Colors.light.success;
  };

  // Ürün render
  const renderProduct = ({ item }: { item: Product }) => (
    <TouchableOpacity
      style={styles.productItem}
      onPress={() => handleProductSelect(item)}
      activeOpacity={0.7}
    >
      <View style={styles.productHeader}>
        <Text style={styles.productName} numberOfLines={2}>
          {item.name}
        </Text>
        <Text style={styles.productPrice}>
          €{item.price.toFixed(2)}
        </Text>
      </View>

      <View style={styles.productDetails}>
        {item.description && (
          <Text style={styles.productDescription} numberOfLines={2}>
            {item.description}
          </Text>
        )}

        <View style={styles.productMeta}>
          <Text style={styles.productCategory}>
            {item.productCategory || item.category}
          </Text>
          
          {showStockInfo && (
            <View style={styles.stockInfo}>
              <View style={[
                styles.stockIndicator,
                { backgroundColor: getStockStatusColor(item.stockQuantity, item.minStockLevel || 0) }
              ]} />
              <Text style={styles.stockQuantity}>
                {item.stockQuantity} {item.unit}
              </Text>
            </View>
          )}

          {showTaxInfo && (
            <View style={styles.taxInfo}>
              <Text style={[
                styles.taxType,
                { color: getTaxTypeColor(item.taxType) }
              ]}>
                {t(`taxType.${item.taxType.toLowerCase()}`)}
              </Text>
            </View>
          )}
        </View>

        {/* RKSV Compliance bilgileri */}
        <View style={styles.complianceInfo}>
          <View style={styles.complianceRow}>
            <View style={[
              styles.complianceIndicator,
              { backgroundColor: getComplianceStatusColor(item.isFiscalCompliant, item.isTaxable) }
            ]} />
            <Text style={styles.complianceText}>
              {item.isFiscalCompliant ? t('rksv.compliant') : t('rksv.nonCompliant')}
            </Text>
            {item.rksvProductType && (
              <Text style={styles.rksvType}>
                {item.rksvProductType}
              </Text>
            )}
          </View>
          
          {!item.isTaxable && item.taxExemptionReason && (
            <Text style={styles.exemptionReason} numberOfLines={1}>
              {t('rksv.exemptionReason')}: {item.taxExemptionReason}
            </Text>
          )}
          
          {item.fiscalCategoryCode && (
            <Text style={styles.fiscalCode}>
              {t('rksv.fiscalCode')}: {item.fiscalCategoryCode}
            </Text>
          )}
        </View>

      </View>
    </TouchableOpacity>
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
      <FlatList
        data={products}
        renderItem={renderProduct}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.listContainer}
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
  productHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: 6, // 8'den 6'ya düşürüldü
  },
  productName: {
    fontSize: 14, // 16'dan 14'e düşürüldü
    fontWeight: '600',
    color: Colors.light.text,
    flex: 1,
    marginRight: 8, // 12'den 8'e düşürüldü
  },
  productPrice: {
    fontSize: 16, // 18'den 16'ya düşürüldü
    fontWeight: '700',
    color: Colors.light.primary,
  },
  productDetails: {
    marginBottom: 6, // 8'den 6'ya düşürüldü
  },
  productDescription: {
    fontSize: 12, // 14'ten 12'ye düşürüldü
    color: Colors.light.textSecondary,
    marginBottom: 6, // 8'den 6'ya düşürüldü
    lineHeight: 16, // 20'den 16'ya düşürüldü
  },
  productMeta: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 6, // 8'den 6'ya düşürüldü
  },
  productCategory: {
    fontSize: 10, // 12'den 10'a düşürüldü
    color: Colors.light.textSecondary,
    backgroundColor: Colors.light.surface,
    paddingHorizontal: 6, // 8'den 6'ya düşürüldü
    paddingVertical: 2, // 4'ten 2'ye düşürüldü
    borderRadius: 8, // 12'den 8'e düşürüldü
  },
  stockInfo: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  stockIndicator: {
    width: 6, // 8'den 6'ya düşürüldü
    height: 6, // 8'den 6'ya düşürüldü
    borderRadius: 3, // 4'ten 3'e düşürüldü
    marginRight: 4, // 6'dan 4'e düşürüldü
  },
  stockQuantity: {
    fontSize: 10, // 12'den 10'a düşürüldü
    color: Colors.light.textSecondary,
  },
  taxInfo: {
    alignItems: 'flex-end',
  },
  taxType: {
    fontSize: 10, // 12'den 10'a düşürüldü
    fontWeight: '500',
    backgroundColor: Colors.light.surface,
    paddingHorizontal: 6, // 8'den 6'ya düşürüldü
    paddingVertical: 2, // 4'ten 2'ye düşürüldü
    borderRadius: 8, // 12'den 8'e düşürüldü
  },
  complianceInfo: {
    marginTop: 6, // 8'den 6'ya düşürüldü
    padding: 6, // 8'den 6'ya düşürüldü
    backgroundColor: Colors.light.surface,
    borderRadius: 6, // 8'den 6'ya düşürüldü
  },
  complianceRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 2, // 4'ten 2'ye düşürüldü
  },
  complianceIndicator: {
    width: 6, // 8'den 6'ya düşürüldü
    height: 6, // 8'den 6'ya düşürüldü
    borderRadius: 3, // 4'ten 3'e düşürüldü
    marginRight: 6, // 8'den 6'ya düşürüldü
  },
  complianceText: {
    fontSize: 10, // 11'den 10'a düşürüldü
    fontWeight: '500',
    color: Colors.light.text,
    flex: 1,
  },
  rksvType: {
    fontSize: 9, // 10'dan 9'a düşürüldü
    color: Colors.light.textSecondary,
    backgroundColor: Colors.light.surface,
    paddingHorizontal: 4, // 6'dan 4'e düşürüldü
    paddingVertical: 1, // 2'den 1'e düşürüldü
    borderRadius: 6, // 8'den 6'ya düşürüldü
  },
  exemptionReason: {
    fontSize: 9, // 10'dan 9'a düşürüldü
    color: Colors.light.warning,
    fontStyle: 'italic',
    marginTop: 1, // 2'den 1'e düşürüldü
  },
  fiscalCode: {
    fontSize: 9, // 10'dan 9'a düşürüldü
    color: Colors.light.textSecondary,
    marginTop: 1, // 2'den 1'e düşürüldü
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