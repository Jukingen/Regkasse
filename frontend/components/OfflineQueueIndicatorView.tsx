import React from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import {
  OFFLINE_QUEUE_DOT_COLORS,
  type OfflineQueueDotColor,
} from '../utils/offlineQueueIndicator';

export type OfflineQueueIndicatorViewProps = {
  color: OfflineQueueDotColor;
  count: number;
  accessibilityLabel: string;
  onPress: () => void;
};

export function OfflineQueueIndicatorView({
  color,
  count,
  accessibilityLabel,
  onPress,
}: OfflineQueueIndicatorViewProps) {
  const dotColor = OFFLINE_QUEUE_DOT_COLORS[color];
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      style={styles.chip}
      testID="offline-queue-indicator"
    >
      <View
        testID={`offline-queue-dot-${color}`}
        style={[styles.dot, { backgroundColor: dotColor }]}
      />
      {count > 0 ? (
        <Text style={styles.count} numberOfLines={1}>
          {count}
        </Text>
      ) : null}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: SoftSpacing.xs,
    paddingVertical: 4,
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.bgSecondary,
    flexShrink: 0,
  },
  dot: {
    width: 10,
    height: 10,
    borderRadius: 5,
  },
  count: {
    ...SoftTypography.caption,
    fontSize: 11,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    minWidth: 10,
  },
});
