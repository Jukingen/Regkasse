import React from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';

export interface Category {
  id: string;
  name: string;
  icon: string;
  color: string;
}

interface CategoryFilterProps {
  categories: Category[];
  selectedCategory: string | null;
  onSelectCategory: (categoryId: string | null) => void;
}

const CategoryFilter: React.FC<CategoryFilterProps> = ({
  categories,
  selectedCategory,
  onSelectCategory,
}) => {
  const { t } = useTranslation();

  const handleCategoryPress = (categoryId: string) => {
    if (selectedCategory === categoryId) {
      onSelectCategory(null); // Kategori seçimini kaldır
    } else {
      onSelectCategory(categoryId);
    }
  };

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Ionicons name="grid" size={20} color={Colors.light.textSecondary} />
        <Text style={styles.title}>{t('categories.title')}</Text>
      </View>
      
      <ScrollView 
        horizontal 
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.scrollContent}
      >
        {/* Tümü butonu */}
        <TouchableOpacity
          style={[
            styles.categoryButton,
            !selectedCategory && styles.categoryButtonActive
          ]}
          onPress={() => onSelectCategory(null)}
          activeOpacity={0.7}
        >
          <Ionicons 
            name="apps" 
            size={20} 
            color={!selectedCategory ? Colors.light.surface : Colors.light.textSecondary} 
          />
          <Text style={[
            styles.categoryText,
            !selectedCategory && styles.categoryTextActive
          ]}>
            {t('categories.all')}
          </Text>
        </TouchableOpacity>

        {/* Kategori butonları */}
        {categories.map((category) => (
          <TouchableOpacity
            key={category.id}
            style={[
              styles.categoryButton,
              selectedCategory === category.id && styles.categoryButtonActive,
              { borderColor: category.color }
            ]}
            onPress={() => handleCategoryPress(category.id)}
            activeOpacity={0.7}
          >
            <Ionicons 
              name={category.icon as any} 
              size={20} 
              color={selectedCategory === category.id ? Colors.light.surface : category.color} 
            />
            <Text style={[
              styles.categoryText,
              selectedCategory === category.id && styles.categoryTextActive,
              { color: selectedCategory === category.id ? Colors.light.surface : category.color }
            ]}>
              {category.name}
            </Text>
          </TouchableOpacity>
        ))}
      </ScrollView>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.lg,
    padding: Spacing.md,
    marginBottom: Spacing.md,
    elevation: 2,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: Spacing.sm,
  },
  title: {
    ...Typography.h3,
    color: Colors.light.text,
    marginLeft: Spacing.xs,
  },
  scrollContent: {
    paddingRight: Spacing.md,
  },
  categoryButton: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.md,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    marginRight: Spacing.sm,
    borderWidth: 2,
    borderColor: Colors.light.border,
    minHeight: 44, // Touch-friendly minimum height
  },
  categoryButtonActive: {
    backgroundColor: Colors.light.primary,
    borderColor: Colors.light.primary,
  },
  categoryText: {
    ...Typography.bodySmall,
    marginLeft: Spacing.xs,
    fontWeight: '500',
  },
  categoryTextActive: {
    color: Colors.light.surface,
  },
});

export default CategoryFilter; 