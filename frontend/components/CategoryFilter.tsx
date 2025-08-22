// Türkçe Açıklama: Kategori bazlı filtreleme için görsel kategori seçici. Dil dosyalarından kategori isimlerini okur ve seçilen kategoriye göre ürünleri filtreler.

import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet, ScrollView } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';

export type ProductCategory = string;

type CategoryFilterProps = {
  selectedCategory: string;
  onCategoryChange: (category: string) => void;
  categories: string[];
};

// Kategori konfigürasyonu - Backend'deki kategori isimleriyle eşleştirildi
const CATEGORY_CONFIG = {
  all: {
    icon: 'grid' as const,
    color: '#6C757D'
  },
  Hauptgerichte: {
    icon: 'restaurant' as const,
    color: '#FF6B6B'
  },
  Getränke: {
    icon: 'cafe' as const,
    color: '#4ECDC4'
  },
  Desserts: {
    icon: 'ice-cream' as const,
    color: '#FFE66D'
  },
  'Alkoholische Getränke': {
    icon: 'wine' as const,
    color: '#A8E6CF'
  },
  Snacks: {
    icon: 'fast-food' as const,
    color: '#FF9F43'
  },
  Suppen: {
    icon: 'water' as const,
    color: '#FF7675'
  },
  Vorspeisen: {
    icon: 'leaf' as const,
    color: '#74B9FF'
  },
  Salate: {
    icon: 'nutrition' as const,
    color: '#00B894'
  },
  'Kaffee & Tee': {
    icon: 'cafe' as const,
    color: '#6C5CE7'
  },
  Süßigkeiten: {
    icon: 'happy' as const,
    color: '#FDCB6E'
  },
  Spezialitäten: {
    icon: 'star' as const,
    color: '#E17055'
  },
  'Brot & Gebäck': {
    icon: 'pizza' as const,
    color: '#DDA0DD'
  }
};

const CategoryFilter: React.FC<CategoryFilterProps> = ({
  selectedCategory,
  onCategoryChange,
  categories
}) => {
  const { t } = useTranslation();

  // 'all' kategorisini başa ekle
  const allCategories = ['all', ...categories];

  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      contentContainerStyle={styles.container}
    >
      {allCategories.map((category) => {
        const config = CATEGORY_CONFIG[category as keyof typeof CATEGORY_CONFIG] || CATEGORY_CONFIG.all;
        const label = category === 'all' ? t('categories.all') : category;
        
        return (
          <TouchableOpacity
            key={category}
            style={[
              styles.categoryButton,
              {
                backgroundColor: selectedCategory === category ? config.color : '#f8f9fa',
                borderColor: config.color,
              }
            ]}
            onPress={() => onCategoryChange(category)}
            activeOpacity={0.7}
          >
            <Ionicons
              name={config.icon}
              size={20}
              color={selectedCategory === category ? '#fff' : config.color}
            />
            <Text
              style={[
                styles.categoryText,
                {
                  color: selectedCategory === category ? '#fff' : config.color,
                }
              ]}
            >
              {label}
            </Text>
          </TouchableOpacity>
        );
      })}
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: {
    paddingHorizontal: 16,
    paddingVertical: 12,
    gap: 12,
  },
  categoryButton: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 20,
    borderWidth: 2,
    gap: 6,
    minWidth: 80,
    justifyContent: 'center',
  },
  categoryText: {
    fontSize: 14,
    fontWeight: '600',
  },
});

export default CategoryFilter; 