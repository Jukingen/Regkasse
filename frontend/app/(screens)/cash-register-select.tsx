import { Redirect, router } from 'expo-router';
import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useAuth } from '../../contexts/AuthContext';
import { SoftColors, SoftSpacing } from '../../constants/SoftTheme';
import {
  fetchPosSelectableRegisters,
  setDefaultPosCashRegister,
  type CashRegisterSelectableRow,
  type PosSelectableEmptyReason,
} from '../../services/api/cashRegisterService';
import { autoOpenShiftApi } from '../../services/api/shiftService';
import { WaveLoader } from '../../src/components/common/WaveLoader';
import {
  needsPosCashRegisterSelection,
  readValidPosCashRegisterId,
} from '../../utils/posCashRegister';
import { isOpenedOnSelect } from '../../utils/posSelectableRegisterFilter';
import {
  classifyRegisterListError,
  type RegisterListFailureKind,
} from '../../utils/registerListError';
import {
  parseShiftAutoOpenError,
  SHIFT_AUTO_OPEN_CODES,
  shiftAutoOpenAlertI18nKeys,
} from '../../utils/shiftAutoOpenError';

function formatRegisterLabel(registerNumber: string): string {
  return registerNumber.trim() || '—';
}

/**
 * Login gate: pick a cash register, persist assignment, then POST /api/pos/shift/auto-open.
 */
