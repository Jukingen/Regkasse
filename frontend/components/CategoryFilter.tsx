// Soft minimal category filter with warm brown tones
import React from 'react';
import { View, Text, Pressable, StyleSheet, ScrollView } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography, SoftShadows } from '../constants/SoftTheme';

export type ProductCategory = string;

type CategoryFilterProps = {
  selectedCategory: string;
  onCategoryChange: (category: string) => void;
  categories: string[];
};

// Soft theme category icons (using muted accent color)
const CATEGORY_ICONS: Record<string, keyof typeof Ionicons.glyphMap> = {
  all: 'grid-outline',
  Getränke: 'wine-outline',
  Speisen: 'restaurant-outline',
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

const CategoryFilter: React.FC<CategoryFilterProps> = ({
  selectedCategory,
  onCategoryChange,
  categories
}) => {
  const { t } = useTranslation();
  const allCategories = ['all', ...categories];

  const getIcon = (category: string): keyof typeof Ionicons.glyphMap => {
    return CATEGORY_ICONS[category] || 'folder-outline';
  };

  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      contentContainerStyle={styles.container}
    >
      {allCategories.map((category) => {
        const isSelected = selectedCategory === category;
        const label = category === 'all' ? t('categories.all') : category;

        return (
          <Pressable
            key={category}
            style={({ pressed }) => [
              styles.chip,
              isSelected && styles.chipSelected,
              pressed && styles.chipPressed,
            ]}
            onPress={() => onCategoryChange(category)}
          >
            <Ionicons
              name={getIcon(category)}
              size={16}
              color={isSelected ? SoftColors.textInverse : SoftColors.textSecondary}
            />
            <Text style={[styles.chipText, isSelected && styles.chipTextSelected]}>
              {label}
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