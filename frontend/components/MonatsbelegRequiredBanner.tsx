// RKSV: when ensure-ready returns monatsbeleg_required, offer in-app Monatsbeleg creation (German operator copy only).
import React, { useCallback, useMemo, useState } from 'react';
import { ActivityIndicator, Alert, Pressable, StyleSheet, Text, View } from 'react-native';

import { POS_ENSURE_READY_ON_ENTRY } from '../constants/posFeatureFlags';
import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import { getViennaYearMonth, postCreateMonatsbeleg } from '../services/api/rksvSpecialReceiptsService';
import { receiptPrinter } from '../services/receiptPrinter';

export function MonatsbelegRequiredBanner() {
  const { data, refreshAsync } = usePosRegisterReadiness();
  const [busy, setBusy] = useState(false);

  const { year, month } = useMemo(() => getViennaYearMonth(), []);
  const isDecemberAnnual = month === 12;

  const needs =
    POS_ENSURE_READY_ON_ENTRY &&
    data?.nextAction === 'monatsbeleg_required' &&
    Boolean(data?.effectiveRegisterId?.trim());

  const registerId = data?.effectiveRegisterId?.trim() ?? '';

  const runCreate = useCallback(async () => {
    if (!registerId) return;
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
        /* print is best-effort; fiscal row exists */
      }
      const title = isDecemberAnnual ? 'Jahresbeleg' : 'Monatsbeleg';
      const okBody = isDecemberAnnual
        ? 'Es wurde ein Jahresbeleg erstellt.'
        : 'Es wurde ein Monatsbeleg erstellt.';
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
  }, [registerId, refreshAsync, year, month, isDecemberAnnual]);

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

  if (!needs) {
    return null;
  }

  return (
    <View style={styles.wrap} accessibilityRole="alert">
      <Text style={styles.title}>
        {isDecemberAnnual ? 'Jahresbeleg für dieses Jahr fehlt.' : 'Monatsbeleg für diesen Monat fehlt.'}
      </Text>
      <Text style={styles.hint}>
        {isDecemberAnnual
          ? 'Im Dezember entspricht der Monatsabschluss dem Jahresbeleg (RKSV). Erstellen Sie den fiskalischen Jahresbeleg, um fortzufahren.'
          : 'Für die laufende Abrechnungsperiode (Kalendermonat) ist ein fiskalischer Monatsbeleg erforderlich. Erstellen Sie den Nullbeleg, um fortzufahren.'}
      </Text>
      <Pressable
        onPress={onCreate}
        disabled={busy}
        style={({ pressed }) => [styles.btn, pressed && styles.btnPressed, busy && styles.btnDisabled]}
        accessibilityRole="button"
        accessibilityLabel={isDecemberAnnual ? 'Jahresbeleg jetzt erstellen' : 'Monatsbeleg jetzt erstellen'}
      >
        {busy ? (
          <ActivityIndicator color={SoftColors.textInverse} />
        ) : (
          <Text style={styles.btnText}>{isDecemberAnnual ? 'Jahresbeleg erstellen' : 'Jetzt erstellen'}</Text>
        )}
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    backgroundColor: SoftColors.warningBg,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: SoftColors.border,
  },
  title: {
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginBottom: 4,
  },
  hint: {
    color: SoftColors.textSecondary,
    fontSize: 13,
    marginBottom: SoftSpacing.sm,
  },
  btn: {
    alignSelf: 'flex-start',
    backgroundColor: SoftColors.accent,
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: SoftRadius.md,
  },
  btnPressed: {
    opacity: 0.85,
  },
  btnDisabled: {
    opacity: 0.6,
  },
  btnText: {
    color: SoftColors.textInverse,
    fontWeight: '600',
  },
});
