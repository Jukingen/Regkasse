// RKSV: when ensure-ready returns startbeleg_required, offer in-app creation (German operator copy only).
import React, { useCallback, useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';

import { POS_ENSURE_READY_ON_ENTRY } from '../constants/posFeatureFlags';
import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import { postCreateStartbeleg } from '../services/api/rksvSpecialReceiptsService';
import { receiptPrinter } from '../services/receiptPrinter';
import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import { WaveLoader } from '../src/components/common/WaveLoader';

export function StartbelegRequiredBanner() {
  const { data, refreshAsync } = usePosRegisterReadiness();
  const [busy, setBusy] = useState(false);

  const needs =
    POS_ENSURE_READY_ON_ENTRY &&
    data?.nextAction === 'startbeleg_required' &&
    Boolean(data?.effectiveRegisterId?.trim());

  const registerId = data?.effectiveRegisterId?.trim() ?? '';

  const onCreate = useCallback(async () => {
    if (!registerId) return;
    setBusy(true);
    try {
      const created = await postCreateStartbeleg({ cashRegisterId: registerId, reason: 'POS Startbeleg' });
      await refreshAsync();
      try {
        await receiptPrinter.print(String(created.paymentId));
        Alert.alert('Startbeleg', 'Startbeleg wurde erstellt und gedruckt. Sie können nun mit der Schicht fortfahren.');
      } catch {
        Alert.alert(
          'Startbeleg',
          'Startbeleg wurde erstellt. Der automatische Druck ist fehlgeschlagen — Beleg später erneut drucken. Sie können nun mit der Schicht fortfahren.'
        );
      }
    } catch (e: unknown) {
      const err = e as { data?: { message?: string }; message?: string };
      const msg = err?.data?.message ?? err?.message ?? 'Unbekannter Fehler';
      Alert.alert('Startbeleg', String(msg));
    } finally {
      setBusy(false);
    }
  }, [registerId, refreshAsync]);

  if (!needs) {
    return null;
  }

  return (
    <View style={styles.wrap} accessibilityRole="alert">
      <Text style={styles.title}>Startbeleg muss erstellt werden.</Text>
      <Text style={styles.hint}>
        Ohne Startbeleg ist keine normale Schicht / keine Verkäufe möglich. Erstellen Sie den fiskalischen Nullbeleg, um
        fortzufahren.
      </Text>
      <Pressable
        onPress={() => void onCreate()}
        disabled={busy}
        style={({ pressed }) => [styles.btn, pressed && styles.btnPressed, busy && styles.btnDisabled]}
        accessibilityRole="button"
        accessibilityLabel="Startbeleg jetzt erstellen"
      >
        {busy ? (
          <WaveLoader size={20} color={SoftColors.textInverse} />
        ) : (
          <Text style={styles.btnText}>Jetzt erstellen</Text>
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
