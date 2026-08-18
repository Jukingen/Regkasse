/**
 * Compact POS TSE chip: Active (green) / Degraded (amber) / Inactive (red).
 * Tap shows SCU, last check, certificate, and Fiskaly TEST/LIVE.
 */
import React, { useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useTseStatus } from '../hooks/useTseStatus';
import { formatUserDateTime } from '../utils/dateFormatter';

function indicatorDotColor(status: string): string {
  if (status === 'Inactive') return '#dc2626';
  if (status === 'Degraded') return '#ca8a04';
  return '#16a34a';
}

export function TseStatusBadge() {
  const { t } = useTranslation(['system', 'common']);
  const { status, message, loading, details, showTestBadge, error } = useTseStatus();

  const isInactive = status === 'Inactive';
  const isDegraded = status === 'Degraded';
  const dotColor = indicatorDotColor(String(status));

  const statusText = loading
    ? t('system:tse.indicator.checking')
    : isInactive
      ? t('system:tse.indicator.inactive')
      : isDegraded
        ? t('system:tse.indicator.degraded')
        : showTestBadge
          ? t('system:tse.indicator.activeTest')
          : t('system:tse.indicator.active');

  const showDetails = useCallback(() => {
    const na = t('system:tse.tooltip.na');
    const envLabel = details.environment?.trim() || (showTestBadge ? 'TEST' : na);
    const lines = [
      `${t('system:tse.tooltip.status')}: ${statusText}`,
      `${t('system:tse.tooltip.environment')}: ${envLabel}`,
      `${t('system:tse.tooltip.message')}: ${message?.trim() || error?.trim() || na}`,
      `${t('system:tse.tooltip.lastCheck')}: ${formatUserDateTime(details.lastCheck, { includeSeconds: true }) || na}`,
      `${t('system:tse.tooltip.scuId')}: ${details.scuId?.trim() || na}`,
      `${t('system:tse.tooltip.certificateUntil')}: ${formatUserDateTime(details.certificateValidUntil) || na}`,
    ];
    if (details.cached) {
      lines.push(t('system:tse.tooltip.cached'));
    }

    Alert.alert(t('system:tse.tooltip.title'), lines.join('\n'), [
      { text: t('common:ok'), style: 'default' },
    ]);
  }, [details, error, message, showTestBadge, statusText, t]);

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
    maxWidth: 196,
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
});
