import React, { useCallback, useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import { useMonatsbelegStatus } from '../hooks/useMonatsbelegStatus';
import { notifyPosMonatsbelegManager } from '../services/api/cashRegisterService';
import { isValidPosCashRegisterId } from '../utils/posCashRegister';

/**
 * Non-blocking POS warning when Monatsbeleg is missing but sales are still allowed
 * (GracePeriod days 1–14 or WarningOnly).
 */
export function MonatsbelegSalesWarningBanner() {
  const { t } = useTranslation(['checkout']);
  const posReadiness = usePosRegisterReadiness();
  const { requiresAttention, warningLevel, data, isOverdue } = useMonatsbelegStatus();
  const [dismissed, setDismissed] = useState(false);
  const [notifyBusy, setNotifyBusy] = useState(false);

  const registerId = posReadiness.data?.effectiveRegisterId?.trim() ?? '';
  const salesBlocked =
    posReadiness.data?.monatsbelegSalesBlocked === true || data?.salesBlocked === true;
  const canContinue =
    posReadiness.data?.monatsbelegCanContinueWithWarning === true ||
    data?.canContinueWithWarning === true;
  const level = posReadiness.data?.monatsbelegWarningLevel ?? warningLevel;
  const isRed = level === 'red' || isOverdue;

  const visible =
    !dismissed &&
    !salesBlocked &&
    (level === 'yellow' || level === 'red') &&
    (canContinue || requiresAttention) &&
    isValidPosCashRegisterId(registerId);

  const onNotify = useCallback(async () => {
    if (!registerId || notifyBusy) return;
    setNotifyBusy(true);
    try {
      const result = await notifyPosMonatsbelegManager(registerId);
      if (result.code === 'ALREADY_NOTIFIED') {
        Alert.alert('Mandanten-Admin', t('checkout:posFlow.monatsbelegBanner.notifyAlready'));
      } else if (result.ok) {
        Alert.alert('Mandanten-Admin', t('checkout:posFlow.monatsbelegBanner.notifySuccess'));
      } else {
        Alert.alert('Mandanten-Admin', t('checkout:posFlow.monatsbelegBanner.notifyFailed'));
      }
    } catch {
      Alert.alert('Mandanten-Admin', t('checkout:posFlow.monatsbelegBanner.notifyFailed'));
    } finally {
      setNotifyBusy(false);
    }
  }, [notifyBusy, registerId, t]);

  if (!visible) return null;

  return (
    <View
      style={[styles.root, isRed ? styles.rootRed : styles.rootYellow]}
      accessibilityRole="alert">
      <Text style={styles.title}>{t('checkout:posFlow.monatsbelegBanner.missingTitle')}</Text>
      <Text style={styles.body}>{t('checkout:posFlow.monatsbelegBanner.allowedWithWarning')}</Text>
      <View style={styles.actions}>
        <Pressable
          onPress={() => {
            void onNotify();
          }}
          disabled={notifyBusy}
          style={({ pressed }) => [styles.btn, pressed && !notifyBusy && styles.pressed]}
          accessibilityRole="button"
          accessibilityLabel={t('checkout:posFlow.monatsbelegBanner.contactManager')}>
          <Text style={styles.btnText}>{t('checkout:posFlow.monatsbelegBanner.contactManager')}</Text>
        </Pressable>
        <Pressable
          onPress={() => setDismissed(true)}
          style={({ pressed }) => [styles.btnGhost, pressed && styles.pressed]}
          accessibilityRole="button"
          accessibilityLabel={t('checkout:posFlow.monatsbelegBanner.continueWithWarning')}>
          <Text style={styles.btnGhostText}>
            {t('checkout:posFlow.monatsbelegBanner.continueWithWarning')}
          </Text>
        </Pressable>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  root: {
    marginHorizontal: SoftSpacing.md,
    marginBottom: SoftSpacing.sm,
    padding: SoftSpacing.md,
    borderRadius: SoftRadius.md,
  },
  rootRed: {
    backgroundColor: SoftColors.errorBg,
  },
  rootYellow: {
    backgroundColor: SoftColors.warningBg,
  },
  title: {
    ...SoftTypography.label,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.xs,
  },
  body: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.sm,
  },
  actions: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: SoftSpacing.sm,
  },
  btn: {
    backgroundColor: SoftColors.accent,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.sm,
  },
  btnGhost: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.sm,
    borderWidth: 1,
    borderColor: SoftColors.accent,
  },
  btnText: {
    ...SoftTypography.caption,
    fontWeight: '600',
    color: SoftColors.textInverse,
  },
  btnGhostText: {
    ...SoftTypography.caption,
    fontWeight: '600',
    color: SoftColors.accent,
  },
  pressed: {
    opacity: 0.85,
  },
});
