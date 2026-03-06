/**
 * POS ürün satırı: tek tıkla sepete ekleme (one-tap add). Extras tıklanabilir chip ile seçilir; modal yok.
 * Memoized: re-renders only when product.id or selected modifiers (ids+prices) change.
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

interface ProductRowProps {
  product: Product;
  pendingModifiers: ModifierChipItem[];
  onAdd: (product: Product, modifiers: ModifierChipItem[]) => void;
  onAddModifier: (product: Product, modifier: ModifierOptionItem) => void;
  getCategoryEmoji?: (category?: string) => string;
}

function modifiersKey(mods: ModifierChipItem[]): string {
  if (!mods.length) return '';
  return mods
    .slice()
    .sort((a, b) => a.id.localeCompare(b.id))
    .map((m) => `${m.id}:${m.quantity ?? 1}`)
    .join(',');
}

function ProductRowInner({
  product,
  pendingModifiers,
  onAdd,
  onAddModifier,
  getCategoryEmoji = () => '📦',
}: ProductRowProps) {
  const groups = product.modifierGroups ?? [];
  const allModifiers: ModifierOptionItem[] = useMemo(
    () =>
      groups.flatMap((g) =>
        (g.modifiers ?? []).map((m) => ({ id: m.id, name: m.name, price: Number(m.price) }))
      ),
    [product.modifierGroups]
  );
  const hasModifiers = allModifiers.length > 0;

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
              selectedModifiers={pendingModifiers.map((m) => ({ ...m, quantity: m.quantity ?? 1 }))}
              onAdd={(m) => onAddModifier(product, m)}
              hideQuantityStepper
              loading={false}
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

export const ProductRow = memo(ProductRowInner, (prev, next) => {
  if (prev.product.id !== next.product.id) return false;
  if (modifiersKey(prev.pendingModifiers) !== modifiersKey(next.pendingModifiers)) return false;
  return true;
});
