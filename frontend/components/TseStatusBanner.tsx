/**
 * Compact TSE status chip for the POS header (always visible).
 * Active / Degraded / Inactive with tooltip details.
 */
import React, { useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useTseHealth } from '../hooks/useTseHealth';
import { formatUserDateTime, formatUserTime } from '../utils/dateFormatter';

/** Operator-facing offline copy — keep in sync with contract test. */
export const TSE_OFFLINE_BANNER_LABEL = 'OFFLINE MODUS – NUR BARZAHLUNG, KEINE GUTSCHEINE';

function indicatorDotColor(indicator: string, bannerVariant: string): string {
  if (indicator === 'Inactive' || bannerVariant === 'offline') return '#dc2626';
  if (indicator === 'Degraded' || bannerVariant === 'slow') return '#ca8a04';
  return '#16a34a';
}

/** Compact header chip: green/amber/red dot + TSE status. Tap shows details. */
export function TseStatusBanner() {
  const { t } = useTranslation(['system', 'common']);
  const {
    indicatorStatus,
    bannerVariant,
    message,
    lastCheck,
    scuId,
    certificateValidUntil,
    cached,
    lastErrorMessageSafe,
    loading,
  } = useTseHealth();

  const isInactive = indicatorStatus === 'Inactive' || bannerVariant === 'offline';
  const isDegraded = indicatorStatus === 'Degraded' || bannerVariant === 'slow';
  const dotColor = indicatorDotColor(String(indicatorStatus), bannerVariant);

  const statusText = loading
    ? t('system:tse.indicator.checking')
    : isInactive
      ? t('system:tse.indicator.inactive')
      : isDegraded
        ? t('system:tse.indicator.degraded')
        : t('system:tse.indicator.active');

  const showDetails = useCallback(() => {
    const na = t('system:tse.tooltip.na');
    const lines = [
      `${t('system:tse.tooltip.status')}: ${statusText}`,
      `${t('system:tse.tooltip.message')}: ${message?.trim() || lastErrorMessageSafe?.trim() || na}`,
      `${t('system:tse.tooltip.lastCheck')}: ${formatUserDateTime(lastCheck, { includeSeconds: true }) || na}`,
      `${t('system:tse.tooltip.scuId')}: ${scuId?.trim() || na}`,
      `${t('system:tse.tooltip.certificateUntil')}: ${formatUserDateTime(certificateValidUntil) || na}`,
    ];
    if (cached) {
      lines.push(t('system:tse.tooltip.cached'));
    }

    Alert.alert(t('system:tse.tooltip.title'), lines.join('\n'), [
      { text: t('common:ok'), style: 'default' },
    ]);
  }, [
    cached,
    certificateValidUntil,
    lastCheck,
    lastErrorMessageSafe,
    message,
    scuId,
    statusText,
    t,
  ]);

  return (
    <Pressable
      onPress={showDetails}
      accessibilityRole="button"
      accessibilityLabel={statusText}
      accessibilityHint={t('system:tse.tooltip.hint')}
      style={styles.chip}>
      <View style={[styles.dot, { backgroundColor: dotColor }]} />
      <Text style={styles.chipText} numberOfLines={1}>
        {statusText}
      </Text>
    </Pressable>
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
    maxWidth: 148,
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
