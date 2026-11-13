import React, { useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Pressable, StyleSheet, Text } from 'react-native';

import { SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useLicenseStatus } from '../hooks/useLicenseStatus';
import { useMandantLicenseWarning } from '../hooks/useMandantLicenseWarning';
import { formatUserDate } from '../utils/dateFormatter';
import { areLicenseChecksBypassedInDevelopment } from '../utils/licenseCriticalActionGuard';
import { openLicenseExtension } from '../utils/openAdmin';
import {
  mapPosLicenseChipState,
  posLicenseChipColor,
} from '../utils/posLicenseStatusChip';

/**
 * Compact always-visible POS license lifecycle chip (Active / Grace / Locked).
 * Tap opens a German status alert with renew via FA deep link.
 */
export function LicenseStatus() {
  const { t } = useTranslation('license');
  const { status: deploymentStatus, loading } = useLicenseStatus();
  const { state: mandant, shouldShowGrace } = useMandantLicenseWarning();

  const chip = useMemo(
    () =>
      mapPosLicenseChipState({
        mandant,
        shouldShowGrace,
        deployment: deploymentStatus,
      }),
    [mandant, shouldShowGrace, deploymentStatus]
  );

  const message = useMemo(() => {
    if (chip.state === 'Active') {
      return t('statusChip.messageActive');
    }
    if (chip.state === 'Grace') {
      return t('statusChip.messageGrace', { days: chip.displayDays });
    }
    return t('statusChip.messageLocked');
  }, [chip.displayDays, chip.state, t]);

  const onRenew = useCallback(() => {
    const machineHash = deploymentStatus?.machineHash ?? '';
    void openLicenseExtension(machineHash);
  }, [deploymentStatus?.machineHash]);

  const onPress = useCallback(() => {
    const expiresLabel = chip.expiresAt ? formatUserDate(chip.expiresAt) : '—';
    Alert.alert(
      t('statusChip.alertTitle'),
      t('statusChip.alertBody', {
        state: t(`statusChip.state.${chip.state}`),
        expiresAt: expiresLabel,
        message,
      }),
      [
        {
          text: t('statusChip.renewNow'),
          onPress: onRenew,
        },
        { text: t('statusChip.ok'), style: 'cancel' },
      ]
    );
  }, [chip.expiresAt, chip.state, message, onRenew, t]);

  if (areLicenseChecksBypassedInDevelopment()) return null;
  if (loading && !deploymentStatus && !mandant) return null;

  const backgroundColor = posLicenseChipColor(chip.state);

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={message}
      accessibilityHint={t('statusChip.accessibilityHint')}
      onPress={onPress}
      style={({ pressed }) => [
        styles.chip,
        { backgroundColor },
        pressed && styles.pressed,
      ]}
    >
      <Text style={styles.label} numberOfLines={1}>
        {message}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  chip: {
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: SoftSpacing.xs,
    borderRadius: SoftRadius.sm,
    maxWidth: 180,
  },
  pressed: {
    opacity: 0.85,
  },
  label: {
    color: '#FFFFFF',
    fontSize: SoftTypography.caption.fontSize,
    fontWeight: '600',
  },
});
