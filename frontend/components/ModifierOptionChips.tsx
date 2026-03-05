/**
 * POS: Tıklanabilir modifier seçenekleri (Extras). Chip’e tıklanınca seçim toggle edilir; modal yok.
 */
import React, { useState } from 'react';
import { View, Text, Pressable, StyleSheet, ActivityIndicator } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography } from '../constants/SoftTheme';
export interface ModifierOptionItem {
  id: string;
  name: string;
  price: number;
}

interface ModifierOptionChipsProps {
  /** Tüm seçenekler (gruplardan düzleştirilmiş) */
  modifiers: ModifierOptionItem[];
  /** Şu an seçili olanlar */
  selectedModifiers: ModifierOptionItem[];
  /** Chip tıklanınca: modifier seçiliyse çıkar, değilse ekle */
  onToggle: (modifier: ModifierOptionItem) => void;
  loading?: boolean;
  maxSummaryChips?: number;
}

const LABEL_EXTRAS = 'Extras';
const MAX_SUMMARY = 3;

export function ModifierOptionChips({
  modifiers,
  selectedModifiers,
  onToggle,
  loading = false,
  maxSummaryChips = MAX_SUMMARY,
}: ModifierOptionChipsProps) {
  const [expanded, setExpanded] = useState(false);
  const selectedIds = new Set(selectedModifiers.map((m) => m.id));
  const totalExtra = selectedModifiers.reduce((s, m) => s + m.price, 0);

  if (loading) {
    return (
      <View style={styles.container}>
        <Text style={styles.label}>{LABEL_EXTRAS}</Text>
        <ActivityIndicator size="small" color={SoftColors.accent} style={styles.loader} />
      </View>
    );
  }

  if (!modifiers.length) return null;

  const summaryVisible = selectedModifiers.slice(0, maxSummaryChips);
  const remaining = selectedModifiers.length - maxSummaryChips;

  return (
    <View style={styles.container}>
      <Pressable style={styles.header} onPress={() => setExpanded((e) => !e)} hitSlop={8}>
        <Text style={styles.label}>{LABEL_EXTRAS} {expanded ? '▲' : '▼'}</Text>
        {totalExtra > 0 && (
          <Text style={styles.extraTotal}>+€{totalExtra.toFixed(2)}</Text>
        )}
      </Pressable>

      {!expanded && selectedModifiers.length > 0 && (
        <View style={styles.summaryRow}>
          {summaryVisible.map((m) => (
            <View key={m.id} style={styles.summaryChip}>
              <Text style={styles.summaryChipText} numberOfLines={1}>+ {m.name}</Text>
            </View>
          ))}
          {remaining > 0 && (
            <View style={styles.moreChip}>
              <Text style={styles.moreChipText}>+{remaining} more</Text>
            </View>
          )}
        </View>
      )}

      {expanded && (
        <View style={styles.chipRow}>
          {modifiers.map((m) => {
            const selected = selectedIds.has(m.id);
            return (
              <Pressable
                key={m.id}
                style={[styles.chip, selected && styles.chipSelected]}
                onPress={() => onToggle(m)}
              >
                <Text style={[styles.chipText, selected && styles.chipTextSelected]} numberOfLines={1}>
                  + {m.name} {m.price > 0 ? `€${Number(m.price).toFixed(2)}` : ''}
                </Text>
              </Pressable>
            );
          })}
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
  extraTotal: {
    ...SoftTypography.caption,
    color: SoftColors.accentDark,
    fontWeight: '600',
  },
  loader: {
    marginTop: SoftSpacing.xs,
  },
  summaryRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: SoftSpacing.xs,
    alignItems: 'center',
    marginTop: 2,
  },
  summaryChip: {
    backgroundColor: SoftColors.accentLight + '80',
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 2,
    borderRadius: SoftRadius.sm,
    maxWidth: 100,
  },
  summaryChipText: {
    ...SoftTypography.caption,
    color: SoftColors.textPrimary,
    fontSize: 11,
  },
  moreChip: {
    backgroundColor: SoftColors.bgSecondary,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 2,
    borderRadius: SoftRadius.sm,
  },
  moreChipText: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    fontSize: 11,
  },
  chipRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: SoftSpacing.xs,
    alignItems: 'center',
  },
  chip: {
    backgroundColor: SoftColors.bgSecondary,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 4,
    borderRadius: SoftRadius.sm,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
    maxWidth: 140,
  },
  chipSelected: {
    backgroundColor: SoftColors.accentLight + '99',
    borderColor: SoftColors.accent,
  },
  chipText: {
    ...SoftTypography.caption,
    color: SoftColors.textPrimary,
    fontSize: 11,
  },
  chipTextSelected: {
    color: SoftColors.accentDark,
    fontWeight: '600',
  },
});