export default function CashRegisterSelectScreen() {
  const { t } = useTranslation(['settings', 'auth', 'common', 'shift']);
  const { isAuthenticated, isAuthReady, user, logout, setCurrentCashRegisterId } = useAuth();

  const [registers, setRegisters] = useState<CashRegisterSelectableRow[]>([]);
  const [emptyReason, setEmptyReason] = useState<PosSelectableEmptyReason>(null);
  const [listFailure, setListFailure] = useState<RegisterListFailureKind | null>(null);
  const [loading, setLoading] = useState(true);
  const [savingId, setSavingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [retryToken, setRetryToken] = useState(0);

  const loadRegisters = useCallback(async () => {
    setLoading(true);
    setError(null);
    setListFailure(null);
    setEmptyReason(null);
    try {
      const { registers: rows, emptyReason: reason } = await fetchPosSelectableRegisters();
      setRegisters(rows);
      setEmptyReason(reason);
    } catch (e) {
      setRegisters([]);
      setListFailure(classifyRegisterListError(e));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!isAuthReady || !isAuthenticated) return;
    void loadRegisters();
  }, [isAuthReady, isAuthenticated, loadRegisters, retryToken]);

  const handleSelect = useCallback(
    async (registerId: string) => {
      const trimmed = readValidPosCashRegisterId(registerId);
      if (!trimmed || savingId) return;

      setSavingId(trimmed);
      setError(null);
      try {
        const assigned = await setDefaultPosCashRegister(trimmed);
        await autoOpenShiftApi(assigned);
        await setCurrentCashRegisterId(assigned);
        router.replace('/(tabs)/cash-register');
      } catch (e) {
        const parsed = parseShiftAutoOpenError(e);
        if (parsed.code === SHIFT_AUTO_OPEN_CODES.SHIFT_ALREADY_OPEN) {
          await setCurrentCashRegisterId(trimmed);
          router.replace('/(tabs)/cash-register');
          return;
        }
        const keys = shiftAutoOpenAlertI18nKeys(parsed.code);
        setError(t(keys.messageKey));
      } finally {
        setSavingId(null);
      }
    },
    [savingId, setCurrentCashRegisterId, t]
  );

  if (!isAuthReady) {
    return (
      <View style={[styles.container, styles.centered]}>
        <WaveLoader size={32} color={SoftColors.accentDark} />
      </View>
    );
  }

  if (!isAuthenticated || !user) {
    return <Redirect href="/(auth)/login" />;
  }

  if (user.mustChangePasswordOnNextLogin) {
    return <Redirect href="/(auth)/change-password" />;
  }

  if (!needsPosCashRegisterSelection(user.currentCashRegisterId)) {
    return <Redirect href="/(tabs)/cash-register" />;
  }

  const emptyMessage =
    emptyReason === 'no_registers'
      ? t('settings:registerSelect.noActiveRegisters')
      : emptyReason === 'none_open'
        ? t('settings:registerSelect.emptyNoneOpen')
        : emptyReason === 'none_assigned'
          ? t('settings:registerSelect.emptyNoneAssigned')
          : emptyReason === 'none_selectable_for_user'
            ? t('settings:registerSelect.emptyNoneSelectable')
            : t('settings:cashRegister.noRegistersAvailable');

  return (
    <SafeAreaView style={styles.container} edges={['top', 'bottom']}>
      <Text style={styles.title}>{t('settings:registerSelect.title')}</Text>
      <Text style={styles.intro}>{t('settings:registerSelect.intro')}</Text>

      {error ? (
        <View style={styles.errorBanner} accessibilityRole="alert">
          <Text style={styles.errorText}>{error}</Text>
        </View>
      ) : null}

      {loading ? <WaveLoader size={28} style={styles.loader} /> : null}

      {savingId ? (
        <Text style={styles.openingHint}>{t('settings:registerSelect.openingShift')}</Text>
      ) : null}

      {!loading && registers.length > 0 ? (
        <View style={styles.optionList}>
          {registers.map((register) => {
            const disabled = Boolean(savingId);
            const selected = savingId === register.id;
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
                accessibilityState={{ disabled, busy: selected }}>
                <View style={styles.optionTextWrap}>
                  <Text style={[styles.optionText, selected && styles.optionTextSelected]}>
                    {formatRegisterLabel(register.registerNumber)}
                  </Text>
                  <Text style={styles.optionMeta}>
                    {register.location?.trim()
                      ? register.location.trim()
                      : t('settings:registerSelect.noDescription')}
                  </Text>
                  <Text style={styles.optionStatus}>
                    {isOpenedOnSelect(register)
                      ? t('settings:registerSelect.statusClosedOpensOnSelect')
                      : t('settings:registerSelect.statusOpen')}
                  </Text>
                </View>
                <Text style={[styles.selectLabel, selected && styles.optionTextSelected]}>
                  {selected
                    ? t('settings:registerSelect.openingShift')
                    : t('settings:registerSelect.select')}
                </Text>
              </Pressable>
            );
          })}
        </View>
      ) : null}

      {!loading && registers.length === 0 ? (
        <Text style={styles.empty}>{emptyMessage}</Text>
      ) : null}

      {listFailure ? (
        <Pressable
          onPress={() => setRetryToken((n) => n + 1)}
          style={styles.linkButton}
          accessibilityRole="button">
          <Text style={styles.linkText}>{t('settings:registerAssignment.reloadList')}</Text>
        </Pressable>
      ) : null}

      <Pressable
        onPress={() => void logout()}
        style={styles.logoutButton}
        accessibilityRole="button"
        accessibilityLabel={t('auth:logout')}>
        <Text style={styles.logoutText}>{t('auth:logout')}</Text>
      </Pressable>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
    paddingHorizontal: SoftSpacing.lg,
    paddingTop: SoftSpacing.lg,
  },
  centered: {
    justifyContent: 'center',
    alignItems: 'center',
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.sm,
  },
  intro: {
    fontSize: 15,
    color: SoftColors.textSecondary,
    lineHeight: 22,
    marginBottom: SoftSpacing.lg,
  },
  loader: {
    marginVertical: SoftSpacing.md,
  },
  optionList: {
    gap: SoftSpacing.sm,
  },
  optionRow: {
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: 8,
    paddingVertical: 14,
    paddingHorizontal: 16,
    backgroundColor: SoftColors.bgCard,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: SoftSpacing.md,
  },
  optionRowSelected: {
    borderColor: SoftColors.accentDark,
    backgroundColor: SoftColors.bgAccent,
  },
  optionRowDisabled: {
    opacity: 0.55,
  },
  optionText: {
    fontSize: 16,
    color: SoftColors.textPrimary,
    fontWeight: '500',
  },
  optionTextWrap: {
    flex: 1,
  },
  optionMeta: {
    fontSize: 13,
    color: SoftColors.textSecondary,
    marginTop: 2,
  },
  optionStatus: {
    fontSize: 12,
    color: SoftColors.textMuted,
    marginTop: 4,
    fontWeight: '600',
  },
  selectLabel: {
    fontSize: 14,
    fontWeight: '700',
    color: SoftColors.accentDark,
  },
  openingHint: {
    fontSize: 14,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.sm,
  },
  optionTextSelected: {
    color: SoftColors.accentDark,
    fontWeight: '700',
  },
  empty: {
    fontSize: 14,
    color: SoftColors.textMuted,
    marginTop: SoftSpacing.sm,
    lineHeight: 20,
  },
  errorBanner: {
    backgroundColor: SoftColors.errorBg,
    borderWidth: 1,
    borderColor: SoftColors.error,
    borderRadius: 8,
    paddingVertical: 12,
    paddingHorizontal: 14,
    marginBottom: SoftSpacing.md,
  },
  errorText: {
    fontSize: 14,
    color: SoftColors.error,
    lineHeight: 20,
  },
  linkButton: {
    marginTop: SoftSpacing.md,
    paddingVertical: 4,
  },
  linkText: {
    color: SoftColors.info,
    fontSize: 14,
    fontWeight: '600',
  },
  logoutButton: {
    marginTop: 'auto',
    paddingVertical: SoftSpacing.md,
    alignItems: 'center',
  },
  logoutText: {
    fontSize: 15,
    color: SoftColors.textMuted,
    fontWeight: '600',
  },
});
