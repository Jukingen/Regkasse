// Soft minimal category filter – backend'den gelen kategoriler; "Alle" sadece UI.
import React from 'react';
import { Text, Pressable, StyleSheet, ScrollView } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { SoftColors, SoftShadows, SoftSpacing, SoftRadius, SoftState, SoftTypography } from '../constants/SoftTheme';
import type { CatalogCategory } from '../hooks/useProductsUnified';

type CategoryFilterProps = {
  categories: CatalogCategory[];
  selectedCategoryId: string | null;
  onCategoryChange: (categoryId: string | null) => void;
};

const CATEGORY_ICONS: Record<string, keyof typeof Ionicons.glyphMap> = {
  Getränke: 'wine-outline',
  Speisen: 'restaurant-outline',
  Nachspeisen: 'ice-cream-outline',
  Desserts: 'ice-cream-outline',
  Snacks: 'fast-food-outline',
  'Kaffee & Tee': 'cafe-outline',
  Hauptgerichte: 'restaurant-outline',
  'Alkoholische Getränke': 'wine-outline',
  Suppen: 'water-outline',
  Vorspeisen: 'leaf-outline',
  Salate: 'nutrition-outline',
  Süßigkeiten: 'happy-outline',
  Spezialitäten: 'star-outline',
  'Brot & Gebäck': 'pizza-outline',
};

const getIcon = (name: string): keyof typeof Ionicons.glyphMap =>
  CATEGORY_ICONS[name] || 'folder-outline';

const CategoryFilter: React.FC<CategoryFilterProps> = ({
  categories,
  selectedCategoryId,
  onCategoryChange,
}) => {
  const { t } = useTranslation();
  const insets = useSafeAreaInsets();

  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      contentContainerStyle={[styles.container, { paddingRight: Math.max(SoftSpacing.md, insets.right) }]}
    >
      {/* "Alle" sadece UI konsepti – backend'de kategori yok */}
      <Pressable
        key="__all__"
        style={({ pressed, focused }) => [
          styles.chip,
          selectedCategoryId === null && styles.chipSelected,
          pressed && styles.chipPressed,
          focused && SoftState.focusVisible,
        ]}
        onPress={() => onCategoryChange(null)}
        hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
        accessibilityLabel={selectedCategoryId === null ? t('categories.all') + ', ausgewählt' : t('categories.all')}
        accessibilityRole="button"
        accessibilityState={{ selected: selectedCategoryId === null }}
      >
        <Ionicons
          name="grid-outline"
          size={16}
          color={selectedCategoryId === null ? SoftColors.textInverse : SoftColors.textPrimary}
        />
        <Text style={[styles.chipText, selectedCategoryId === null && styles.chipTextSelected]} numberOfLines={1} ellipsizeMode="tail">
          {t('categories.all')}
        </Text>
      </Pressable>
      {categories.map((cat) => {
        const isSelected = selectedCategoryId === cat.id;
        return (
          <Pressable
            key={cat.id}
            style={({ pressed, focused }) => [
              styles.chip,
              isSelected && styles.chipSelected,
              pressed && styles.chipPressed,
              focused && SoftState.focusVisible,
            ]}
            onPress={() => onCategoryChange(cat.id)}
            hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
            accessibilityLabel={isSelected ? `${cat.name}, ausgewählt` : cat.name}
            accessibilityRole="button"
            accessibilityState={{ selected: isSelected }}
          >
            <Ionicons
              name={getIcon(cat.name)}
              size={16}
              color={isSelected ? SoftColors.textInverse : SoftColors.textPrimary}
            />
            <Text style={[styles.chipText, isSelected && styles.chipTextSelected]} numberOfLines={1} ellipsizeMode="tail">
              {cat.name}
            </Text>
          </Pressable>
        );
      })}
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    gap: SoftSpacing.sm,
  },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    minHeight: 44,
    maxWidth: 160,
    borderRadius: SoftRadius.full,
    backgroundColor: SoftColors.bgCard,
    marginRight: SoftSpacing.sm,
    gap: SoftSpacing.xs,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
    ...SoftShadows.sm,
  },
  chipSelected: {
    backgroundColor: SoftColors.accent,
    borderColor: SoftColors.accent,
  },
  chipPressed: SoftState.pressedScale,
  chipText: {
    ...SoftTypography.label,
    color: SoftColors.textPrimary,
    flexShrink: 1,
  },
  chipTextSelected: {
    color: SoftColors.textInverse,
    fontWeight: '600',
  },
});

export default CategoryFilter; 