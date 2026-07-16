/**
 * Compact Monatsbeleg warning badge for the POS header.
 * Tap opens create confirmation (same flow as former main-screen banners).
 */
import React, { useCallback } from 'react';
import { Pressable, Text, StyleSheet, Alert } from 'react-native';
import { useTranslation } from 'react-i18next';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import { useMonatsbelegStatus } from '../hooks/useMonatsbelegStatus';
import { usePosMonatsbelegCreate } from '../hooks/usePosMonatsbelegCreate';
import { isValidPosCashRegisterId } from '../utils/posCashRegister';
import { resolvePosMonatsbelegTarget } from '../utils/resolvePosMonatsbelegTarget';

export function MonatsbelegHeaderBadge() {
  const { t } = useTranslation(['checkout']);
  const { isOverdue, requiresAttention, warningLevel, data, refresh } = useMonatsbelegStatus();
  const posReadiness = usePosRegisterReadiness();
  const { busy, requestCreate } = usePosMonatsbelegCreate();

  const handlePress = useCallback(() => {
    if (busy) return;
    const registerId = posReadiness.data?.effectiveRegisterId?.trim();
    if (!registerId || !isValidPosCashRegisterId(registerId)) {
      Alert.alert(
        'Keine Kasse ausgewählt',
        'Bitte wählen Sie zuerst eine Registrierkasse aus, bevor Sie den Monatsbeleg erstellen.',
      );
      return;
    }
    const { year, month } = resolvePosMonatsbelegTarget(data);
    const title =
      warningLevel === 'red' || isOverdue
        ? t('checkout:posFlow.monatsbelegBanner.red')
        : t('checkout:posFlow.monatsbelegBanner.yellow', {
            days: data?.daysUntilDeadline ?? 0,
          });
    Alert.alert(title, t('checkout:posFlow.monatsbelegCurrentMonthOverdue.description'), [
      { text: 'Abbrechen', style: 'cancel' },
      {
        text: t('checkout:posFlow.monatsbelegBanner.createNow'),
        onPress: () => {
          requestCreate({
            cashRegisterId: registerId,
            year,
            month,
            onAfterSuccess: () => {
              void refresh();
            },
          });
        },
      },
    ]);
  }, [
    busy,
    data,
    isOverdue,
    posReadiness.data?.effectiveRegisterId,
    refresh,
    requestCreate,
    t,
    warningLevel,
  ]);

  if (!requiresAttention) {
    return null;
  }

  const isRed = isOverdue || warningLevel === 'red';

  return (
    <Pressable
      onPress={handlePress}
      disabled={busy}
      style={({ pressed }) => [
        styles.badge,
        isRed ? styles.badgeRed : styles.badgeYellow,
        pressed && !busy && styles.pressed,
        busy && styles.disabled,
      ]}
      accessibilityRole="button"
      accessibilityLabel={
        isRed
          ? t('checkout:posFlow.monatsbelegBanner.red')
          : t('checkout:posFlow.monatsbelegBanner.yellow', {
              days: data?.daysUntilDeadline ?? 0,
            })
      }
      accessibilityHint={t('checkout:posFlow.monatsbelegBanner.createNow')}
      hitSlop={6}
    >
      <Text style={[styles.icon, isRed && styles.iconRed]}>!</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  badge: {
    minWidth: 22,
    height: 22,
    borderRadius: SoftRadius.sm,
    paddingHorizontal: SoftSpacing.xs,
    alignItems: 'center',
    justifyContent: 'center',
    flexShrink: 0,
  },
  badgeRed: {
    backgroundColor: SoftColors.errorBg,
  },
  badgeYellow: {
    backgroundColor: SoftColors.warningBg,
  },
  pressed: {
    opacity: 0.75,
  },
  disabled: {
    opacity: 0.5,
  },
  icon: {
    ...SoftTypography.caption,
    fontSize: 12,
    fontWeight: '800',
    color: '#92400e',
  },
  iconRed: {
    color: SoftColors.error,
  },
});
