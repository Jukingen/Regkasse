/**
 * Ürün satırında Extras (Zutaten) özeti: başlık, seçili chip'ler, toplam ek fiyat, Edit aksiyonu.
 * Sadece ürünün modifier'ı varsa veya seçili extras varsa gösterilir.
 */
import React from 'react';
import { View, Text, Pressable, StyleSheet, ActivityIndicator } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography } from '../constants/SoftTheme';
import { ExtrasChips, type ModifierChipItem } from './ExtrasChips';

interface ProductExtrasInlineProps {
  /** Seçili modifier'lar (bu satır için) */
  selectedModifiers: ModifierChipItem[];
  /** Toplam ek fiyat (seçili modifier'ların toplamı) */
  totalExtraPrice?: number;
  /** Edit tıklandığında (modifier seçim UI'ı açılacak) */
  onEditPress: () => void;
  /** Modifier grupları yükleniyor mu */
  loading?: boolean;
  /** Chip tıklanınca da Edit açılsın mı */
  chipsPressOpensEdit?: boolean;
}

const LABEL_EXTRAS = 'Extras (Zutaten)';
const LABEL_EDIT = 'Edit';

export function ProductExtrasInline({
  selectedModifiers,
  totalExtraPrice = 0,
  onEditPress,
  loading = false,
  chipsPressOpensEdit = true,
}: ProductExtrasInlineProps) {
  const hasSelection = selectedModifiers.length > 0;

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.label}>{LABEL_EXTRAS}</Text>
        {loading ? (
          <ActivityIndicator size="small" color={SoftColors.accent} style={styles.loader} />
        ) : (
          <Pressable onPress={onEditPress} hitSlop={8} style={styles.editBtn}>
            <Text style={styles.editText}>{LABEL_EDIT}</Text>
          </Pressable>
        )}
      </View>
      {hasSelection && (
        <View style={styles.summary}>
          <ExtrasChips
            modifiers={selectedModifiers}
            maxVisible={3}
            onPress={chipsPressOpensEdit ? onEditPress : undefined}
          />
          {totalExtraPrice > 0 && (
            <Text style={styles.extraPrice}>(+€{totalExtraPrice.toFixed(2)})</Text>
          )}
        </View>
      )}
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
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: SoftSpacing.xs,
  },
  label: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  loader: {
    marginLeft: SoftSpacing.sm,
  },
  editBtn: {
    paddingVertical: 2,
    paddingHorizontal: SoftSpacing.sm,
  },
  editText: {
    ...SoftTypography.caption,
    color: SoftColors.accentDark,
    fontWeight: '600',
  },
  summary: {
    flexDirection: 'row',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: SoftSpacing.sm,
  },
  extraPrice: {
    ...SoftTypography.caption,
    color: SoftColors.accentDark,
    fontWeight: '600',
  },
});
