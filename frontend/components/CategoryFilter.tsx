// Türkçe Açıklama: Kategori bazlı filtreleme için görsel kategori seçici. Backend'den gelen kategorileri dinamik olarak yükler ve seçilen kategoriye göre ürünleri filtreler.

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
  // Backend'deki kategori isimleri
  Getränke: {
    icon: 'wine' as const,
    color: '#3498db'
  },
  Speisen: {
    icon: 'restaurant' as const,
    color: '#e74c3c'
  },
  Desserts: {
    icon: 'ice-cream' as const,
    color: '#f39c12'
  },
  Snacks: {
    icon: 'fast-food' as const,
    color: '#27ae60'
  },
  'Kaffee & Tee': {
    icon: 'cafe' as const,
    color: '#8e44ad'
  },
  // Eski kategoriler (geriye uyumluluk için)
  Hauptgerichte: {
    icon: 'restaurant' as const,
    color: '#FF6B6B'
  },
  'Alkoholische Getränke': {
    icon: 'wine' as const,
    color: '#A8E6CF'
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

  // Kategori bulunamadığında varsayılan konfigürasyon
  const getCategoryConfig = (category: string) => {
    return CATEGORY_CONFIG[category as keyof typeof CATEGORY_CONFIG] || {
      icon: 'folder' as const,
      color: '#6C757D'
    };
  };

  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      contentContainerStyle={styles.container}
    >
      {allCategories.map((category) => {
        const config = getCategoryConfig(category);
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