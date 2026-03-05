/**
 * POS grid ürün kartı: tek tıkla sepete ekleme. Extras tıklanabilir chip; modal yok.
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

interface ProductGridCardProps {
  product: Product;
  pendingModifiers: ModifierChipItem[];
  onAdd: (product: Product, modifiers: ModifierChipItem[]) => void;
  onToggleModifier: (productId: string, modifier: ModifierOptionItem) => void;
  getCategoryEmoji?: (category?: string) => string;
}

export function ProductGridCard({
  product,
  pendingModifiers,
  onAdd,
  onToggleModifier,
  getCategoryEmoji = () => '📦',
}: ProductGridCardProps) {
  const { groups, loading, hasModifiers } = useProductModifierGroups(product.id);
  const allModifiers: ModifierOptionItem[] = groups.flatMap((g) =>
    (g.modifiers ?? []).map((m) => ({ id: m.id, name: m.name, price: Number(m.price) }))
  );

  return (
    <Pressable
      style={({ pressed }) => [styles.card, pressed && styles.cardPressed]}
      onPress={() => onAdd(product, pendingModifiers)}
    >
      <View style={styles.imageWrapper}>
        <Text style={styles.emoji}>{getCategoryEmoji(product.productCategory || product.category)}</Text>
      </View>
      <View style={styles.content}>
        <Text style={styles.category}>{product.productCategory || product.category}</Text>
        <Text style={styles.name} numberOfLines={2}>{product.name}</Text>
        <View style={styles.priceBadge}>
          <Text style={styles.priceText}>€{product.price?.toFixed(2) || '0.00'}</Text>
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
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    flex: 1,
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.xl,
    margin: SoftSpacing.xs,
    overflow: 'hidden',
    ...SoftShadows.sm,
  },
  imageWrapper: {
    aspectRatio: 1,
    backgroundColor: SoftColors.bgSecondary,
    justifyContent: 'center',
    alignItems: 'center',
  },
  emoji: { fontSize: 40 },
  content: { padding: SoftSpacing.md, gap: SoftSpacing.xs },
  category: { ...SoftTypography.caption, color: SoftColors.textMuted, textTransform: 'uppercase' as const },
  name: { ...SoftTypography.body, fontWeight: '600', color: SoftColors.textPrimary },
  priceBadge: {
    backgroundColor: SoftColors.accentLight,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 2,
    borderRadius: SoftRadius.sm,
    alignSelf: 'flex-start',
  },
  priceText: { ...SoftTypography.price, color: SoftColors.accentDark },
  cardPressed: { opacity: 0.92 },
});
