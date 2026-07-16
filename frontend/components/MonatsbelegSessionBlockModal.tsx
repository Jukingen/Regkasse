// RKSV: full-screen blocking modal when Monatsbeleg is overdue (ensure-ready monatsbeleg_required).
import React, { useCallback, useMemo } from 'react';
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';

import { POS_ENSURE_READY_ON_ENTRY } from '../constants/posFeatureFlags';
import { SoftColors, SoftRadius, SoftShadows, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import { usePosMonatsbelegCreate } from '../hooks/usePosMonatsbelegCreate';
import { isReadinessMonatsbelegGateActive } from '../utils/posRegisterGateCopy';
import { getViennaYearMonth } from '../utils/resolvePosMonatsbelegTarget';
import { WaveLoader } from '../src/components/common/WaveLoader';

export function MonatsbelegSessionBlockModal() {
  const { data, loading, error } = usePosRegisterReadiness();
  const { busy, requestCreate } = usePosMonatsbelegCreate();
  const { year, month } = useMemo(() => getViennaYearMonth(), []);
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
