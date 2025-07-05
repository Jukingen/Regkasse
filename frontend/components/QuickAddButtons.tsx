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
import { Product } from '../services/api/productService';
import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';

interface QuickAddButtonsProps {
  favoriteProducts: Product[];
  onAddToCart: (product: Product, quantity: number) => void;
}

const QuickAddButtons: React.FC<QuickAddButtonsProps> = ({
  favoriteProducts,
  onAddToCart,
}) => {
  const { t } = useTranslation();

  if (favoriteProducts.length === 0) {
    return null;
  }

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Ionicons name="star" size={20} color={Colors.light.secondary} />
        <Text style={styles.title}>{t('quickAdd.favorites')}</Text>
      </View>
      
      <ScrollView 
        horizontal 
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.scrollContent}
      >
        {favoriteProducts.map((product) => (
          <TouchableOpacity
            key={product.id}
            style={styles.quickAddButton}
            onPress={() => onAddToCart(product, 1)}
            activeOpacity={0.7}
          >
            <View style={styles.productInfo}>
              <Text style={styles.productName} numberOfLines={2}>
                {product.name}
              </Text>
              <Text style={styles.productPrice}>
                {product.price.toFixed(2)}€
              </Text>
            </View>
            <View style={styles.addIcon}>
              <Ionicons name="add-circle" size={24} color={Colors.light.primary} />
            </View>
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
  quickAddButton: {
    backgroundColor: Colors.light.cartBackground,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    marginRight: Spacing.sm,
    minWidth: 120,
    minHeight: 80,
    justifyContent: 'space-between',
    borderWidth: 1,
    borderColor: Colors.light.borderLight,
  },
  productInfo: {
    flex: 1,
  },
  productName: {
    ...Typography.bodySmall,
    color: Colors.light.text,
    marginBottom: Spacing.xs,
    fontWeight: '500',
  },
  productPrice: {
    ...Typography.body,
    color: Colors.light.primary,
    fontWeight: '600',
  },
  addIcon: {
    alignSelf: 'flex-end',
    marginTop: Spacing.xs,
  },
});

export default QuickAddButtons; 