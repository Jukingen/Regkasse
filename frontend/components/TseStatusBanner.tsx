/**
 * Compact TSE status chip for the POS header (always visible).
 * Active / Degraded / Inactive with tooltip details.
 */
import React from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useTseHealth } from '../hooks/useTseHealth';
import { formatUserTime } from '../utils/dateFormatter';
import { TseStatusBadge } from './TseStatusBadge';

/** Operator-facing offline copy — keep in sync with contract test. */
export const TSE_OFFLINE_BANNER_LABEL = 'OFFLINE MODUS – NUR BARZAHLUNG, KEINE GUTSCHEINE';

/** Compact header chip: green/amber/red dot + TSE status. Tap shows details. */
export function TseStatusBanner() {
  return <TseStatusBadge />;
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
