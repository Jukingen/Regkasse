import React, { useCallback } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';

import { usePosRegisterSelection } from '../hooks/usePosRegisterSelection';
import { isValidPosCashRegisterId } from '../utils/posCashRegister';
import { WaveLoader } from '../src/components/common/WaveLoader';

function formatRegisterLabel(registerNumber: string, location?: string): string {
  const num = registerNumber.trim() || '—';
  const loc = location?.trim();
  return loc ? `${num} – ${loc}` : num;
}

/** POS settings: pick and persist cash register assignment (German UI). */
export function CashRegisterSelector() {
  const { t } = useTranslation(['settings']);
  const {
    effectiveRegisterId,
    registers,
    registersLoading,
    registersListFailure,
    isLoading,
    savingRegisterId,
    settingsLoadFailed,
    selectRegister,
    reloadSettings,
    reloadRegisters,
    refreshReadiness,
  } = usePosRegisterSelection();

  const handleSelect = useCallback(
    async (registerId: string) => {
      const ok = await selectRegister(registerId);
      if (ok) {
        Alert.alert(
          t('settings:registerAssignment.savedTitle'),
          t('settings:registerAssignment.savedMessage')
        );
        return;
      }
      Alert.alert(
        t('settings:registerAssignment.saveErrorTitle'),
        t('settings:registerAssignment.saveErrorMessage')
      );
    },
    [selectRegister, t]
  );

  if (isLoading) {
    return (
      <View style={styles.container}>
        <Text style={styles.sectionTitle}>{t('settings:cashRegister.sectionTitle')}</Text>
        <WaveLoader size={28} style={{ marginVertical: 12 }} />
      </View>
    );
  }

  const selectedRow = registers.find((r) => r.id === effectiveRegisterId);
  const selectedLabel = selectedRow
    ? formatRegisterLabel(selectedRow.registerNumber, selectedRow.location)
    : isValidPosCashRegisterId(effectiveRegisterId)
      ? effectiveRegisterId!.slice(0, 8)
      : null;

  return (
    <View style={styles.container}>
      <Text style={styles.sectionTitle}>{t('settings:cashRegister.sectionTitle')}</Text>
      <Text style={styles.intro}>{t('settings:registerAssignment.intro')}</Text>

      {selectedLabel ? (
        <Text style={styles.assigned}>
          {t('settings:registerAssignment.assignedPrefix')}{' '}
          <Text style={styles.assignedStrong}>{selectedLabel}</Text>
        </Text>
      ) : null}

      {settingsLoadFailed ? (
        <Pressable onPress={() => void reloadSettings()} style={styles.linkButton}>
          <Text style={styles.linkText}>{t('settings:registerAssignment.retry')}</Text>
        </Pressable>
      ) : null}

      {registersLoading ? <WaveLoader size={28} style={{ marginVertical: 8 }} /> : null}

      {!registersLoading && registers.length > 0 ? (
        <View style={styles.optionList}>
          {registers.map((register) => {
            const selected = register.id === effectiveRegisterId;
            const disabled = Boolean(savingRegisterId);
            return (
              <Pressable
                key={register.id}
                disabled={disabled}
                onPress={() => void handleSelect(register.id)}
                style={[
                  styles.optionRow,
                  selected && styles.optionRowSelected,
                  disabled && styles.optionRowDisabled,
                ]}
                accessibilityRole="button"
                accessibilityState={{ selected }}
              >
                <Text style={[styles.optionText, selected && styles.optionTextSelected]}>
                  {formatRegisterLabel(register.registerNumber, register.location)}
                </Text>
              </Pressable>
            );
          })}
        </View>
      ) : null}

      {!registersLoading && registers.length === 0 && !isValidPosCashRegisterId(effectiveRegisterId) ? (
        <Text style={styles.empty}>{t('settings:cashRegister.noRegistersAvailable')}</Text>
      ) : null}

      {registersListFailure ? (
        <Pressable onPress={reloadRegisters} style={styles.linkButton}>
          <Text style={styles.linkText}>{t('settings:registerAssignment.reloadList')}</Text>
        </Pressable>
      ) : null}

      {!isValidPosCashRegisterId(effectiveRegisterId) ? (
        <Pressable onPress={refreshReadiness} style={styles.linkButton}>
          <Text style={styles.linkText}>{t('settings:registerAssignment.retryReadiness')}</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    marginTop: 4,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
    marginBottom: 8,
  },
  intro: {
    fontSize: 14,
    color: '#666',
    lineHeight: 20,
    marginBottom: 10,
  },
  assigned: {
    fontSize: 14,
    color: '#1976d2',
    marginBottom: 10,
  },
  assignedStrong: {
    fontWeight: '700',
  },
  optionList: {
    gap: 8,
    marginTop: 4,
  },
  optionRow: {
    borderWidth: 1,
    borderColor: '#d0d7de',
    borderRadius: 8,
    paddingVertical: 12,
    paddingHorizontal: 14,
    backgroundColor: '#fff',
  },
  optionRowSelected: {
    borderColor: '#007AFF',
    backgroundColor: '#e3f2fd',
  },
  optionRowDisabled: {
    opacity: 0.55,
  },
  optionText: {
    fontSize: 15,
    color: '#333',
    fontWeight: '500',
  },
  optionTextSelected: {
    color: '#007AFF',
    fontWeight: '700',
  },
  empty: {
    fontSize: 14,
    color: '#888',
    marginTop: 8,
    fontStyle: 'italic',
  },
  linkButton: {
    marginTop: 10,
    paddingVertical: 4,
  },
  linkText: {
    color: '#007AFF',
    fontSize: 14,
    fontWeight: '600',
  },
});
