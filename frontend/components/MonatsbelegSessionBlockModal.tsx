// RKSV: full-screen blocking modal when Monatsbeleg is overdue (ensure-ready monatsbeleg_required).
import React, { useCallback, useMemo, useState } from 'react';
import { Alert, Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';

import { POS_ENSURE_READY_ON_ENTRY } from '../constants/posFeatureFlags';
import { SoftColors, SoftRadius, SoftShadows, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import { useLicenseStatus } from '../hooks/useLicenseStatus';
import { getViennaYearMonth, postCreateMonatsbeleg } from '../services/api/rksvSpecialReceiptsService';
import { receiptPrinter } from '../services/receiptPrinter';
import { isReadinessMonatsbelegGateActive } from '../utils/posRegisterGateCopy';
import { ensureLicenseAllowsCriticalAction } from '../utils/licenseCriticalActionGuard';
import { WaveLoader } from '../src/components/common/WaveLoader';

export function MonatsbelegSessionBlockModal() {
  const { data, loading, error, refreshAsync } = usePosRegisterReadiness();
  const [busy, setBusy] = useState(false);
  const { t } = useTranslation('license');
  const { status: licenseSnapshot } = useLicenseStatus();
  const { year, month } = useMemo(() => getViennaYearMonth(), []);
  const isDecemberAnnual = month === 12;

  const visible =
    POS_ENSURE_READY_ON_ENTRY &&
    !loading &&
    !error &&
    isReadinessMonatsbelegGateActive(data, { ensureReadyEnabled: true }) &&
    Boolean(data?.effectiveRegisterId?.trim());

  const registerId = data?.effectiveRegisterId?.trim() ?? '';

  const runCreate = useCallback(async () => {
    if (!registerId) return;
    const licenseOk = await ensureLicenseAllowsCriticalAction(licenseSnapshot, t, 'specialReceipt');
    if (!licenseOk) return;
    setBusy(true);
    try {
      const created = await postCreateMonatsbeleg({
        cashRegisterId: registerId,
        year,
        month,
        reason: isDecemberAnnual ? 'POS Jahresbeleg (Dezember)' : 'POS Monatsbeleg',
      });
      await refreshAsync();
      let printed = false;
      try {
        await receiptPrinter.print(String(created.paymentId));
        printed = true;
      } catch {
        /* best-effort */
      }
      const title = isDecemberAnnual ? 'Jahresbeleg' : 'Monatsbeleg';
      const okBody = isDecemberAnnual ? 'Es wurde ein Jahresbeleg erstellt.' : 'Es wurde ein Monatsbeleg erstellt.';
      const failPrint = isDecemberAnnual
        ? 'Es wurde ein Jahresbeleg erstellt. Der automatische Druck ist fehlgeschlagen — Beleg später erneut drucken.'
        : 'Es wurde ein Monatsbeleg erstellt. Der automatische Druck ist fehlgeschlagen — Beleg später erneut drucken.';
      Alert.alert(title, printed ? okBody : failPrint);
    } catch (e: unknown) {
      const err = e as { data?: { message?: string }; message?: string };
      const msg = err?.data?.message ?? err?.message ?? 'Unbekannter Fehler';
      Alert.alert(isDecemberAnnual ? 'Jahresbeleg' : 'Monatsbeleg', String(msg));
    } finally {
      setBusy(false);
    }
  }, [registerId, refreshAsync, year, month, isDecemberAnnual, licenseSnapshot, t]);

  const onCreate = useCallback(() => {
    if (!registerId) return;
    if (isDecemberAnnual) {
      Alert.alert('Jahresbeleg erstellen', 'Dieser Vorgang kann nicht rückgängig gemacht werden.', [
        { text: 'Abbrechen', style: 'cancel' },
        { text: 'Erstellen', onPress: () => void runCreate() },
      ]);
      return;
    }
    void runCreate();
  }, [registerId, isDecemberAnnual, runCreate]);

  return (
    <Modal visible={visible} animationType="fade" presentationStyle="fullScreen" onRequestClose={() => {}}>
      <View style={styles.root}>
        <Text style={styles.title}>Monatsbeleg erforderlich</Text>
        <Text style={styles.body}>
          {isDecemberAnnual
            ? 'Im Dezember entspricht der Monatsabschluss dem Jahresbeleg (RKSV). Ohne Jahresbeleg sind keine Verkäufe möglich.'
            : 'Für den aktuellen Kalendermonat fehlt der fiskalische Monatsbeleg (RKSV). Ohne Monatsbeleg sind keine Verkäufe möglich.'}
        </Text>
        <Pressable
          onPress={onCreate}
          disabled={busy}
          style={({ pressed }) => [styles.btn, pressed && !busy && styles.btnPressed, busy && styles.btnDisabled]}
          accessibilityRole="button"
          accessibilityLabel={isDecemberAnnual ? 'Jahresbeleg jetzt erstellen' : 'Monatsbeleg jetzt erstellen'}
        >
          {busy ? (
            <WaveLoader size={20} color={SoftColors.textInverse} />
          ) : (
            <Text style={styles.btnText}>{isDecemberAnnual ? 'Jahresbeleg erstellen' : 'Monatsbeleg erstellen'}</Text>
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
    ...SoftShadows.sm,
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
});
