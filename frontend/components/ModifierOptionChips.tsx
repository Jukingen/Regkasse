/**
 * POS: Inline Extras (modifier) – seçili olanlar satırda [-] qty [+], seçili olmayanlar + ile eklenir. Miktar ürün miktarından bağımsız.
 */
import React from 'react';
import { View, Text, Pressable, StyleSheet, ActivityIndicator } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography } from '../constants/SoftTheme';

export interface ModifierOptionItem {
  id: string;
  name: string;
  price: number;
  quantity?: number;
}

interface ModifierOptionChipsProps {
  modifiers: ModifierOptionItem[];
  /** Seçili modifier'lar (quantity ile) – aktif sepetteki son satıra ait */
  selectedModifiers: ModifierOptionItem[];
  onAdd: (modifier: ModifierOptionItem) => void;
  onIncrement?: (modifierId: string) => void;
  onDecrement?: (modifierId: string) => void;
  onRemove?: (modifierId: string) => void;
  loading?: boolean;
  /** true = product list: no [-] qty [+], only selectable chips; false = cart: full stepper */
  hideQuantityStepper?: boolean;
  /** Optional group label (e.g. "Saucen"); defaults to "Extras" */
  label?: string;
}

const LABEL_EXTRAS = 'Extras';

export function ModifierOptionChips({
  modifiers,
  selectedModifiers,
  onAdd,
  onIncrement,
  onDecrement,
  onRemove,
  loading = false,
  hideQuantityStepper = false,
  label = LABEL_EXTRAS,
}: ModifierOptionChipsProps) {
  const selectedById = new Map(selectedModifiers.map((m) => [m.id, m]));

  if (loading) {
    return (
      <View style={styles.container}>
        <Text style={styles.label}>{label}</Text>
        <ActivityIndicator size="small" color={SoftColors.accent} style={styles.loader} />
      </View>
    );
  }

  if (!modifiers.length) return null;

  return (
    <View style={styles.container}>
      <Text style={styles.label}>{label}</Text>
      <View style={styles.rows}>
        {modifiers.map((m) => {
          const selected = selectedById.get(m.id);
          const qty = selected?.quantity ?? 0;
          if (qty > 0 && !hideQuantityStepper) {
            return (
              <View key={m.id} style={styles.modifierRow}>
                <Text style={styles.modifierRowLabel} numberOfLines={1}>
                  {m.name} €{Number(m.price).toFixed(2)}
                </Text>
                <View style={styles.qtyGroup}>
                  <Pressable
                    style={[styles.qtyBtn, qty <= 1 && styles.qtyBtnRemove]}
                    onPress={() => (qty <= 1 ? onRemove?.(m.id) : onDecrement?.(m.id))}
                    accessibilityLabel={qty <= 1 ? `${m.name} entfernen` : `${m.name} verringern`}
                    accessibilityRole="button"
                  >
                    <Text style={styles.qtyBtnText}>−</Text>
                  </Pressable>
                  <Text style={styles.qtyValue}>{qty}</Text>
                  <Pressable
                    style={styles.qtyBtn}
                    onPress={() => onIncrement?.(m.id)}
                    accessibilityLabel={`${m.name} erhöhen`}
                    accessibilityRole="button"
                  >
                    <Text style={styles.qtyBtnText}>+</Text>
                  </Pressable>
                </View>
              </View>
            );
          }
          if (qty > 0 && hideQuantityStepper) {
            return (
              <Pressable
                key={m.id}
                style={styles.selectedChip}
                onPress={() => onAdd(m)}
                accessibilityLabel={`${m.name} erneut hinzufügen`}
                accessibilityRole="button"
              >
                <Text style={styles.selectedChipText}>{m.name} ✓</Text>
              </Pressable>
            );
          }
          return (
            <Pressable
              key={m.id}
              style={styles.addChip}
              onPress={() => onAdd(m)}
              accessibilityLabel={`${m.name} hinzufügen`}
              accessibilityRole="button"
            >
              <Text style={styles.addChipText}>+ {m.name} {m.price > 0 ? `€${Number(m.price).toFixed(2)}` : ''}</Text>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    marginTop: SoftSpacing.xs,
    paddingTop: SoftSpacing.xs,
    borderTopWidth: 1,
    borderTopColor: SoftColors.borderLight,
  },
  label: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: SoftSpacing.xs,
  },
  loader: {
    marginTop: SoftSpacing.xs,
  },
  rows: {
    gap: SoftSpacing.xs,
  },
  modifierRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: 4,
    paddingHorizontal: SoftSpacing.sm,
    backgroundColor: SoftColors.bgSecondary,
    borderRadius: SoftRadius.sm,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
  },
  modifierRowLabel: {
    ...SoftTypography.caption,
    color: SoftColors.textPrimary,
    flex: 1,
  },
  qtyGroup: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  qtyBtn: {
    width: 26,
    height: 26,
    borderRadius: 13,
    backgroundColor: SoftColors.bgCard,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
    alignItems: 'center',
    justifyContent: 'center',
  },
  qtyBtnRemove: {
    borderColor: SoftColors.error,
  },
  qtyBtnText: {
    fontSize: 16,
    fontWeight: '600',
    color: SoftColors.textPrimary,
  },
  qtyValue: {
    ...SoftTypography.caption,
    minWidth: 20,
    textAlign: 'center',
    fontWeight: '600',
    color: SoftColors.textPrimary,
  },
  addChip: {
    alignSelf: 'flex-start',
    backgroundColor: SoftColors.bgSecondary,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 4,
    borderRadius: SoftRadius.sm,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
  },
  addChipText: {
    ...SoftTypography.caption,
    color: SoftColors.textPrimary,
  },
  selectedChip: {
    alignSelf: 'flex-start',
    backgroundColor: SoftColors.accentLight,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 4,
    borderRadius: SoftRadius.sm,
    borderWidth: 1,
    borderColor: SoftColors.accent,
  },
  selectedChipText: {
    ...SoftTypography.caption,
    color: SoftColors.accentDark,
  },
});
