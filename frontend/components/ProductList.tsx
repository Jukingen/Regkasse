// Soft minimal product list with grid/list view modes
import React, { useState, useEffect, useCallback } from 'react';
import {
  View,
  Text,
  Pressable,
  StyleSheet,
  FlatList,
  ActivityIndicator,
  RefreshControl,
  Dimensions,
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { Product } from '../services/api/productService';
import { useProductsUnified } from '../hooks/useProductsUnified';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography, SoftShadows } from '../constants/SoftTheme';

const { width: screenWidth } = Dimensions.get('window');
const isTablet = screenWidth > 768;

interface ProductListProps {
  categoryFilter?: string;
  stockStatusFilter?: 'in-stock' | 'out-of-stock' | 'low-stock';
  searchQuery?: string;
  onProductSelect?: (product: Product) => void;
  showStockInfo?: boolean;
  showTaxInfo?: boolean;
  ListHeaderComponent?: React.ComponentType<any> | React.ReactElement | null;
  ListFooterComponent?: React.ComponentType<any> | React.ReactElement | null;
}

export const ProductList: React.FC<ProductListProps> = ({
  categoryFilter,
  stockStatusFilter,
  searchQuery,
  onProductSelect,
  showStockInfo = true,
  showTaxInfo = true,
  ListHeaderComponent,
  ListFooterComponent
}) => {
  const { t } = useTranslation();

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
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('list');

  const getGridColumns = () => isTablet ? 3 : 2;

  const loadProducts = useCallback(async (refresh: boolean = false) => {
    try {
      let productsData: Product[];

      if (searchQuery) {
        productsData = searchProducts(searchQuery).filter(p => {
          const productCategory = p.productCategory || p.category;
          return categoryFilter ? productCategory === categoryFilter : true;
        });
      } else if (categoryFilter) {
        productsData = cachedProducts.filter(p => {
          const productCategory = p.productCategory || p.category;
          return productCategory === categoryFilter;
        });
      } else if (stockStatusFilter) {
        productsData = cachedProducts.filter(p => {
          switch (stockStatusFilter) {
            case 'in-stock': return p.stockQuantity > (p.minStockLevel || 0);
            case 'out-of-stock': return p.stockQuantity === 0;
            case 'low-stock': return p.stockQuantity <= (p.minStockLevel || 0) && p.stockQuantity > 0;
            default: return true;
          }
        });
      } else {
        productsData = cachedProducts;
      }

      setProducts(productsData);
    } catch (err) {
      console.error('Error loading products:', err);
    }
  }, [categoryFilter, stockStatusFilter, searchQuery, cachedProducts, searchProducts]);

  useEffect(() => {
    if (cachedProducts.length > 0) loadProducts(false);
  }, [cachedProducts.length, loadProducts]);

  useEffect(() => {
    if (cachedProducts.length > 0) loadProducts(false);
  }, [categoryFilter, cachedProducts.length, loadProducts]);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    await refreshData();
    setRefreshing(false);
  }, [refreshData]);

  const handleProductSelect = (product: Product) => {
    if (onProductSelect) onProductSelect(product);
  };

  // Soft stock status colors
  const getStockStatusColor = (quantity: number, minLevel: number) => {
    if (quantity <= 0) return SoftColors.error;
    if (quantity <= minLevel) return SoftColors.warning;
    return SoftColors.success;
  };

  // Grid product card (soft minimal)
  const renderGridProduct = ({ item }: { item: Product }) => (
    <Pressable
      style={({ pressed }) => [
        styles.gridCard,
        pressed && styles.cardPressed,
      ]}
      onPress={() => handleProductSelect(item)}
    >
      {/* Image placeholder with emoji */}
      <View style={styles.gridImageWrapper}>
        <Text style={styles.gridImageEmoji}>
          {getCategoryEmoji(item.productCategory || item.category)}
        </Text>
      </View>

      {/* Content */}
      <View style={styles.gridContent}>
        <Text style={styles.gridCategory}>
          {item.productCategory || item.category}
        </Text>
        <Text style={styles.gridName} numberOfLines={2}>
          {item.name}
        </Text>

        {/* Price badge */}
        <View style={styles.priceBadge}>
          <Text style={styles.priceText}>
            €{item.price?.toFixed(2) || '0.00'}
          </Text>
        </View>

        {/* Stock indicator */}
        {showStockInfo && (
          <View style={styles.stockRow}>
            <View style={[
              styles.stockDot,
              { backgroundColor: getStockStatusColor(item.stockQuantity || 0, item.minStockLevel || 0) }
            ]} />
            <Text style={styles.stockText}>{item.stockQuantity || 0}</Text>
          </View>
        )}
      </View>
    </Pressable>
  );

  // List product card (soft minimal)
  const renderListProduct = ({ item }: { item: Product }) => (
    <Pressable
      style={({ pressed }) => [
        styles.listCard,
        pressed && styles.cardPressed,
      ]}
      onPress={() => handleProductSelect(item)}
    >
      {/* Thumbnail */}
      <View style={styles.listThumbnail}>
        <Text style={styles.listEmoji}>
          {getCategoryEmoji(item.productCategory || item.category)}
        </Text>
      </View>

      {/* Info */}
      <View style={styles.listInfo}>
        <Text style={styles.listName} numberOfLines={1}>{item.name}</Text>
        {item.description && (
          <Text style={styles.listDescription} numberOfLines={1}>
            {item.description}
          </Text>
        )}
        <View style={styles.listMeta}>
          <View style={styles.priceBadgeSm}>
            <Text style={styles.priceTextSm}>€{item.price?.toFixed(2)}</Text>
          </View>
          {showStockInfo && (
            <View style={styles.stockRow}>
              <View style={[
                styles.stockDot,
                { backgroundColor: getStockStatusColor(item.stockQuantity || 0, item.minStockLevel || 0) }
              ]} />
              <Text style={styles.stockText}>{item.stockQuantity || 0}</Text>
            </View>
          )}
        </View>
      </View>
    </Pressable>
  );

  // View mode toggle (soft pills)
  const renderViewModeToggle = () => (
    <View style={styles.viewToggleContainer}>
      <View style={styles.viewToggle}>
        <Pressable
          style={[styles.toggleBtn, viewMode === 'list' && styles.toggleBtnActive]}
          onPress={() => setViewMode('list')}
        >
          <Text style={[styles.toggleBtnText, viewMode === 'list' && styles.toggleBtnTextActive]}>
            List
          </Text>
        </Pressable>
        <Pressable
          style={[styles.toggleBtn, viewMode === 'grid' && styles.toggleBtnActive]}
          onPress={() => setViewMode('grid')}
        >
          <Text style={[styles.toggleBtnText, viewMode === 'grid' && styles.toggleBtnTextActive]}>
            Grid
          </Text>
        </Pressable>
      </View>
    </View>
  );

  const renderFooter = () => {
    if (!cacheLoading) return null;
    return (
      <View style={styles.loadingFooter}>
        <ActivityIndicator size="small" color={SoftColors.accent} />
        <Text style={styles.loadingText}>{t('common.loadingMore')}</Text>
      </View>
    );
  };

  if (cacheError) {
    return (
      <View style={styles.errorContainer}>
        <Text style={styles.errorText}>{cacheError}</Text>
        <Pressable style={styles.retryButton} onPress={() => refreshData()}>
          <Text style={styles.retryButtonText}>{t('common.retry')}</Text>
        </Pressable>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <FlatList
        data={products}
        renderItem={viewMode === 'grid' ? renderGridProduct : renderListProduct}
        keyExtractor={(item) => item.id || Math.random().toString()}
        numColumns={viewMode === 'grid' ? getGridColumns() : 1}
        key={viewMode}
        contentContainerStyle={[
          styles.listContainer,
          viewMode === 'grid' && styles.gridContainer
        ]}
        refreshControl={
          <RefreshControl
            refreshing={refreshing}
            onRefresh={onRefresh}
            colors={[SoftColors.accent]}
            tintColor={SoftColors.accent}
          />
        }
        ListHeaderComponent={
          <>
            {ListHeaderComponent}
            {renderViewModeToggle()}
          </>
        }
        ListFooterComponent={
          <>
            {renderFooter()}
            {ListFooterComponent}
          </>
        }
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
      />
    </View>
  );
};

