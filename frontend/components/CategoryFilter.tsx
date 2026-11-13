// Soft minimal category filter – backend'den gelen kategoriler; "Alle" sadece UI.
import { Ionicons } from '@expo/vector-icons';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Text,
  Pressable,
  StyleSheet,
  ScrollView,
  View,
  useWindowDimensions,
  type DimensionValue,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import {
  SoftColors,
  SoftShadows,
  SoftSpacing,
  SoftRadius,
  SoftState,
  SoftTypography,
} from '../constants/SoftTheme';
import type { CatalogCategory } from '../hooks/useProductsUnified';
import {
  categoryColorTint,
  isCategoryColor,
  resolveCategoryIcon,
} from '../utils/categoryDisplay';

type CategoryFilterProps = {
  categories: CatalogCategory[];
  selectedCategoryId: string | null;
  onCategoryChange: (categoryId: string | null) => void;
};

const CategoryFilter: React.FC<CategoryFilterProps> = ({
  categories,
  selectedCategoryId,
  onCategoryChange,
}) => {
  const { t } = useTranslation(['products']);
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();
  const [viewMode, setViewMode] = useState<'chips' | 'grid'>('chips');
  const gridColumns = width >= 768 ? 4 : 3;
  const tileWidth = `${100 / gridColumns}%` as DimensionValue;

  const renderAllChip = (compact: boolean) => {
    const isSelected = selectedCategoryId === null;
    return (
      <Pressable
        key="__all__"
        style={(state) => [
          compact ? styles.tile : styles.chip,
          isSelected && (compact ? styles.tileSelected : styles.chipSelected),
          state.pressed && styles.chipPressed,
          (state as { focused?: boolean }).focused && SoftState.focusVisible,
        ]}
        onPress={() => {
          onCategoryChange(null);
        }}
        hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
        accessibilityLabel={
          isSelected ? `${t('products:all')}, ${t('products:a11y.selected')}` : t('products:all')
        }
        accessibilityRole="button"
        accessibilityState={{ selected: isSelected }}>
        {compact ? (
          <Text style={styles.tileEmoji} accessibilityElementsHidden>
            📦
          </Text>
        ) : (
          <Ionicons
            name="grid-outline"
            size={16}
            color={isSelected ? SoftColors.textInverse : SoftColors.textPrimary}
          />
        )}
        <Text
          style={[
            compact ? styles.tileLabel : styles.chipText,
            isSelected && (compact ? styles.tileLabelSelected : styles.chipTextSelected),
          ]}
          numberOfLines={compact ? 2 : 1}
          ellipsizeMode="tail">
          {t('products:all')}
        </Text>
      </Pressable>
    );
  };

  const renderCategory = (cat: CatalogCategory, compact: boolean) => {
    const isSelected = selectedCategoryId === cat.id;
    const icon = resolveCategoryIcon(cat.icon);
    const color = isCategoryColor(cat.color) ? cat.color : undefined;
    const selectedBg = color ?? SoftColors.accent;
    const unselectedBg = color ? categoryColorTint(color, 0.16) : SoftColors.bgCard;
    const unselectedBorder = color ?? SoftColors.borderLight;

    return (
      <Pressable
        key={cat.id}
        style={(state) => [
          compact ? styles.tile : styles.chip,
          {
            backgroundColor: isSelected ? selectedBg : unselectedBg,
            borderColor: isSelected ? selectedBg : unselectedBorder,
          },
          state.pressed && styles.chipPressed,
          (state as { focused?: boolean }).focused && SoftState.focusVisible,
        ]}
        onPress={() => {
          onCategoryChange(cat.id);
        }}
        hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
        accessibilityLabel={
          isSelected ? `${cat.name}, ${t('products:a11y.selected')}` : cat.name
        }
        accessibilityRole="button"
        accessibilityState={{ selected: isSelected }}>
        <Text
          style={compact ? styles.tileEmoji : styles.chipEmoji}
          accessibilityElementsHidden>
          {icon}
        </Text>
        <Text
          style={[
            compact ? styles.tileLabel : styles.chipText,
            isSelected && (compact ? styles.tileLabelSelected : styles.chipTextSelected),
          ]}
          numberOfLines={compact ? 2 : 1}
          ellipsizeMode="tail">
          {cat.name}
        </Text>
      </Pressable>
    );
  };

  return (
    <View>
      <View style={styles.viewToggleRow}>
        <Pressable
          style={[styles.viewToggleBtn, viewMode === 'chips' && styles.viewToggleBtnActive]}
          onPress={() => {
            setViewMode('chips');
          }}
          accessibilityRole="tab"
          accessibilityState={{ selected: viewMode === 'chips' }}
          accessibilityLabel={t('products:a11y.chipView')}>
          <Ionicons
            name="ellipsis-horizontal"
            size={16}
            color={viewMode === 'chips' ? SoftColors.textInverse : SoftColors.textPrimary}
          />
          <Text
            style={[
              styles.viewToggleText,
              viewMode === 'chips' && styles.viewToggleTextActive,
            ]}>
            {t('products:viewChips')}
          </Text>
        </Pressable>
        <Pressable
          style={[styles.viewToggleBtn, viewMode === 'grid' && styles.viewToggleBtnActive]}
          onPress={() => {
            setViewMode('grid');
          }}
          accessibilityRole="tab"
          accessibilityState={{ selected: viewMode === 'grid' }}
          accessibilityLabel={t('products:a11y.gridView')}>
          <Ionicons
            name="grid-outline"
            size={16}
            color={viewMode === 'grid' ? SoftColors.textInverse : SoftColors.textPrimary}
          />
          <Text
            style={[
              styles.viewToggleText,
              viewMode === 'grid' && styles.viewToggleTextActive,
            ]}>
            {t('products:viewGrid')}
          </Text>
        </Pressable>
      </View>

      {viewMode === 'chips' ? (
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={[
            styles.container,
            { paddingRight: Math.max(SoftSpacing.md, insets.right) },
          ]}>
          {renderAllChip(false)}
          {categories.map((cat) => renderCategory(cat, false))}
        </ScrollView>
      ) : (
        <View
          style={[
            styles.grid,
            { paddingRight: Math.max(SoftSpacing.md, insets.right) },
          ]}>
          <View style={[styles.tileWrap, { width: tileWidth }]}>{renderAllChip(true)}</View>
          {categories.map((cat) => (
            <View key={cat.id} style={[styles.tileWrap, { width: tileWidth }]}>
              {renderCategory(cat, true)}
            </View>
          ))}
        </View>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  viewToggleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: SoftSpacing.md,
    paddingBottom: SoftSpacing.xs,
    gap: SoftSpacing.sm,
  },
  viewToggleBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.xs,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.xs,
    minHeight: 36,
    borderRadius: SoftRadius.full,
    backgroundColor: SoftColors.bgCard,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
  },
  viewToggleBtnActive: {
    backgroundColor: SoftColors.accent,
    borderColor: SoftColors.accent,
  },
  viewToggleText: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
  },
  viewToggleTextActive: {
    color: SoftColors.textInverse,
    fontWeight: '600',
  },
  container: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    gap: SoftSpacing.sm,
  },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: SoftSpacing.sm,
  },
  tileWrap: {
    padding: SoftSpacing.xs,
  },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    minHeight: 44,
    maxWidth: 180,
    borderRadius: SoftRadius.full,
    backgroundColor: SoftColors.bgCard,
    marginRight: SoftSpacing.sm,
    gap: SoftSpacing.xs,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
    ...SoftShadows.sm,
  },
  tile: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: SoftSpacing.md,
    minHeight: 88,
    borderRadius: SoftRadius.lg,
    backgroundColor: SoftColors.bgCard,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
    gap: SoftSpacing.xs,
    ...SoftShadows.sm,
  },
  chipSelected: {
    backgroundColor: SoftColors.accent,
    borderColor: SoftColors.accent,
  },
  tileSelected: {
    backgroundColor: SoftColors.accent,
    borderColor: SoftColors.accent,
  },
  chipPressed: SoftState.pressedScale,
  chipEmoji: {
    fontSize: 16,
  },
  chipText: {
    ...SoftTypography.label,
    color: SoftColors.textPrimary,
    flexShrink: 1,
  },
  chipTextSelected: {
    color: SoftColors.textInverse,
    fontWeight: '600',
  },
  tileEmoji: {
    fontSize: 28,
  },
  tileLabel: {
    ...SoftTypography.caption,
    color: SoftColors.textPrimary,
    textAlign: 'center',
  },
  tileLabelSelected: {
    color: SoftColors.textInverse,
    fontWeight: '600',
  },
});

export default CategoryFilter;
