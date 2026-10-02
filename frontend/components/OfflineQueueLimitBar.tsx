import React from 'react';
import { StyleSheet, Text, View } from 'react-native';

import {
  OFFLINE_LIMIT_BAR_COLORS,
  offlineLimitPercent,
  resolveOfflineLimitBarTone,
  type OfflineLimitBarTone,
} from '../utils/offlineQueueLimit';

export type OfflineQueueLimitBarProps = {
  current: number;
  limit: number;
  label: string;
};

const TONE_STYLE: Record<OfflineLimitBarTone, 'limitOk' | 'limitWarning' | 'limitExceeded'> = {
  green: 'limitOk',
  yellow: 'limitWarning',
  red: 'limitExceeded',
};

export function OfflineQueueLimitBar({ current, limit, label }: OfflineQueueLimitBarProps) {
  const percent = offlineLimitPercent(current, limit);
  const tone = resolveOfflineLimitBarTone(percent);
  const fill = Math.min(100, percent);
  const namedStyle = TONE_STYLE[tone];

  return (
    <View
      style={styles.wrap}
      testID="offline-queue-limit-bar"
      accessibilityRole="progressbar"
      accessibilityLabel={label}
      accessibilityValue={{ min: 0, max: limit, now: current }}
    >
      <Text style={styles.label}>{label}</Text>
      <View style={styles.track} testID="offline-queue-limit-track">
        <View
          testID={`offline-queue-limit-fill-${tone}`}
          style={[
            styles.fill,
            styles[namedStyle],
            { width: `${fill}%`, backgroundColor: OFFLINE_LIMIT_BAR_COLORS[tone] },
          ]}
        />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    marginBottom: 10,
  },
  label: {
    fontSize: 12,
    color: '#1e3a8a',
    fontWeight: '600',
    marginBottom: 6,
  },
  track: {
    height: 8,
    borderRadius: 4,
    backgroundColor: '#e5e7eb',
    overflow: 'hidden',
  },
  fill: {
    height: 8,
    borderRadius: 4,
  },
  limitOk: {
    backgroundColor: '#16a34a',
  },
  limitWarning: {
    backgroundColor: '#eab308',
  },
  limitExceeded: {
    backgroundColor: '#dc2626',
  },
});
