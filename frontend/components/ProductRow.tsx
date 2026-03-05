/**
 * POS ürün satırı: tek tıkla sepete ekleme (one-tap add). Extras tıklanabilir chip ile seçilir; modal yok.
 */
import React from 'react';
import { View, Text, Pressable, StyleSheet } from 'react-native';
import { Product } from '../services/api/productService';
import { useProductModifierGroups } from '../hooks/useProductModifierGroups';
import { ModifierOptionChips, type ModifierOptionItem } from './ModifierOptionChips';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography, SoftShadows } from '../constants/SoftTheme';

export interface ModifierChipItem {
  id: string;
  name: string;
  price: number;
}

interface ProductRowProps {
  product: Product;
  pendingModifiers: ModifierChipItem[];
  onAdd: (product: Product, modifiers: ModifierChipItem[]) => void;
  /** Chip ile modifier seçimini toggle eder (bir sonraki sepete eklemede kullanılır) */
  onToggleModifier: (productId: string, modifier: ModifierOptionItem) => void;
  getCategoryEmoji?: (category?: string) => string;
}

export function ProductRow({
  product,
  pendingModifiers,
  onAdd,
  onToggleModifier,
  getCategoryEmoji = () => '📦',
}: ProductRowProps) {
  const { groups, loading, hasModifiers } = useProductModifierGroups(product.id);
  const allModifiers: ModifierOptionItem[] = groups.flatMap((g) =>
    (g.modifiers ?? []).map((m) => ({ id: m.id, name: m.name, price: Number(m.price) }))
  );

  const handleRowPress = () => {
    onAdd(product, pendingModifiers);
  };

  return (
    <Pressable
      style={({ pressed }) => [styles.card, pressed && styles.cardPressed]}
      onPress={handleRowPress}
    >
      <View style={styles.mainRow}>
        <View style={styles.thumbnail}>
          <Text style={styles.emoji}>{getCategoryEmoji(product.productCategory || product.category)}</Text>
        </View>

        <View style={styles.info}>
          <Text style={styles.name} numberOfLines={1}>{product.name}</Text>
          {product.description ? (
            <Text style={styles.description} numberOfLines={1}>{product.description}</Text>
          ) : null}
          <View style={styles.meta}>
            <View style={styles.priceBadge}>
              <Text style={styles.priceText}>€{product.price?.toFixed(2)}</Text>
            </View>
          </View>

          {hasModifiers && (
            <ModifierOptionChips
              modifiers={allModifiers}
              selectedModifiers={pendingModifiers}
              onToggle={(m) => onToggleModifier(product.id, m)}
              loading={loading}
            />
          )}
        </View>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.md,
    marginBottom: SoftSpacing.sm,
    ...SoftShadows.sm,
  },
  mainRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
  thumbnail: {
    width: 64,
    height: 64,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.bgSecondary,
    justifyContent: 'center',
    alignItems: 'center',
  },
  emoji: {
    fontSize: 28,
  },
  info: {
    flex: 1,
    marginLeft: SoftSpacing.md,
    gap: SoftSpacing.xs,
  },
  name: {
    ...SoftTypography.body,
    fontWeight: '600',
    color: SoftColors.textPrimary,
  },
  description: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
  },
  meta: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.md,
  },
  priceBadge: {
    backgroundColor: SoftColors.accentLight,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 2,
    borderRadius: SoftRadius.sm,
  },
  priceText: {
    ...SoftTypography.priceSmall,
    color: SoftColors.accentDark,
  },
  cardPressed: {
    opacity: 0.92,
  },
});
