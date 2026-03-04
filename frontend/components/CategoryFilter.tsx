// Soft minimal category filter – backend'den gelen kategoriler; "Alle" sadece UI.
import React from 'react';
import { Text, Pressable, StyleSheet, ScrollView } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography, SoftShadows } from '../constants/SoftTheme';
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

  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      contentContainerStyle={styles.container}
    >
      {/* "Alle" sadece UI konsepti – backend'de kategori yok */}
      <Pressable
        key="__all__"
        style={({ pressed }) => [
          styles.chip,
          selectedCategoryId === null && styles.chipSelected,
          pressed && styles.chipPressed,
        ]}
        onPress={() => onCategoryChange(null)}
      >
        <Ionicons
          name="grid-outline"
          size={16}
          color={selectedCategoryId === null ? SoftColors.textInverse : SoftColors.textSecondary}
        />
        <Text style={[styles.chipText, selectedCategoryId === null && styles.chipTextSelected]}>
          {t('categories.all')}
        </Text>
      </Pressable>
      {categories.map((cat) => {
        const isSelected = selectedCategoryId === cat.id;
        return (
          <Pressable
            key={cat.id}
            style={({ pressed }) => [
              styles.chip,
              isSelected && styles.chipSelected,
              pressed && styles.chipPressed,
            ]}
            onPress={() => onCategoryChange(cat.id)}
          >
            <Ionicons
              name={getIcon(cat.name)}
              size={16}
              color={isSelected ? SoftColors.textInverse : SoftColors.textSecondary}
            />
            <Text style={[styles.chipText, isSelected && styles.chipTextSelected]}>
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
    paddingHorizontal: SoftSpacing.lg,
    paddingVertical: SoftSpacing.md,
    gap: SoftSpacing.sm,
  },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: SoftSpacing.lg,
    paddingVertical: SoftSpacing.sm + 2,
    borderRadius: SoftRadius.full,
    backgroundColor: SoftColors.bgCard,
    marginRight: SoftSpacing.sm,
    gap: SoftSpacing.xs,
    ...SoftShadows.sm,
  },
  chipSelected: {
    backgroundColor: SoftColors.accent,
  },
  chipPressed: {
    opacity: 0.85,
    transform: [{ scale: 0.98 }],
  },
  chipText: {
    ...SoftTypography.label,
    color: SoftColors.textSecondary,
  },
  chipTextSelected: {
    color: SoftColors.textInverse,
  },
});

export default CategoryFilter; 