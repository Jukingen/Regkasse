/**
 * Compact TSE status chip for the POS header (always visible).
 * Online / slow / offline with latency; critical offline strip when offline.
 */
import React from 'react';
import { View, Text, StyleSheet } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useTseHealth } from '../hooks/useTseHealth';
import { formatUserTime } from '../utils/dateFormatter';

/** Operator-facing offline copy — keep in sync with contract test. */
export const TSE_OFFLINE_BANNER_LABEL = 'OFFLINE MODUS – NUR BARZAHLUNG, KEINE GUTSCHEINE';

/** Compact header chip: green/amber/red dot + TSE status + latency. */
export function TseStatusBanner() {
  const { bannerVariant, lastLatencyMs } = useTseHealth();

  const isOffline = bannerVariant === 'offline';
  const isSlow = bannerVariant === 'slow';

  const dotColor = isOffline ? '#dc2626' : isSlow ? '#ca8a04' : '#16a34a';

  const statusText = isOffline
    ? 'TSE offline'
    : isSlow
      ? lastLatencyMs != null
        ? `TSE ${Math.round(lastLatencyMs)}ms`
        : 'TSE langsam'
      : lastLatencyMs != null
        ? `TSE ${Math.round(lastLatencyMs)}ms`
        : 'TSE online';

  return (
    <View style={styles.chip} accessibilityRole="summary" accessibilityLabel={statusText}>
      <View style={[styles.dot, { backgroundColor: dotColor }]} />
      <Text style={styles.chipText} numberOfLines={1}>
        {statusText}
      </Text>
    </View>
  );
}

/** Full-width critical strip — only when TSE is offline (Barzahlung-only warning). */
export function TseOfflineRestrictionBanner() {
  const { bannerVariant, estimatedRecoveryTimeUtc } = useTseHealth();

  if (bannerVariant !== 'offline') return null;

  const etaHint = estimatedRecoveryTimeUtc
    ? ` · Nächste Prüfung ca. ${formatUserTime(estimatedRecoveryTimeUtc, { includeSeconds: true }) || '—'}`
    : '';

  return (
    <View style={styles.offlineStrip} accessibilityRole="alert">
      <Text style={styles.offlineStripText} numberOfLines={2}>
        {TSE_OFFLINE_BANNER_LABEL}
        {etaHint}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingVertical: 2,
    paddingHorizontal: SoftSpacing.xs,
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.bgSecondary,
    flexShrink: 1,
    minWidth: 0,
    maxWidth: 120,
  },
  dot: {
    width: 6,
    height: 6,
    borderRadius: 3,
  },
  chipText: {
    ...SoftTypography.caption,
    fontSize: 10,
    fontWeight: '600',
    color: SoftColors.textSecondary,
    flexShrink: 1,
  },
  offlineStrip: {
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    borderRadius: SoftRadius.sm,
    marginHorizontal: SoftSpacing.md,
    marginBottom: SoftSpacing.sm,
    backgroundColor: '#5c1010',
  },
  offlineStripText: {
    ...SoftTypography.caption,
    fontWeight: '700',
    color: SoftColors.textInverse,
    textAlign: 'center',
  },
});
