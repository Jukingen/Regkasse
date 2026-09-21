import { Redirect, router } from 'expo-router';
import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useAuth } from '../../contexts/AuthContext';
import { SoftColors, SoftSpacing } from '../../constants/SoftTheme';
import {
  fetchMyPosCashRegisterOpenRequests,
  fetchPosSelectableRegisters,
  notifyPosMonatsbelegManager,
  requestPosCashRegisterOpen,
  setDefaultPosCashRegister,
  type CashRegisterOpenRequestRow,
  type CashRegisterSelectableRow,
  type PosSelectableEmptyReason,
} from '../../services/api/cashRegisterService';
import { autoCloseShiftApi, autoOpenShiftApi } from '../../services/api/shiftService';
import { WaveLoader } from '../../src/components/common/WaveLoader';
import {
  needsPosCashRegisterSelection,
  readValidPosCashRegisterId,
} from '../../utils/posCashRegister';
import { isClosedRegister, resolvePosPickerRowKind } from '../../utils/posSelectableRegisterFilter';
import { hasPermission } from '../../utils/posPermissions';
import {
  classifyRegisterListError,
  type RegisterListFailureKind,
} from '../../utils/registerListError';
import {
  parseShiftAutoOpenError,
  resolveRegisterSelectAutoOpenUx,
  SHIFT_AUTO_OPEN_CODES,
  type RegisterSelectAutoOpenUx,
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
  const [requestingId, setRequestingId] = useState<string | null>(null);
  const [myRequests, setMyRequests] = useState<CashRegisterOpenRequestRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [autoOpenBanner, setAutoOpenBanner] = useState<
    (RegisterSelectAutoOpenUx & { registerId: string }) | null
  >(null);
  const [bannerBusy, setBannerBusy] = useState(false);
  const [requestNotice, setRequestNotice] = useState<string | null>(null);
  const [retryToken, setRetryToken] = useState(0);

  const loadRegisters = useCallback(async (opts?: { silent?: boolean }) => {
    if (!opts?.silent) {
      setLoading(true);
      setError(null);
      setAutoOpenBanner(null);
      setListFailure(null);
      setEmptyReason(null);
    }
    try {
      const [{ registers: rows, emptyReason: reason }, requests] = await Promise.all([
        fetchPosSelectableRegisters(),
        fetchMyPosCashRegisterOpenRequests().catch(() => [] as CashRegisterOpenRequestRow[]),
      ]);
      setRegisters(rows);
      setEmptyReason(reason);
      setMyRequests(requests);
    } catch (e) {
      setRegisters([]);
      setListFailure(classifyRegisterListError(e));
    } finally {
      if (!opts?.silent) setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!isAuthReady || !isAuthenticated) return;
    void loadRegisters();
  }, [isAuthReady, isAuthenticated, loadRegisters, retryToken]);

  const canOpenShift = hasPermission(user, 'shift.open');
  const hasPendingOpenRequest = myRequests.some((row) => row.status === 'Pending');

  useEffect(() => {
    if (!isAuthReady || !isAuthenticated) return;
    if (!hasPendingOpenRequest && canOpenShift) return;
    if (!hasPendingOpenRequest && !registers.some((row) => isClosedRegister(row))) return;
    const timer = setInterval(() => {
      void loadRegisters({ silent: true });
    }, 10_000);
    return () => clearInterval(timer);
  }, [isAuthReady, isAuthenticated, hasPendingOpenRequest, canOpenShift, registers, loadRegisters]);

  const handleSelect = useCallback(
    async (registerId: string) => {
      const trimmed = readValidPosCashRegisterId(registerId);
      if (!trimmed || savingId) return;

      setSavingId(trimmed);
      setError(null);
      setAutoOpenBanner(null);
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
        const ux = resolveRegisterSelectAutoOpenUx(parsed.code, parsed.httpStatus);
        setAutoOpenBanner({ ...ux, registerId: trimmed });
      } finally {
        setSavingId(null);
      }
    },
    [savingId, setCurrentCashRegisterId, t]
  );

  const latestRequestByRegisterId = React.useMemo(() => {
    const map = new Map<string, CashRegisterOpenRequestRow>();
    for (const row of myRequests) {
      if (!map.has(row.cashRegisterId)) {
        map.set(row.cashRegisterId, row);
      }
    }
    return map;
  }, [myRequests]);

  const handleRequestOpen = useCallback(
    async (registerId: string) => {
      const trimmed = readValidPosCashRegisterId(registerId);
      if (!trimmed || requestingId || savingId) return;
      setRequestingId(trimmed);
      setError(null);
      setRequestNotice(null);
      try {
        const result = await requestPosCashRegisterOpen(trimmed);
        if (!result.succeeded && result.code !== 'OPEN_REQUEST_ALREADY_PENDING') {
          setError(t('settings:registerSelect.requestFailed'));
          return;
        }
        setAutoOpenBanner(null);
        setRequestNotice(t('settings:registerSelect.requestSent'));
        await loadRegisters({ silent: true });
      } catch {
        setError(t('settings:registerSelect.requestFailed'));
      } finally {
        setRequestingId(null);
      }
    },
    [loadRegisters, requestingId, savingId, t]
  );

  const onBannerAction = useCallback(async () => {
    if (!autoOpenBanner || bannerBusy || savingId || requestingId) return;
    if (autoOpenBanner.action === 'none' || autoOpenBanner.action === 'noop') return;

    setBannerBusy(true);
    setError(null);
    try {
      if (autoOpenBanner.action === 'requestOpen') {
        await handleRequestOpen(autoOpenBanner.registerId);
        return;
      }
      if (autoOpenBanner.action === 'reload') {
        await loadRegisters();
        return;
      }
      if (autoOpenBanner.action === 'closeOther') {
        await autoCloseShiftApi();
        await loadRegisters();
        return;
      }
      if (autoOpenBanner.action === 'notifyManager') {
        await notifyPosMonatsbelegManager(autoOpenBanner.registerId);
      }
    } catch {
      setError(t('settings:registerSelect.requestFailed'));
    } finally {
      setBannerBusy(false);
    }
  }, [autoOpenBanner, bannerBusy, handleRequestOpen, loadRegisters, requestingId, savingId, t]);

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
      <Text style={styles.intro}>
        {t(
          canOpenShift
            ? 'settings:registerSelect.intro'
            : 'settings:registerSelect.introNoPermission'
        )}
      </Text>

      {autoOpenBanner ? (
        <View
          testID={
            autoOpenBanner.tone === 'info'
              ? 'register-select-info-banner'
              : 'register-select-error-banner'
          }
          style={autoOpenBanner.tone === 'info' ? styles.infoBanner : styles.errorBanner}
          accessibilityRole="alert">
          <Text style={autoOpenBanner.tone === 'info' ? styles.infoText : styles.errorText}>
            {t(autoOpenBanner.messageKey)}
          </Text>
          {autoOpenBanner.buttonKey ? (
            <Pressable
              testID="register-select-banner-action"
              disabled={bannerBusy || Boolean(savingId || requestingId)}
              onPress={() => void onBannerAction()}
              style={styles.bannerAction}
              accessibilityRole="button"
              accessibilityState={{ disabled: bannerBusy }}>
              <Text
                style={
                  autoOpenBanner.tone === 'info' ? styles.bannerActionInfo : styles.bannerActionError
                }>
                {t(autoOpenBanner.buttonKey)}
              </Text>
            </Pressable>
          ) : null}
        </View>
      ) : null}

      {error ? (
        <View style={styles.errorBanner} accessibilityRole="alert">
          <Text style={styles.errorText}>{error}</Text>
        </View>
      ) : null}

      {requestNotice ? (
        <View style={styles.successBanner} accessibilityRole="alert">
          <Text style={styles.successText}>{requestNotice}</Text>
        </View>
      ) : null}

      {loading ? <WaveLoader size={28} style={styles.loader} /> : null}

      {savingId ? (
        <Text style={styles.openingHint}>{t('settings:registerSelect.openingShift')}</Text>
      ) : null}

      {!loading && registers.length > 0 ? (
        <View style={styles.optionList}>
          {registers.map((register) => {
            const kind = resolvePosPickerRowKind(register, canOpenShift);
            const latestRequest = latestRequestByRegisterId.get(register.id);
            const pending = latestRequest?.status === 'Pending';
            const denied = latestRequest?.status === 'Denied';
            const requesting = requestingId === register.id;
            const selected = savingId === register.id;
            const busy = Boolean(savingId || requestingId);

            if (kind === 'unavailable') {
              return (
                <View key={register.id} style={[styles.optionRow, styles.optionRowClosed]}>
                  <View style={styles.optionTextWrap}>
                    <Text style={styles.optionText}>
                      {formatRegisterLabel(register.registerNumber)}
                    </Text>
                    <Text style={styles.optionStatus}>
                      {t('settings:registerSelect.statusUnavailable')}
                    </Text>
                  </View>
                </View>
              );
            }

            if (kind === 'requestOpen') {
              return (
                <View key={register.id} style={[styles.optionRow, styles.optionRowClosed]}>
                  <View style={styles.optionTextWrap}>
                    <Text style={styles.optionText}>
                      {formatRegisterLabel(register.registerNumber)}
                    </Text>
                    <Text style={styles.optionMeta}>
                      {register.location?.trim()
                        ? register.location.trim()
                        : t('settings:registerSelect.noDescription')}
                    </Text>
                    <Text style={styles.optionStatus}>
                      {t('settings:registerSelect.statusClosed')}
                    </Text>
                    <Text style={styles.optionHint}>
                      {denied
                        ? t('settings:registerSelect.requestDenied')
                        : t('settings:registerSelect.mustBeOpenedByManager')}
                    </Text>
                  </View>
                  <Pressable
                    disabled={busy || pending}
                    onPress={() => void handleRequestOpen(register.id)}
                    style={[
                      styles.requestButton,
                      (busy || pending) && styles.optionRowDisabled,
                    ]}
                    accessibilityRole="button"
                    accessibilityState={{ disabled: busy || pending, busy: requesting }}>
                    <Text style={styles.requestButtonText}>
                      {pending || requesting
                        ? t('settings:registerSelect.requestSent')
                        : t('settings:registerSelect.requestOpen')}
                    </Text>
                  </Pressable>
                </View>
              );
            }

            const statusLabel =
              kind === 'opensOnSelect'
                ? t('settings:registerSelect.statusClosedOpensOnSelect')
                : t('settings:registerSelect.statusAvailable');

            return (
              <Pressable
                key={register.id}
                disabled={busy}
                onPress={() => void handleSelect(register.id)}
                style={[
                  styles.optionRow,
                  selected && styles.optionRowSelected,
                  busy && styles.optionRowDisabled,
                ]}
                testID="cash-register-select-option"
                accessibilityRole="button"
                accessibilityState={{ disabled: busy, busy: selected }}>
                <View style={styles.optionTextWrap}>
                  <Text style={[styles.optionText, selected && styles.optionTextSelected]}>
                    {formatRegisterLabel(register.registerNumber)}
                  </Text>
                  <Text style={styles.optionMeta}>
                    {register.location?.trim()
                      ? register.location.trim()
                      : t('settings:registerSelect.noDescription')}
                  </Text>
                  <Text style={styles.optionStatus}>{statusLabel}</Text>
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
        <Text style={styles.empty}>
          {listFailure ? t('settings:registerSelect.listLoadFailed') : emptyMessage}
        </Text>
      ) : null}

      <Pressable
        onPress={() => setRetryToken((n) => n + 1)}
        style={styles.linkButton}
        accessibilityRole="button">
        <Text style={styles.linkText}>{t('settings:registerAssignment.reloadList')}</Text>
      </Pressable>

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
  optionRowClosed: {
    alignItems: 'flex-start',
  },
  optionHint: {
    fontSize: 12,
    color: SoftColors.textSecondary,
    marginTop: 4,
    lineHeight: 17,
  },
  requestButton: {
    alignSelf: 'center',
    borderWidth: 1,
    borderColor: SoftColors.accentDark,
    borderRadius: 8,
    paddingVertical: 8,
    paddingHorizontal: 10,
    maxWidth: 150,
  },
  requestButtonText: {
    fontSize: 13,
    fontWeight: '700',
    color: SoftColors.accentDark,
    textAlign: 'center',
  },
  successBanner: {
    backgroundColor: SoftColors.bgAccent,
    borderWidth: 1,
    borderColor: SoftColors.accentDark,
    borderRadius: 8,
    paddingVertical: 12,
    paddingHorizontal: 14,
    marginBottom: SoftSpacing.md,
  },
  successText: {
    fontSize: 14,
    color: SoftColors.accentDark,
    lineHeight: 20,
    fontWeight: '600',
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
  infoBanner: {
    backgroundColor: SoftColors.infoBg,
    borderWidth: 1,
    borderColor: SoftColors.info,
    borderRadius: 8,
    paddingVertical: 12,
    paddingHorizontal: 14,
    marginBottom: SoftSpacing.md,
  },
  infoText: {
    fontSize: 14,
    color: SoftColors.textPrimary,
    lineHeight: 20,
  },
  bannerAction: {
    marginTop: 10,
    alignSelf: 'flex-start',
  },
  bannerActionInfo: {
    fontSize: 14,
    fontWeight: '700',
    color: SoftColors.info,
  },
  bannerActionError: {
    fontSize: 14,
    fontWeight: '700',
    color: SoftColors.error,
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
