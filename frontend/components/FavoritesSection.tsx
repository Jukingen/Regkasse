import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';
import { Product } from '../services/api/productService';

interface FavoritesSectionProps {
  favoriteProducts: Product[];
  onAddToCart: (product: Product) => void;
}

const FavoritesSection: React.FC<FavoritesSectionProps> = ({
  favoriteProducts,
  onAddToCart,
}) => {
  const { t } = useTranslation();

  if (favoriteProducts.length === 0) {
    return null;
  }

  return (
    <View style={styles.favoritesSection}>
      <Text style={styles.sectionTitleSmall}>{t('cashRegister.quickAdd')}</Text>
      <ScrollView 
        horizontal 
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.favoritesContainer}
      >
        {favoriteProducts.map(product => (
          <TouchableOpacity
            key={product.id}
            style={styles.favoriteProductCard}
            onPress={() => onAddToCart(product)}
          >
            <View style={styles.favoriteProductContent}>
              <Text style={styles.favoriteProductName} numberOfLines={2}>
                {product.name}
              </Text>
              <Text style={styles.favoriteProductPrice}>
                €{product.price.toFixed(2)}
              </Text>
              <View style={styles.addIconContainer}>
                <Ionicons name="add" size={14} color="white" />
              </View>
            </View>
          </TouchableOpacity>
        ))}
      </ScrollView>
    </View>
  );
};

const styles = StyleSheet.create({
  favoritesSection: {
    marginBottom: Spacing.sm,
  },
  sectionTitleSmall: {
    ...Typography.caption,
    color: Colors.light.text,
    marginBottom: Spacing.xs,
    paddingHorizontal: Spacing.sm,
    fontWeight: '600',
    fontSize: 12,
  },
  favoritesContainer: {
    paddingHorizontal: Spacing.sm,
  },
  favoriteProductCard: {
    width: 120,
    backgroundColor: Colors.light.background,
    marginRight: Spacing.xs,
    borderRadius: BorderRadius.sm,
    borderWidth: 1,
    borderColor: Colors.light.border,
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 1,
    },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 2,
  },
  favoriteProductContent: {
    padding: Spacing.xs,
    alignItems: 'center',
  },
  favoriteProductName: {
    ...Typography.caption,
    color: Colors.light.text,
    fontWeight: '600',
    textAlign: 'center',
    marginBottom: Spacing.xs,
    fontSize: 10,
  },
  favoriteProductPrice: {
    ...Typography.caption,
    color: Colors.light.primary,
    fontWeight: '600',
    fontSize: 11,
    marginBottom: Spacing.xs,
  },
  addIconContainer: {
    backgroundColor: Colors.light.primary,
    width: 20,
    height: 20,
    borderRadius: BorderRadius.sm,
    justifyContent: 'center',
    alignItems: 'center',
  },
});

export default FavoritesSection; 