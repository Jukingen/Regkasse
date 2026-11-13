// Settings screen: cash register assignment for POS payment (German UI copy).
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { View, Text, StyleSheet, TouchableOpacity, Alert } from 'react-native';

import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import {
  fetchPosSelectableRegisters,
  type CashRegisterSelectableRow,
  type PosSelectableEmptyReason,
} from '../services/api/cashRegisterService';
import { getUserSettings, updateCashRegisterConfig } from '../services/api/userSettingsService';
import { WaveLoader } from '../src/components/common/WaveLoader';
import { isValidPosCashRegisterId } from '../utils/posCashRegister';
import {
  buildPosRegisterGateContext,
  registerGateBannerDetail,
  registerGateBannerTitle,
} from '../utils/posRegisterGateCopy';
import type { RegisterListFailureKind } from '../utils/registerListError';
import { classifyRegisterListError } from '../utils/registerListError';

/**
 * Settings screen: persist POS cash-register assignment (GET settings + optional list + PUT cash-register).
 */
export function CashRegisterAssignmentSection() {
  const { t } = useTranslation(['settings']);
  const posReadiness = usePosRegisterReadiness();
  const [loadingSettings, setLoadingSettings] = useState(true);
  const [settingsLoadFailed, setSettingsLoadFailed] = useState(false);
  const [assignedId, setAssignedId] = useState<string | null>(null);
  const [picklist, setPicklist] = useState<CashRegisterSelectableRow[]>([]);
  const [listLoading, setListLoading] = useState(false);
  const [listFailureKind, setListFailureKind] = useState<RegisterListFailureKind | null>(null);
  const [listEmptyReason, setListEmptyReason] = useState<PosSelectableEmptyReason>(null);
  const [savingId, setSavingId] = useState<string | null>(null);
  const [listRetryToken, setListRetryToken] = useState(0);

  const load = useCallback(async () => {
    setLoadingSettings(true);
    setSettingsLoadFailed(false);
    try {
      const s = await getUserSettings();
      const id = s.cashRegisterId?.trim();
      const invalid = !id || id === '00000000-0000-0000-0000-000000000000';
      setAssignedId(invalid ? null : id);
    } catch {
      setSettingsLoadFailed(true);
      setAssignedId(null);
    } finally {
      setLoadingSettings(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (loadingSettings || settingsLoadFailed) return;
    if (isValidPosCashRegisterId(assignedId)) {
      setPicklist([]);
      setListEmptyReason(null);
      return;
    }

    let cancelled = false;
    setListLoading(true);
    setListFailureKind(null);
    setListEmptyReason(null);
    fetchPosSelectableRegisters()
      .then(({ registers: rows, emptyReason }) => {
        if (!cancelled) {
          setPicklist(rows);
          setListEmptyReason(emptyReason);
          setListFailureKind(null);
        }
      })
      .catch((e) => {
        if (!cancelled) {
          setPicklist([]);
          setListEmptyReason(null);
          setListFailureKind(classifyRegisterListError(e));
        }
      })
      .finally(() => {
        if (!cancelled) setListLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [loadingSettings, settingsLoadFailed, assignedId, listRetryToken]);

  const persist = async (id: string) => {
    const trimmedId = id.trim();
    if (!trimmedId) return;
    setSavingId(trimmedId);
    try {
      const updated = await updateCashRegisterConfig({ cashRegisterId: trimmedId });
      const next = updated.cashRegisterId?.trim();
      if (next && next !== '00000000-0000-0000-0000-000000000000') {
        setAssignedId(next);
      } else {
        setAssignedId(trimmedId);
      }
      await posReadiness.refreshAsync();
      Alert.alert(
        t('settings:registerAssignment.savedTitle'),
        t('settings:registerAssignment.savedMessage')
      );
    } catch (e) {
      console.warn('[CashRegisterAssignmentSection] save failed', e);
      Alert.alert(
        t('settings:registerAssignment.saveErrorTitle'),
        t('settings:registerAssignment.saveErrorMessage')
      );
    } finally {
      setSavingId(null);
    }
  };

  const registerGateCtx = useMemo(
    () =>
      buildPosRegisterGateContext({
        settingsLoadFailed,
        registerListFailureKind: listFailureKind,
        registerListLoading: listLoading,
        registerPicklistCount: picklist.length,
        registerListEmptyReason: listEmptyReason,
        readiness: {
          loading: posReadiness.loading,
          error: !!posReadiness.error,
          nextAction: posReadiness.data?.nextAction ?? null,
          messageCode: posReadiness.data?.messageCode ?? null,
          registerStatus: posReadiness.data?.registerStatus ?? null,
        },
      }),
    [
      settingsLoadFailed,
      listFailureKind,
      listLoading,
      picklist.length,
      listEmptyReason,
      posReadiness.loading,
      posReadiness.error,
      posReadiness.data?.nextAction,
      posReadiness.data?.messageCode,
      posReadiness.data?.registerStatus,
    ]
  );

  if (loadingSettings) {
    return (
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>{t('settings:registerAssignment.title')}</Text>
        <WaveLoader size={28} style={{ marginVertical: 12 }} />
      </View>
    );
  }

  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>{t('settings:registerAssignment.title')}</Text>
      <Text style={styles.muted}>{t('settings:registerAssignment.intro')}</Text>
      {isValidPosCashRegisterId(assignedId) ? (
        <Text style={styles.assigned}>
          {t('settings:registerAssignment.assignedPrefix')}{' '}
          <Text style={styles.assignedMono}>{(assignedId ?? '').slice(0, 8)}…</Text>
        </Text>
      ) : null}

      {!isValidPosCashRegisterId(assignedId) ? (
        <View style={styles.banner}>
          <Text style={styles.bannerTitle}>{registerGateBannerTitle(registerGateCtx)}</Text>
          {!listLoading && !posReadiness.loading ? (
            <Text style={styles.mutedSmall}>{t('settings:registerGate.banner.intro')}</Text>
          ) : null}
          <Text style={styles.bannerDetail}>{registerGateBannerDetail(registerGateCtx)}</Text>
          {settingsLoadFailed ? (
            <TouchableOpacity onPress={load} style={styles.retryBtn}>
              <Text style={styles.retryText}>{t('settings:registerAssignment.retry')}</Text>
            </TouchableOpacity>
          ) : null}
          {posReadiness.error ? (
            <TouchableOpacity
              onPress={() => {
                posReadiness.refresh();
              }}
              style={styles.retryBtn}>
              <Text style={styles.retryText}>
                {t('settings:registerAssignment.retryReadiness')}
              </Text>
            </TouchableOpacity>
          ) : null}
          {listLoading || posReadiness.loading ? (
            <WaveLoader size={28} style={{ marginVertical: 8 }} />
          ) : null}
          {!listLoading && picklist.length > 0 ? (
            <View style={styles.chipRow}>
              {picklist.map((r) => (
                <TouchableOpacity
                  key={r.id}
                  style={[styles.chip, savingId === r.id && styles.chipDisabled]}
                  disabled={!!savingId}
                  onPress={() => persist(r.id)}>
                  <Text style={styles.chipText}>{r.registerNumber || r.id.slice(0, 8)}</Text>
                </TouchableOpacity>
              ))}
            </View>
          ) : null}
          {!listLoading && (listFailureKind === 'network' || listFailureKind === 'unknown') ? (
            <TouchableOpacity
              onPress={() => {
                setListRetryToken((n) => n + 1);
              }}>
              <Text style={styles.retryText}>{t('settings:registerAssignment.reloadList')}</Text>
            </TouchableOpacity>
          ) : null}
        </View>
      ) : (
        <TouchableOpacity
          style={styles.changeBtn}
          onPress={() => {
            setAssignedId(null);
          }}>
          <Text style={styles.changeBtnText}>{t('settings:registerAssignment.change')}</Text>
        </TouchableOpacity>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  section: {
    marginTop: 8,
    paddingVertical: 8,
  },
  sectionTitle: { fontSize: 16, fontWeight: '600', marginBottom: 8, color: '#333' },
  muted: { fontSize: 14, color: '#666', marginBottom: 8, lineHeight: 20 },
  mutedSmall: { fontSize: 13, color: '#666', marginBottom: 4 },
  assigned: { fontSize: 14, color: '#1976d2', marginBottom: 8 },
  assignedMono: { fontFamily: 'monospace' },
  banner: {
    backgroundColor: '#fff8e1',
    borderRadius: 8,
    padding: 12,
    borderWidth: 1,
    borderColor: '#ffe082',
  },
  bannerTitle: { fontSize: 15, fontWeight: '700', color: '#5d4037', marginBottom: 4 },
  bannerDetail: { fontSize: 13, color: '#6d4c41', lineHeight: 18 },
  chipRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginTop: 10 },
  chip: {
    paddingVertical: 8,
    paddingHorizontal: 12,
    backgroundColor: '#1976d2',
    borderRadius: 20,
  },
  chipDisabled: { opacity: 0.5 },
  chipText: { color: '#fff', fontWeight: '600' },
  retryBtn: { marginTop: 10 },
  retryText: { color: '#1976d2', fontWeight: '600', marginTop: 8 },
  changeBtn: { marginTop: 8, padding: 10, alignItems: 'center' },
  changeBtnText: { color: '#1976d2', fontWeight: '600' },
});
