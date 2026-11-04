// Soft minimal product list with grid/list view modes.
// POS: Tek tıkla sepete ekleme; Extras tıklanabilir chip ile seçilir (onAddProduct + onToggleModifier).
import React, { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  Pressable,
  StyleSheet,
  FlatList,
  RefreshControl,
  useWindowDimensions,
  Platform,
  type DimensionValue,
} from 'react-native';

import { ProductGridCard } from './ProductGridCard';
import { ProductRow, type ModifierChipItem, type OnAddAddOn } from './ProductRow';
import {
  SoftColors,
  SoftSpacing,
  SoftRadius,
  SoftState,
  SoftTypography,
  SoftShadows,
} from '../constants/SoftTheme';
import { getContentPaddingHorizontal } from '../constants/breakpoints';
import { useProductsUnified } from '../hooks/useProductsUnified';
import { Product } from '../services/api/productService';
import { WaveLoader } from '../src/components/common/WaveLoader';
import {
  categoryColorTint,
  isCategoryColor,
  resolveCategoryIcon,
} from '../utils/categoryDisplay';

/** Breakpoint: tablet = 768+ for grid columns. */
const TABLET_MIN_WIDTH = 768;

/**
 * Pixel width of one grid cell. Percentage fallbacks collapse inside a flex row;
 * an explicit width keeps cards side by side in the catalog column.
 */
export function catalogGridItemWidth(
  paneWidth: number,
  columns: number,
  horizontalPadding: number
): number {
  if (paneWidth <= 0 || columns <= 0) return 0;
  const inner = paneWidth - horizontalPadding * 2;
  if (inner <= 0) return 0;
  return Math.floor(inner / columns);
}

function gridCellLayout(paneWidth: number, columns: number, horizontalPadding: number) {
  const measured = catalogGridItemWidth(paneWidth, columns, horizontalPadding);
  if (measured > 0) {
    return {
      width: measured,
      flexGrow: 0,
      flexShrink: 0,
      flexBasis: measured,
    };
  }
  const share = `${100 / columns}%` as DimensionValue;
  return {
    width: share,
    flexGrow: 0,
    flexShrink: 0,
    flexBasis: share,
  };
}

/** Ürün satırına tıklanınca (eski modal akışı – artık kullanılmıyor, onAddProduct tercih edilir) */
export type OnProductSelect = (product: Product) => void;

interface ProductListProps {
  /** null = "Alle", string = category id from backend */
  categoryFilterId?: string | null;
  categoryFilter?: string;
  stockStatusFilter?: 'in-stock' | 'out-of-stock' | 'low-stock';
  searchQuery?: string;
  onProductSelect?: OnProductSelect;
  /** POS: Ürün bazında bekleyen modifier seçimi; key = productId */
  pendingModifiersByProduct?: Record<string, ModifierChipItem[]>;
  /** POS: Tek tıkla sepete ekle (product only; Phase D PR-B: no selectedModifiers on request). */
  onAddProduct?: (product: Product) => void;
  /** Add-on tapped → one cart line (addItem); no modifier state. Phase D: onAddModifier removed. */
  onAddAddOn?: OnAddAddOn;
  /** Add-on grupları olan ürün tıklandığında bottom sheet aç (base + add-on'lar flat cart). */
  onOpenAddOnSheet?: (product: Product) => void;
  /** Long-press product row (e.g. toggle cashier favorite). */
  onLongPressProduct?: (product: Product) => void;
  showStockInfo?: boolean;
  showTaxInfo?: boolean;
  ListHeaderComponent?: React.ComponentType<any> | React.ReactElement | null;
  ListFooterComponent?: React.ComponentType<any> | React.ReactElement | null;
}

