import React, { useCallback, useMemo, useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useAuth } from '../contexts/AuthContext';
import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import { useMonatsbelegStatus } from '../hooks/useMonatsbelegStatus';
import { usePosMonatsbelegCreate } from '../hooks/usePosMonatsbelegCreate';
import { notifyPosMonatsbelegManager } from '../services/api/cashRegisterService';
import { isValidPosCashRegisterId } from '../utils/posCashRegister';
import { resolveMonatsbelegBannerState } from '../utils/posMonatsbelegBannerState';
import { hasPermission } from '../utils/posPermissions';
import { resolvePosMonatsbelegTarget } from '../utils/resolvePosMonatsbelegTarget';
import { WaveLoader } from '../src/components/common/WaveLoader';

const RKSV_MONATSBELEG_CREATE = 'rksv.monatsbeleg.create';

/**
 * Dashboard banner for missing previous-month Monatsbeleg (all blocking modes).
 * Strict / day-15+ Grace: not dismissible. Grace 1–14 and WarningOnly: session dismiss.
 */
export function MonatsbelegSalesWarningBanner() {
  const { t } = useTranslation(['checkout']);
  const { user } = useAuth();
  const posReadiness = usePosRegisterReadiness();
  const { data: status } = useMonatsbelegStatus();
  const { busy, requestCreate } = usePosMonatsbelegCreate();
  const [dismissed, setDismissed] = useState(false);
  const [notifyBusy, setNotifyBusy] = useState(false);

  const registerId = posReadiness.data?.effectiveRegisterId?.trim() ?? '';
  const canCreate = hasPermission(user, RKSV_MONATSBELEG_CREATE);
  const state = useMemo(
    () =>
      resolveMonatsbelegBannerState({
        readiness: posReadiness.data,
        status,
        canCreate,
        dismissed,
      }),
    [canCreate, dismissed, posReadiness.data, status]
  );

  const { year, month } = useMemo(() => resolvePosMonatsbelegTarget(status), [status]);

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

  const onCreate = useCallback(() => {
    if (!canCreate || !registerId) return;
    requestCreate({ cashRegisterId: registerId, year, month, force: true });
  }, [canCreate, month, registerId, requestCreate, year]);

  if (!state.visible || !isValidPosCashRegisterId(registerId)) return null;

  return (
    <View
      style={[styles.root, state.tone === 'red' ? styles.rootRed : styles.rootYellow]}
      accessibilityRole="alert"
      testID="monatsbeleg-dashboard-banner">
      <Text style={styles.title}>{t('checkout:monatsbeleg.banner.title')}</Text>
      <Text style={styles.body}>{t(`checkout:monatsbeleg.banner.${state.bodyKey}`)}</Text>
      <View style={styles.actions}>
        {state.showCreate ? (
          <Pressable
            onPress={onCreate}
            disabled={busy || notifyBusy}
            style={({ pressed }) => [styles.btn, pressed && !busy && styles.pressed]}
            accessibilityRole="button"
            accessibilityLabel={t('checkout:monatsbeleg.banner.createNow')}>
            {busy ? (
              <WaveLoader size={16} color={SoftColors.textInverse} />
            ) : (
              <Text style={styles.btnText}>{t('checkout:monatsbeleg.banner.createNow')}</Text>
            )}
          </Pressable>
        ) : null}
        <Pressable
          onPress={() => {
            void onNotify();
          }}
          disabled={notifyBusy || busy}
          style={({ pressed }) => [styles.btn, pressed && !notifyBusy && styles.pressed]}
          accessibilityRole="button"
          accessibilityLabel={t('checkout:monatsbeleg.banner.contactManager')}>
          <Text style={styles.btnText}>{t('checkout:monatsbeleg.banner.contactManager')}</Text>
        </Pressable>
        {state.canDismiss ? (
          <Pressable
            onPress={() => setDismissed(true)}
            style={({ pressed }) => [styles.btnGhost, pressed && styles.pressed]}
            accessibilityRole="button"
            accessibilityLabel={t('checkout:monatsbeleg.banner.dismiss')}>
            <Text style={styles.btnGhostText}>{t('checkout:monatsbeleg.banner.dismiss')}</Text>
          </Pressable>
        ) : null}
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
