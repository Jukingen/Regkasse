// RKSV: full-screen blocking modal when Monatsbeleg is overdue (ensure-ready monatsbeleg_required).
import React, { useCallback, useMemo, useState } from 'react';
import { Alert, Modal, Pressable, StyleSheet, Text, View } from 'react-native';

import {
  SoftColors,
  SoftRadius,
  SoftShadows,
  SoftSpacing,
  SoftTypography,
} from '../constants/SoftTheme';
import { POS_ENSURE_READY_ON_ENTRY } from '../constants/posFeatureFlags';
import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import { useMonatsbelegStatus } from '../hooks/useMonatsbelegStatus';
import { usePosMonatsbelegCreate } from '../hooks/usePosMonatsbelegCreate';
import { notifyPosMonatsbelegManager } from '../services/api/cashRegisterService';
import { WaveLoader } from '../src/components/common/WaveLoader';
import { isReadinessMonatsbelegGateActive } from '../utils/posRegisterGateCopy';
import { resolvePosMonatsbelegTarget } from '../utils/resolvePosMonatsbelegTarget';

export function MonatsbelegSessionBlockModal() {
  const { data, loading, error, refreshAsync } = usePosRegisterReadiness();
  const { data: monatsbelegStatus } = useMonatsbelegStatus();
  const { busy, requestCreate } = usePosMonatsbelegCreate();
  const [notifyBusy, setNotifyBusy] = useState(false);
  const { year, month } = useMemo(
    () => resolvePosMonatsbelegTarget(monatsbelegStatus),
    [monatsbelegStatus]
  );
  const isDecemberAnnual = month === 12;

  const visible =
    POS_ENSURE_READY_ON_ENTRY &&
    !loading &&
    !error &&
    isReadinessMonatsbelegGateActive(data, { ensureReadyEnabled: true }) &&
    Boolean(data?.effectiveRegisterId?.trim());

  const registerId = data?.effectiveRegisterId?.trim() ?? '';

  const onCreate = useCallback(() => {
    if (!registerId) return;
    requestCreate({ cashRegisterId: registerId, year, month });
  }, [registerId, requestCreate, year, month]);

  const onNotifyManager = useCallback(async () => {
    if (!registerId || notifyBusy) return;
    setNotifyBusy(true);
    try {
      const result = await notifyPosMonatsbelegManager(registerId);
      await refreshAsync();
      if (result.code === 'ALREADY_NOTIFIED') {
        Alert.alert('Mandanten-Admin', 'Der Mandanten-Admin wurde heute bereits benachrichtigt.');
      } else if (result.ok) {
        Alert.alert('Mandanten-Admin', 'Der Mandanten-Admin wurde benachrichtigt.');
      } else {
        Alert.alert('Mandanten-Admin', 'Benachrichtigung fehlgeschlagen. Bitte erneut versuchen.');
      }
    } catch {
      Alert.alert('Mandanten-Admin', 'Benachrichtigung fehlgeschlagen. Bitte erneut versuchen.');
    } finally {
      setNotifyBusy(false);
    }
  }, [notifyBusy, refreshAsync, registerId]);

  return (
    <Modal
      visible={visible}
      animationType="fade"
      presentationStyle="fullScreen"
      onRequestClose={() => {}}>
      <View style={styles.root}>
        <Text style={styles.title}>Monatsbeleg fehlt</Text>
        <Text style={styles.body}>
          {isDecemberAnnual
            ? 'Im Dezember entspricht der Monatsabschluss dem Jahresbeleg (RKSV). Ohne Jahresbeleg sind keine Verkäufe möglich.'
            : 'Für den abgeschlossenen Vormonat fehlt der fiskalische Monatsbeleg. Bitte Mandanten-Admin kontaktieren.'}
        </Text>
        <Pressable
          onPress={() => {
            void onNotifyManager();
          }}
          disabled={notifyBusy || busy}
          style={({ pressed }) => [
            styles.btn,
            pressed && !notifyBusy && styles.btnPressed,
            (notifyBusy || busy) && styles.btnDisabled,
          ]}
          accessibilityRole="button"
          accessibilityLabel="Manager kontaktieren">
          {notifyBusy ? (
            <WaveLoader size={20} color={SoftColors.textInverse} />
          ) : (
            <Text style={styles.btnText}>Manager kontaktieren</Text>
          )}
        </Pressable>
        <Pressable
          onPress={onCreate}
          disabled={busy || notifyBusy}
          style={({ pressed }) => [
            styles.btnSecondary,
            pressed && !busy && styles.btnPressed,
            busy && styles.btnDisabled,
          ]}
          accessibilityRole="button"
          accessibilityLabel={
            isDecemberAnnual ? 'Jahresbeleg jetzt erstellen' : 'Monatsbeleg jetzt erstellen'
          }>
          {busy ? (
            <WaveLoader size={20} color={SoftColors.accent} />
          ) : (
            <Text style={styles.btnSecondaryText}>
              {isDecemberAnnual ? 'Jahresbeleg erstellen' : 'Monatsbeleg erstellen'}
            </Text>
          )}
        </Pressable>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    justifyContent: 'center',
    padding: SoftSpacing.lg,
    backgroundColor: SoftColors.warningBg,
  },
  title: {
    ...SoftTypography.h2,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    textAlign: 'center',
    marginBottom: SoftSpacing.md,
  },
  body: {
    ...SoftTypography.body,
    color: SoftColors.textSecondary,
    textAlign: 'center',
    marginBottom: SoftSpacing.xl,
  },
  btn: {
    alignSelf: 'center',
    backgroundColor: SoftColors.accent,
    paddingHorizontal: SoftSpacing.lg,
    paddingVertical: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    minWidth: 220,
    alignItems: 'center',
    marginBottom: SoftSpacing.md,
    ...SoftShadows.sm,
  },
  btnSecondary: {
    alignSelf: 'center',
    backgroundColor: SoftColors.bgCard,
    paddingHorizontal: SoftSpacing.lg,
    paddingVertical: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    minWidth: 220,
    alignItems: 'center',
  },
  btnPressed: {
    opacity: 0.9,
  },
  btnDisabled: {
    opacity: 0.6,
  },
  btnText: {
    ...SoftTypography.label,
    fontWeight: '600',
    color: SoftColors.textInverse,
  },
  btnSecondaryText: {
    ...SoftTypography.label,
    fontWeight: '600',
    color: SoftColors.accent,
  },
});