// Helper: category emoji mapping
const getCategoryEmoji = (category?: string): string => {
  const map: Record<string, string> = {
    'Getränke': '🍹',
    'Speisen': '🍽️',
    'Desserts': '🍰',
    'Snacks': '🍿',
    'Kaffee & Tee': '☕',
    'Hauptgerichte': '🍛',
    'Alkoholische Getränke': '🍷',
    'Suppen': '🥣',
    'Vorspeisen': '🥗',
    'Salate': '🥗',
    'Süßigkeiten': '🍬',
    'Spezialitäten': '⭐',
    'Brot & Gebäck': '🥐',
  };
  return map[category || ''] || '📦';
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
  },
  listContainer: {
    padding: SoftSpacing.md,
  },
  gridContainer: {
    paddingHorizontal: SoftSpacing.sm,
  },

  // Grid Card
  gridCard: {
    flex: 1,
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.xl,
    margin: SoftSpacing.xs,
    overflow: 'hidden',
    ...SoftShadows.sm,
  },
  cardPressed: {
    opacity: 0.9,
    transform: [{ scale: 0.98 }],
  },
  gridImageWrapper: {
    aspectRatio: 1,
    backgroundColor: SoftColors.bgSecondary,
    justifyContent: 'center',
    alignItems: 'center',
  },
  gridImageEmoji: {
    fontSize: 40,
  },
  gridContent: {
    padding: SoftSpacing.md,
    gap: SoftSpacing.xs,
  },
  gridCategory: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    textTransform: 'uppercase',
  },
  gridName: {
    ...SoftTypography.body,
    fontWeight: '600',
    color: SoftColors.textPrimary,
  },

  // List Card
  listCard: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.md,
    marginBottom: SoftSpacing.sm,
    ...SoftShadows.sm,
  },
  listThumbnail: {
    width: 64,
    height: 64,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.bgSecondary,
    justifyContent: 'center',
    alignItems: 'center',
  },
  listEmoji: {
    fontSize: 28,
  },
  listInfo: {
    flex: 1,
    marginLeft: SoftSpacing.md,
    gap: SoftSpacing.xs,
  },
  listName: {
    ...SoftTypography.body,
    fontWeight: '600',
    color: SoftColors.textPrimary,
  },
  listDescription: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
  },
  listMeta: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.md,
  },

  // Price Badge
  priceBadge: {
    backgroundColor: SoftColors.accentLight,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: SoftSpacing.xs,
    borderRadius: SoftRadius.sm,
    alignSelf: 'flex-start',
  },
  priceBadgeSm: {
    backgroundColor: SoftColors.accentLight,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 2,
    borderRadius: SoftRadius.sm,
  },
  priceText: {
    ...SoftTypography.price,
    color: SoftColors.accentDark,
  },
  priceTextSm: {
    ...SoftTypography.priceSmall,
    color: SoftColors.accentDark,
  },

  // Stock
  stockRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: SoftSpacing.xs,
  },
  stockDot: {
    width: 6,
    height: 6,
    borderRadius: 3,
    marginRight: SoftSpacing.xs,
  },
  stockText: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
  },

  // View Toggle
  viewToggleContainer: {
    alignItems: 'center',
    paddingVertical: SoftSpacing.sm,
  },
  viewToggle: {
    flexDirection: 'row',
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.full,
    padding: SoftSpacing.xs,
    ...SoftShadows.sm,
  },
  toggleBtn: {
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.lg,
    borderRadius: SoftRadius.full,
  },
  toggleBtnActive: {
    backgroundColor: SoftColors.accent,
  },
  toggleBtnText: {
    ...SoftTypography.label,
    color: SoftColors.textSecondary,
  },
  toggleBtnTextActive: {
    color: SoftColors.textInverse,
  },

  // States
  loadingFooter: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    padding: SoftSpacing.lg,
  },
  loadingText: {
    marginLeft: SoftSpacing.sm,
    color: SoftColors.textMuted,
  },
  errorContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: SoftSpacing.xxl,
    backgroundColor: SoftColors.bgPrimary,
  },
  errorText: {
    ...SoftTypography.body,
    color: SoftColors.error,
    textAlign: 'center',
    marginBottom: SoftSpacing.lg,
  },
  retryButton: {
    backgroundColor: SoftColors.accent,
    paddingHorizontal: SoftSpacing.xl,
    paddingVertical: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    ...SoftShadows.sm,
  },
  retryButtonText: {
    ...SoftTypography.label,
    color: SoftColors.textInverse,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: SoftSpacing.xxl,
  },
  emptyText: {
    ...SoftTypography.body,
    color: SoftColors.textMuted,
    textAlign: 'center',
  },
}); 