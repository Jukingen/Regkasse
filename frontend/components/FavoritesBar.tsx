import React, { useCallback } from 'react';
import {
  View,
  Text,
  Pressable,
  ScrollView,
  Alert,
  StyleSheet,
} from 'react-native';
import { Swipeable } from 'react-native-gesture-handler';

import { SoftColors, SoftRadius, SoftShadows, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useCart } from '../contexts/CartContext';
import type { FavoriteItem } from '../hooks/useFavorites';
import { formatPrice } from '../utils/formatPrice';

export type FavoritesBarProps = {
  favorites: FavoriteItem[];
  removeFavorite: (favoriteId: string) => Promise<void>;
  /** Optional toast/feedback after a favorite line was added to the cart. */
  onProductAdded?: (productName: string) => void;
};

export function FavoritesBar({ favorites, removeFavorite, onProductAdded }: FavoritesBarProps) {
  const { addItem } = useCart();

  const addToCart = useCallback(
    async (fav: FavoriteItem) => {
      await addItem(fav.productId, 1, {
        productName: fav.productName,
        unitPrice: fav.productPrice,
      });
      onProductAdded?.(fav.productName);
    },
    [addItem, onProductAdded]
  );

  const renderRightActions = (id: string) => (
    <Pressable
      style={styles.deleteButton}
      onPress={() => void removeFavorite(id)}
      accessibilityRole="button"
      accessibilityLabel="Favorit entfernen"
    >
      <Text style={styles.deleteText}>🗑️</Text>
    </Pressable>
  );

  if (favorites.length === 0) {
    return (
      <View style={styles.emptyFavorites}>
        <Text style={styles.emptyText}>
          ⭐ Favorit hinzufügen: Produkt lange gedrückt halten
        </Text>
      </View>
    );
  }

  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      style={styles.favoritesBar}
      contentContainerStyle={styles.favoritesBarContent}
    >
      {favorites.map((fav) => (
        <Swipeable key={fav.id} renderRightActions={() => renderRightActions(fav.id)}>
          <Pressable
            style={styles.favoriteItem}
            onPress={() => void addToCart(fav)}
            onLongPress={() =>
              Alert.alert('Aus Favoriten entfernen?', fav.productName, [
                { text: 'Abbrechen', style: 'cancel' },
                { text: 'Entfernen', onPress: () => void removeFavorite(fav.id) },
              ])
            }
            accessibilityRole="button"
            accessibilityLabel={`${fav.productName}, ${formatPrice(fav.productPrice)}`}
          >
            <Text style={styles.favoriteIcon}>⭐</Text>
            <Text style={styles.favoriteName} numberOfLines={1}>
              {fav.productName}
            </Text>
            <Text style={styles.favoritePrice}>{formatPrice(fav.productPrice)}</Text>
          </Pressable>
        </Swipeable>
      ))}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  favoritesBar: {
    maxHeight: 88,
    marginVertical: SoftSpacing.xs,
  },
  favoritesBarContent: {
    paddingHorizontal: SoftSpacing.sm,
  },
  favoriteItem: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.sm,
    marginHorizontal: 4,
    alignItems: 'center',
    width: 76,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
    ...SoftShadows.sm,
  },
  favoriteIcon: {
    fontSize: 22,
  },
  favoriteName: {
    ...SoftTypography.caption,
    fontSize: 11,
    color: SoftColors.textPrimary,
    textAlign: 'center',
    marginTop: 2,
    fontWeight: '600',
  },
  favoritePrice: {
    fontSize: 11,
    color: SoftColors.textSecondary,
    fontWeight: '700',
    marginTop: 2,
  },
  deleteButton: {
    backgroundColor: SoftColors.error,
    justifyContent: 'center',
    alignItems: 'center',
    width: 60,
    borderRadius: SoftRadius.md,
    marginHorizontal: 4,
  },
  deleteText: {
    fontSize: 20,
  },
  emptyFavorites: {
    padding: SoftSpacing.md,
    alignItems: 'center',
    backgroundColor: SoftColors.bgSecondary,
    marginHorizontal: SoftSpacing.sm,
    marginVertical: SoftSpacing.xs,
    borderRadius: SoftRadius.md,
  },
  emptyText: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    textAlign: 'center',
  },
});
