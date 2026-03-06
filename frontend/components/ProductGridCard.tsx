/**
 * POS grid ürün kartı: tek tıkla sepete ekleme. Extras tıklanabilir chip; modal yok.
 * Memoized: re-renders only when product.id or selected modifiers change.
 */
import React, { memo, useMemo } from 'react';
import { View, Text, Pressable, StyleSheet } from 'react-native';
import { Product } from '../services/api/productService';
import { ModifierOptionChips, type ModifierOptionItem } from './ModifierOptionChips';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography, SoftShadows } from '../constants/SoftTheme';

export interface ModifierChipItem {
  id: string;
  name: string;
  price: number;
  quantity?: number;
}

function modifiersKey(mods: ModifierChipItem[]): string {
  if (!mods.length) return '';
  return mods
    .slice()
    .sort((a, b) => a.id.localeCompare(b.id))
    .map((m) => `${m.id}:${m.quantity ?? 1}`)
    .join(',');
}

interface ProductGridCardProps {
  product: Product;
  pendingModifiers: ModifierChipItem[];
  onAdd: (product: Product, modifiers: ModifierChipItem[]) => void;
  onAddModifier: (product: Product, modifier: ModifierOptionItem) => void;
  getCategoryEmoji?: (category?: string) => string;
}

function ProductGridCardInner({
  product,
  pendingModifiers,
  onAdd,
  onAddModifier,
  getCategoryEmoji = () => '📦',
}: ProductGridCardProps) {
  const groups = product.modifierGroups ?? [];
  const allModifiers: ModifierOptionItem[] = useMemo(
    () =>
      groups.flatMap((g) =>
        (g.modifiers ?? []).map((m) => ({ id: m.id, name: m.name, price: Number(m.price) }))
      ),
    [product.modifierGroups]
  );
  const hasModifiers = allModifiers.length > 0;

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
            selectedModifiers={pendingModifiers.map((m) => ({ ...m, quantity: m.quantity ?? 1 }))}
            onAdd={(m) => onAddModifier(product, m)}
            hideQuantityStepper
            loading={false}
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

export const ProductGridCard = memo(ProductGridCardInner, (prev, next) => {
  if (prev.product.id !== next.product.id) return false;
  if (modifiersKey(prev.pendingModifiers) !== modifiersKey(next.pendingModifiers)) return false;
  return true;
});
