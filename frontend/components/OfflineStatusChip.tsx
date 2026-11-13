/**
 * Compact offline queue chip for the POS header (capacity only when relevant).
 */
import React from 'react';
import { View, Text, StyleSheet } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useOfflineStatus } from '../hooks/useOfflineStatus';
import { OFFLINE_UI_LEVEL_COLORS } from '../utils/offlineStatusLevel';

export function OfflineStatusChip() {
  const { pendingCount, maxLimit, isOnline, isSyncing, statusLevel } = useOfflineStatus();

  if (isOnline && pendingCount <= 0 && !isSyncing) {
    return null;
  }

  const color = OFFLINE_UI_LEVEL_COLORS[statusLevel];
  const label = !isOnline
    ? `Offline ${pendingCount}/${maxLimit}`
    : isSyncing
      ? `Sync ${pendingCount}/${maxLimit}`
      : `${pendingCount}/${maxLimit}`;

  return (
    <View
      style={[styles.chip, { borderColor: color }]}
      accessibilityRole="summary"
      accessibilityLabel={`Offline-Warteschlange ${pendingCount} von ${maxLimit}`}>
      <View style={[styles.dot, { backgroundColor: color }]} />
      <Text style={styles.text} numberOfLines={1}>
        {label}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: SoftSpacing.xs,
    paddingVertical: 2,
    borderRadius: SoftRadius.sm,
    borderWidth: StyleSheet.hairlineWidth,
    backgroundColor: SoftColors.bgSecondary,
    flexShrink: 0,
  },
  dot: {
    width: 6,
    height: 6,
    borderRadius: 3,
  },
  text: {
    ...SoftTypography.caption,
    fontSize: 10,
    fontWeight: '600',
    color: SoftColors.textSecondary,
  },
});