export const ProductList: React.FC<ProductListProps> = ({
  categoryFilterId,
  categoryFilter,
  stockStatusFilter,
  searchQuery,
  onProductSelect,
  pendingModifiersByProduct = {},
  onAddProduct,
  onAddAddOn,
  onOpenAddOnSheet,
  onLongPressProduct,
  showStockInfo = false,
  showTaxInfo = true,
  ListHeaderComponent,
  ListFooterComponent,
}) => {
  const { t } = useTranslation();
  const { width: screenWidth } = useWindowDimensions();
  const isTablet = screenWidth >= TABLET_MIN_WIDTH;
  const contentPaddingH = getContentPaddingHorizontal(screenWidth);
  const useInlineExtras = Boolean(onAddProduct);

  const {
    products: cachedProducts,
    loading: cacheLoading,
    error: cacheError,
    refreshData,
    searchProducts,
    getProductsByCategory,
  } = useProductsUnified();

  const [products, setProducts] = useState<Product[]>([]);
  const [refreshing, setRefreshing] = useState(false);
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('list');

  const getGridColumns = () => (isTablet ? 3 : 2);
  const gridColumns = getGridColumns();
  const gridCellStyle = gridCellLayout(paneWidth, gridColumns, contentPaddingH);

  const loadProducts = useCallback(async () => {
    try {
      let productsData: Product[];

      if (searchQuery) {
        productsData = searchProducts(searchQuery);
        if (categoryFilterId != null && categoryFilterId !== '') {
          productsData = productsData.filter(
            (p) => (p as Product & { categoryId?: string }).categoryId === categoryFilterId
          );
        }
      } else if (categoryFilterId != null && categoryFilterId !== '') {
        productsData = getProductsByCategory(categoryFilterId);
      } else if (categoryFilter) {
        productsData = cachedProducts.filter((p) => {
          const productCategory = p.productCategory || p.category;
          return productCategory === categoryFilter;
        });
      } else if (stockStatusFilter) {
        productsData = cachedProducts.filter((p) => {
          switch (stockStatusFilter) {
            case 'in-stock':
              return p.stockQuantity > (p.minStockLevel || 0);
            case 'out-of-stock':
              return p.stockQuantity === 0;
            case 'low-stock':
              return p.stockQuantity <= (p.minStockLevel || 0) && p.stockQuantity > 0;
            default:
              return true;
          }
        });
      } else {
        productsData = getProductsByCategory(null);
      }

      setProducts(productsData);
    } catch (err) {
      console.error('Error loading products:', err);
    }
  }, [
    categoryFilterId,
    categoryFilter,
    stockStatusFilter,
    searchQuery,
    cachedProducts,
    searchProducts,
    getProductsByCategory,
  ]);

  useEffect(() => {
    loadProducts().catch((err: unknown) => {
      console.error('Error loading products:', err);
    });
  }, [cachedProducts, loadProducts]);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await refreshData();
    } finally {
      setRefreshing(false);
    }
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

  // Grid product card: inline Extras + Add veya eski tek tıkla seçim
  const renderGridProduct = ({ item }: { item: Product }) => {
    const card =
      useInlineExtras && onAddProduct ? (
        <ProductGridCard
          product={item}
          pendingModifiers={pendingModifiersByProduct[item.id] ?? []}
          onAdd={onAddProduct}
          onAddAddOn={onAddAddOn}
          onOpenAddOnSheet={onOpenAddOnSheet}
        />
      ) : (
      <Pressable
        style={({ pressed }) => [styles.gridCard, pressed && styles.cardPressed]}
        onPress={() => {
          handleProductSelect(item);
        }}>
        <View
          style={[
            styles.gridImageWrapper,
            isCategoryColor(item.categoryColor)
              ? { backgroundColor: categoryColorTint(item.categoryColor, 0.22) }
              : null,
          ]}>
          <Text style={styles.gridImageEmoji}>{resolveCategoryIcon(item.categoryIcon)}</Text>
        </View>
        <View style={styles.gridContent}>
          <Text
            style={[
              styles.gridCategory,
              isCategoryColor(item.categoryColor) ? { color: item.categoryColor } : null,
            ]}>
            {item.productCategory || item.category}
          </Text>
          <Text style={styles.gridName} numberOfLines={2}>
            {item.name}
          </Text>
          <View style={styles.priceBadge}>
            <Text style={styles.priceText}>€{item.price?.toFixed(2) || '0.00'}</Text>
          </View>
        </View>
      </Pressable>
      );
    return <View style={[styles.gridCell, gridCellStyle]}>{card}</View>;
  };

  // List product card: inline Extras (Zutaten) + Add butonu veya eski tek tıkla seçim
  const renderListProduct = ({ item }: { item: Product }) => {
    if (useInlineExtras && onAddProduct) {
      return (
        <ProductRow
          product={item}
          pendingModifiers={pendingModifiersByProduct[item.id] ?? []}
          onAdd={onAddProduct}
          onAddAddOn={onAddAddOn}
          onOpenAddOnSheet={onOpenAddOnSheet}
          onLongPressProduct={onLongPressProduct}
        />
      );
    }
    return (
      <Pressable
        style={({ pressed }) => [styles.listCard, pressed && styles.cardPressed]}
        onPress={() => {
          handleProductSelect(item);
        }}>
        <View
          style={[
            styles.listThumbnail,
            isCategoryColor(item.categoryColor)
              ? { backgroundColor: categoryColorTint(item.categoryColor, 0.22) }
              : null,
          ]}>
          <Text style={styles.listEmoji}>{resolveCategoryIcon(item.categoryIcon)}</Text>
        </View>
        <View style={styles.listInfo}>
          <Text style={styles.listName} numberOfLines={1}>
            {item.name}
          </Text>
          {item.description && (
            <Text style={styles.listDescription} numberOfLines={1}>
              {item.description}
            </Text>
          )}
          <View style={styles.listMeta}>
            <View style={styles.priceBadgeSm}>
              <Text style={styles.priceTextSm}>€{item.price?.toFixed(2)}</Text>
            </View>
          </View>
        </View>
      </Pressable>
    );
  };

  // View mode toggle (soft pills)
  const renderViewModeToggle = () => (
    <View style={styles.viewToggleContainer}>
      <View style={styles.viewToggle} accessibilityRole="tablist">
        <Pressable
          style={({ pressed }) => [
            styles.toggleBtn,
            viewMode === 'list' && styles.toggleBtnActive,
            pressed && SoftState.pressedScale,
          ]}
          onPress={() => {
            setViewMode('list');
          }}
          accessibilityRole="tab"
          accessibilityState={{ selected: viewMode === 'list' }}
          accessibilityLabel={viewMode === 'list' ? 'Listenansicht, ausgewählt' : 'Listenansicht'}
          hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}>
          <Text style={[styles.toggleBtnText, viewMode === 'list' && styles.toggleBtnTextActive]}>
            Liste
          </Text>
        </Pressable>
        <Pressable
          style={({ pressed }) => [
            styles.toggleBtn,
            viewMode === 'grid' && styles.toggleBtnActive,
            pressed && SoftState.pressedScale,
          ]}
          onPress={() => {
            setViewMode('grid');
          }}
          accessibilityRole="tab"
          accessibilityState={{ selected: viewMode === 'grid' }}
          accessibilityLabel={viewMode === 'grid' ? 'Rasteransicht, ausgewählt' : 'Rasteransicht'}
          hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}>
          <Text style={[styles.toggleBtnText, viewMode === 'grid' && styles.toggleBtnTextActive]}>
            Raster
          </Text>
        </Pressable>
      </View>
    </View>
  );

  const renderFooter = () => {
    if (!cacheLoading) return null;
    return (
      <View style={styles.loadingFooter}>
        <WaveLoader size={18} color={SoftColors.accent} />
        <Text style={styles.loadingText}>{t('common.loadingMore')}</Text>
      </View>
    );
  };

  const listNumColumns = viewMode === 'grid' ? gridColumns : 1;

  const catalogError = cacheError ? (
    <View style={styles.errorBanner} testID="pos-catalog-error">
      <Text style={styles.errorText}>{cacheError}</Text>
      <Pressable
        style={styles.retryButton}
        onPress={() => {
          refreshData().catch((err: unknown) => {
            console.error('Error refreshing products:', err);
          });
        }}
        testID="pos-catalog-retry"
        accessibilityRole="button">
        <Text style={styles.retryButtonText}>{t('common.retry')}</Text>
      </Pressable>
    </View>
  ) : null;

  return (
    <View
      style={styles.container}
      onLayout={(event) => {
        const next = Math.round(event.nativeEvent.layout.width);
        setPaneWidth((prev) => (prev === next ? prev : next));
      }}>
      <FlatList
        style={styles.list}
        data={products}
        renderItem={viewMode === 'grid' ? renderGridProduct : renderListProduct}
        keyExtractor={(item) => item.id || Math.random().toString()}
        numColumns={listNumColumns}
        key={`products-${viewMode}-${listNumColumns}`}
        columnWrapperStyle={listNumColumns > 1 ? styles.gridRow : undefined}
        keyboardShouldPersistTaps="handled"
        removeClippedSubviews={false}
        contentContainerStyle={[
          styles.listContainer,
          viewMode === 'grid' && styles.gridContainer,
          { paddingHorizontal: contentPaddingH },
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
          <View
            style={styles.listHeaderWrap}
            pointerEvents="box-none"
            collapsable={Platform.OS === 'android' ? false : undefined}>
            {typeof ListHeaderComponent === 'function'
              ? React.createElement(ListHeaderComponent)
              : ListHeaderComponent}
            {catalogError}
            <View style={styles.productsSectionHeader}>
              <Text style={styles.stepLabel}>3</Text>
              <Text style={styles.productsSectionTitle} accessibilityRole="header">
                Produkte
              </Text>
            </View>
            {renderViewModeToggle()}
          </View>
        }
        ListFooterComponent={
          <>
            {renderFooter()}
            {ListFooterComponent}
          </>
        }
        ListEmptyComponent={
          !cacheLoading && !cacheError ? (
            <View style={styles.emptyContainer}>
              <Text style={styles.emptyText}>
                {searchQuery ? t('noSearchResults') : t('noProductsFound')}
              </Text>
            </View>
          ) : null
        }
        showsVerticalScrollIndicator={false}
      />
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
  },
  list: {
    flex: 1,
  },
  listHeaderWrap: {
    zIndex: 1,
    elevation: 1,
  },
  listContainer: {
    padding: SoftSpacing.md,
  },
  gridContainer: {
    paddingHorizontal: SoftSpacing.sm,
  },

  gridRow: {
    width: '100%',
    alignItems: 'stretch',
  },
  gridCell: {
    paddingHorizontal: SoftSpacing.xs,
    paddingBottom: SoftSpacing.xs,
  },
  // Grid Card
  gridCard: {
    width: '100%',
    alignSelf: 'stretch',
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.xl,
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
  productsSectionHeader: {
    flexDirection: 'row',
    alignItems: 'baseline',
    gap: SoftSpacing.xs,
    marginTop: SoftSpacing.md,
    marginBottom: SoftSpacing.sm,
  },
  stepLabel: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    width: 14,
  },
  productsSectionTitle: {
    ...SoftTypography.h2,
    color: SoftColors.textPrimary,
    flex: 1,
  },
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
    minHeight: 44,
    justifyContent: 'center',
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
    fontWeight: '600',
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
  errorBanner: {
    alignItems: 'center',
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
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
