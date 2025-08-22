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
    searchProducts: searchProductsInCache
  } = useProductsUnified();
  
  const [products, setProducts] = useState<Product[]>([]);
  const [refreshing, setRefreshing] = useState(false);

  // Ürünleri yükle - cache hook'undan gelen verileri kullan
  const loadProducts = useCallback(async (refresh: boolean = false) => {
    try {
      let productsData: Product[];

      if (searchQuery) {
        // Arama yapılıyorsa - unified hook cache üzerinden
        productsData = searchProductsInCache(searchQuery).filter(p =>
          categoryFilter ? p.category === categoryFilter : true
        );
      } else if (categoryFilter) {
        // Kategori filtresi varsa - cache'den filtrele
        productsData = cachedProducts.filter(p => p.category === categoryFilter);
      } else if (stockStatusFilter) {
        // Stok durumu filtresi varsa - cache'den filtrele
        productsData = cachedProducts.filter(p => {
          switch (stockStatusFilter) {
            case 'in-stock': return p.stockQuantity > p.minStockLevel;
            case 'out-of-stock': return p.stockQuantity === 0;
            case 'low-stock': return p.stockQuantity <= p.minStockLevel && p.stockQuantity > 0;
            default: return true;
          }
        });
      } else {
        // Tüm ürünleri kullan
        productsData = cachedProducts;
      }

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
  }, [cachedProducts.length]); // ✅ YENİ: loadProducts dependency kaldırıldı, sadece length kontrol

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
            {item.category}
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
    padding: 16,
  },
  productItem: {
    backgroundColor: Colors.light.surface,
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
    elevation: 2,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
  },
  productHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: 8,
  },
  productName: {
    fontSize: 16,
    fontWeight: '600',
    color: Colors.light.text,
    flex: 1,
    marginRight: 12,
  },
  productPrice: {
    fontSize: 18,
    fontWeight: '700',
    color: Colors.light.primary,
  },
  productDetails: {
    marginBottom: 8,
  },
  productDescription: {
    fontSize: 14,
    color: Colors.light.textSecondary,
    marginBottom: 8,
    lineHeight: 20,
  },
  productMeta: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  productCategory: {
    fontSize: 12,
    color: Colors.light.textSecondary,
    backgroundColor: Colors.light.surface,
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 12,
  },
  stockInfo: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  stockIndicator: {
    width: 8,
    height: 8,
    borderRadius: 4,
    marginRight: 6,
  },
  stockQuantity: {
    fontSize: 12,
    color: Colors.light.textSecondary,
  },
  taxInfo: {
    alignItems: 'flex-end',
  },
  taxType: {
    fontSize: 12,
    fontWeight: '500',
    backgroundColor: Colors.light.surface,
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 12,
  },
  complianceInfo: {
    marginTop: 8,
    padding: 8,
    backgroundColor: Colors.light.surface,
    borderRadius: 8,
  },
  complianceRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 4,
  },
  complianceIndicator: {
    width: 8,
    height: 8,
    borderRadius: 4,
    marginRight: 8,
  },
  complianceText: {
    fontSize: 11,
    fontWeight: '500',
    color: Colors.light.text,
    flex: 1,
  },
  rksvType: {
    fontSize: 10,
    color: Colors.light.textSecondary,
    backgroundColor: Colors.light.surface,
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 8,
  },
  exemptionReason: {
    fontSize: 10,
    color: Colors.light.warning,
    fontStyle: 'italic',
    marginTop: 2,
  },
  fiscalCode: {
    fontSize: 10,
    color: Colors.light.textSecondary,
    marginTop: 2,
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