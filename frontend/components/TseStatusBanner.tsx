/**
 * German TSE status strip for POS (Online / slow / offline).
 */
import React from 'react';
import { View, Text, StyleSheet } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useTseHealth } from '../hooks/useTseHealth';

export function TseStatusBanner() {
  const {
    bannerVariant,
    pendingOfflineQueueCount,
    estimatedRecoveryTimeUtc,
    lastLatencyMs,
  } = useTseHealth();

  const palette =
    bannerVariant === 'offline'
      ? { bg: '#5c1010', fg: '#fff', label: 'OFFLINE MODUS – NUR BARZAHLUNG, KEINE GUTSCHEINE' }
      : bannerVariant === 'slow'
        ? { bg: '#7a5b00', fg: '#fff', label: 'TSE langsam – bitte warten' }
        : { bg: '#1b5e20', fg: '#e8f5e9', label: 'TSE online' };

  const queueHint =
    typeof pendingOfflineQueueCount === 'number' && pendingOfflineQueueCount > 0
      ? ` · Offline-Warteschlange: ${pendingOfflineQueueCount}`
      : '';

  const latencyHint =
    lastLatencyMs != null && bannerVariant !== 'offline'
      ? ` · ${Math.round(lastLatencyMs)} ms`
      : '';

  const etaHint =
    bannerVariant === 'offline' && estimatedRecoveryTimeUtc
      ? ` · Nächste Prüfung ca. ${new Date(estimatedRecoveryTimeUtc).toLocaleTimeString('de-AT', {
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit',
        })}`
      : '';

  return (
    <View style={[styles.wrap, { backgroundColor: palette.bg }]} accessibilityRole="summary">
      <Text style={[styles.text, { color: palette.fg }]} numberOfLines={2}>
        {palette.label}
        {queueHint}
        {latencyHint}
        {etaHint}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    borderRadius: SoftRadius.sm,
    marginHorizontal: SoftSpacing.md,
    marginBottom: SoftSpacing.sm,
  },
  text: {
    ...SoftTypography.caption,
    fontWeight: '700',
    textAlign: 'center',
  },
});
