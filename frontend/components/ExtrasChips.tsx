/**
 * Seçili modifier'ları chip/badge olarak gösterir. Uzun listelerde "+N more" kısaltması.
 */
import React from 'react';
import { View, Text, StyleSheet, Pressable } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography } from '../constants/SoftTheme';

export interface ModifierChipItem {
  id: string;
  name: string;
  price: number;
}

interface ExtrasChipsProps {
  modifiers: ModifierChipItem[];
  /** Gösterilecek chip sayısı; aşılırsa "+N more" gösterilir */
  maxVisible?: number;
  onPress?: () => void;
}

const DEFAULT_MAX_VISIBLE = 3;

export function ExtrasChips({
  modifiers,
  maxVisible = DEFAULT_MAX_VISIBLE,
  onPress,
}: ExtrasChipsProps) {
  if (!modifiers?.length) return null;

  const visible = modifiers.slice(0, maxVisible);
  const remaining = modifiers.length - maxVisible;

  return (
    <Pressable onPress={onPress} style={styles.wrapper}>
      <View style={styles.chipRow}>
        {visible.map((m) => (
          <View key={m.id} style={styles.chip}>
            <Text style={styles.chipText} numberOfLines={1}>
              + {m.name}
            </Text>
          </View>
        ))}
        {remaining > 0 && (
          <View style={styles.chipMore}>
            <Text style={styles.chipMoreText}>+{remaining} more</Text>
          </View>
        )}
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  wrapper: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
  },
  chipRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: SoftSpacing.xs,
    alignItems: 'center',
  },
  chip: {
    backgroundColor: SoftColors.accentLight + '80',
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 2,
    borderRadius: SoftRadius.sm,
    maxWidth: 120,
  },
  chipText: {
    ...SoftTypography.caption,
    color: SoftColors.textPrimary,
    fontSize: 11,
  },
  chipMore: {
    backgroundColor: SoftColors.bgSecondary,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 2,
    borderRadius: SoftRadius.sm,
  },
  chipMoreText: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    fontSize: 11,
  },
});
