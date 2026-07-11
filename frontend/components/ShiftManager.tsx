import React, { useCallback, useState } from 'react';
import {
  Alert,
  Modal,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { useTranslation } from 'react-i18next';

import {
  DailyClosingApiError,
  downloadDailyClosingReportPdf,
} from '../services/api/shiftService';
import { useShift } from '../hooks/useShift';
import { WaveLoader } from '../src/components/common/WaveLoader';
import { getFormattingLocaleForTextLocale } from '../i18n/localeUtils';
import {
  printDailyClosingReport,
  printDailyClosingReportPdf,
} from '../utils/dailyClosingReportPrint';
import { formatPrice } from '../utils/formatPrice';
import { resolveDailyClosingStatusMessage } from '../utils/resolveDailyClosingStatusMessage';

function parseMoneyInput(value: string): number | null {
  const normalized = value.trim().replace(/\s/g, '').replace(',', '.');
  if (!normalized) return null;
  const n = Number(normalized);
  return Number.isFinite(n) && n >= 0 ? n : null;
}

export function ShiftManager() {
  const { t, i18n } = useTranslation(['settings', 'common']);
  const {
    activeShift,
    cashRegisterId,
    dailyClosingStatus,
    startShift,
    endShift,
    performDailyClosing,
    isLoading,
    error,
    refresh,
    refreshDailyClosingStatus,
  } = useShift();
  const [showStartModal, setShowStartModal] = useState(false);
  const [showEndModal, setShowEndModal] = useState(false);
  const [showDailyClosingModal, setShowDailyClosingModal] = useState(false);
  const [startBalance, setStartBalance] = useState('');
  const [endBalance, setEndBalance] = useState('');
  const [cashCount, setCashCount] = useState('');
  const [notes, setNotes] = useState('');
  const [dailyClosingNotes, setDailyClosingNotes] = useState('');

  const reportLabels = useCallback(
    () => ({
      title: t('settings:shift.dailyClosing.reportTitle'),
      date: t('settings:shift.dailyClosing.reportDate'),
      register: t('settings:shift.dailyClosing.reportRegister'),
      sales: t('settings:shift.dailyClosing.reportSales'),
      cash: t('settings:shift.dailyClosing.reportCash'),
      card: t('settings:shift.dailyClosing.reportCard'),
      cashCount: t('settings:shift.dailyClosing.reportCashCount'),
      difference: t('settings:shift.dailyClosing.reportDifference'),
      fiscalTotal: t('settings:shift.dailyClosing.reportFiscalTotal'),
      fiscalTax: t('settings:shift.dailyClosing.reportFiscalTax'),
      transactions: t('settings:shift.dailyClosing.reportTransactions'),
      tseSignature: t('settings:shift.dailyClosing.reportTse'),
      disclaimer: t('settings:shift.dailyClosing.reportDisclaimer'),
    }),
    [t]
  );

  const closeStartModal = useCallback(() => {
    setShowStartModal(false);
    setStartBalance('');
  }, []);

  const closeEndModal = useCallback(() => {
    setShowEndModal(false);
    setEndBalance('');
    setNotes('');
  }, []);

  const closeDailyClosingModal = useCallback(() => {
    setShowDailyClosingModal(false);
    setCashCount('');
    setDailyClosingNotes('');
  }, []);

  const handleStart = useCallback(async () => {
    const amount = parseMoneyInput(startBalance);
    if (amount == null) {
      Alert.alert(t('settings:shift.invalidAmountTitle'), t('settings:shift.invalidAmountMessage'));
      return;
    }
    try {
      await startShift(amount);
      closeStartModal();
      Alert.alert(t('settings:shift.startedTitle'), t('settings:shift.startedMessage'));
    } catch (e) {
      const noRegister = e instanceof Error && e.message === 'NO_REGISTER';
      Alert.alert(
        t('settings:shift.errorTitle'),
        noRegister ? t('settings:shift.noRegisterMessage') : t('settings:shift.startFailedMessage')
      );
    }
  }, [startBalance, startShift, closeStartModal, t]);

  const handleEnd = useCallback(async () => {
    const amount = parseMoneyInput(endBalance);
    if (amount == null) {
      Alert.alert(t('settings:shift.invalidAmountTitle'), t('settings:shift.invalidAmountMessage'));
      return;
    }
    try {
      const result = await endShift(amount, notes);
      closeEndModal();
      const diff = formatPrice(result.receipt.difference);
      Alert.alert(
        t('settings:shift.endedTitle'),
        t('settings:shift.endedMessage', {
          sales: formatPrice(result.receipt.totalSales),
          difference: diff,
        })
      );
    } catch {
      Alert.alert(t('settings:shift.errorTitle'), t('settings:shift.endFailedMessage'));
    }
  }, [endBalance, notes, endShift, closeEndModal, t]);

  const handleDailyClosing = useCallback(async () => {
    const amount = parseMoneyInput(cashCount);
    if (amount == null) {
      Alert.alert(t('settings:shift.invalidAmountTitle'), t('settings:shift.invalidAmountMessage'));
      return;
    }
    try {
      const result = await performDailyClosing(amount, dailyClosingNotes);
      closeDailyClosingModal();
      const report = result.report;
      try {
        if (result.dailyClosingId) {
          const pdf = await downloadDailyClosingReportPdf(result.dailyClosingId, i18n.language);
          await printDailyClosingReportPdf(pdf, result.dailyClosingId);
        } else if (report) {
          await printDailyClosingReport(
            report,
            reportLabels(),
            getFormattingLocaleForTextLocale(i18n.language)
          );
        }
      } catch {
        Alert.alert(t('settings:shift.errorTitle'), t('settings:shift.dailyClosing.printFailed'));
      }
      Alert.alert(
        t('settings:shift.dailyClosing.successTitle'),
        t('settings:shift.dailyClosing.successMessage', {
          sales: formatPrice(report?.totalSales ?? 0),
          difference: formatPrice(report?.difference ?? 0),
        })
      );
    } catch (e) {
      const blocked =
        e instanceof DailyClosingApiError &&
        typeof e.paymentsWithoutInvoiceCount === 'number' &&
        e.paymentsWithoutInvoiceCount > 0;
      const detail = blocked
        ? ` (${e.paymentsWithoutInvoiceCount})`
        : '';
      Alert.alert(
        t('settings:shift.errorTitle'),
        (e instanceof Error ? e.message : t('settings:shift.dailyClosing.failedMessage')) + detail
      );
      void refreshDailyClosingStatus();
    }
  }, [
    cashCount,
    dailyClosingNotes,
    performDailyClosing,
    closeDailyClosingModal,
    reportLabels,
    i18n.language,
    t,
    refreshDailyClosingStatus,
  ]);

  const canRunDailyClosing = Boolean(
    activeShift && dailyClosingStatus?.canClose && !isLoading
  );

  if (isLoading && !activeShift && !showStartModal && !showEndModal && !showDailyClosingModal) {
    return (
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>{t('settings:shift.title')}</Text>
        <WaveLoader size={28} style={{ marginVertical: 12 }} />
      </View>
    );
  }

  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>{t('settings:shift.title')}</Text>
      <Text style={styles.muted}>{t('settings:shift.intro')}</Text>

      {error ? (
        <View style={styles.errorBox}>
          <Text style={styles.errorText}>{error}</Text>
          <Pressable onPress={() => void refresh()} style={styles.retryBtn}>
            <Text style={styles.retryText}>{t('settings:registerAssignment.retry')}</Text>
          </Pressable>
        </View>
      ) : null}

      {!cashRegisterId ? (
        <Text style={styles.warnText}>{t('settings:shift.noRegisterMessage')}</Text>
      ) : null}

      {activeShift ? (
        <View style={styles.activeCard}>
          <Text style={styles.activeTitle}>{t('settings:shift.active')}</Text>
          <Text style={styles.row}>
            {t('settings:shift.startBalance')}: {formatPrice(activeShift.startBalance)}
          </Text>
          <Text style={styles.row}>
            {t('settings:shift.sales')}: {formatPrice(activeShift.totalSales)}
          </Text>
          {dailyClosingStatus ? (
            <Text
              style={[
                styles.statusHint,
                dailyClosingStatus.canClose ? styles.statusHintReady : styles.statusHintBlocked,
              ]}
            >
              {resolveDailyClosingStatusMessage(dailyClosingStatus, t)}
            </Text>
          ) : null}
          <Pressable
            style={[styles.primaryBtn, styles.closingBtn, !canRunDailyClosing && styles.btnDisabled]}
            onPress={() => setShowDailyClosingModal(true)}
            disabled={!canRunDailyClosing}
            accessibilityRole="button"
            accessibilityLabel={t('settings:shift.dailyClosing.button')}
          >
            <Text style={styles.primaryBtnText}>{t('settings:shift.dailyClosing.button')}</Text>
          </Pressable>
          <Pressable
            style={[styles.primaryBtn, styles.endBtn]}
            onPress={() => setShowEndModal(true)}
            disabled={isLoading}
            accessibilityRole="button"
            accessibilityLabel={t('settings:shift.endShift')}
          >
            <Text style={styles.primaryBtnText}>{t('settings:shift.endShift')}</Text>
          </Pressable>
        </View>
      ) : (
        <Pressable
          style={[styles.primaryBtn, !cashRegisterId && styles.btnDisabled]}
          onPress={() => setShowStartModal(true)}
          disabled={!cashRegisterId || isLoading}
          accessibilityRole="button"
          accessibilityLabel={t('settings:shift.startShift')}
        >
          <Text style={styles.primaryBtnText}>{t('settings:shift.startShift')}</Text>
        </Pressable>
      )}

      <Modal visible={showStartModal} transparent animationType="slide" onRequestClose={closeStartModal}>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={styles.modalTitle}>{t('settings:shift.startModalTitle')}</Text>
            <Text style={styles.modalLabel}>{t('settings:shift.startBalanceLabel')}</Text>
            <TextInput
              style={styles.input}
              keyboardType="decimal-pad"
              value={startBalance}
              onChangeText={setStartBalance}
              placeholder="0,00"
              placeholderTextColor="#999"
            />
            <View style={styles.modalActions}>
              <Pressable style={styles.secondaryBtn} onPress={closeStartModal}>
                <Text style={styles.secondaryBtnText}>{t('common:cancel')}</Text>
              </Pressable>
              <Pressable
                style={[styles.primaryBtn, isLoading && styles.btnDisabled]}
                onPress={() => void handleStart()}
                disabled={isLoading}
              >
                <Text style={styles.primaryBtnText}>
                  {isLoading ? t('settings:shift.working') : t('settings:shift.confirmStart')}
                </Text>
              </Pressable>
            </View>
          </View>
        </View>
      </Modal>

      <Modal visible={showEndModal} transparent animationType="slide" onRequestClose={closeEndModal}>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={styles.modalTitle}>{t('settings:shift.endModalTitle')}</Text>
            <Text style={styles.modalLabel}>{t('settings:shift.endBalanceLabel')}</Text>
            <TextInput
              style={styles.input}
              keyboardType="decimal-pad"
              value={endBalance}
              onChangeText={setEndBalance}
              placeholder="0,00"
              placeholderTextColor="#999"
            />
            <Text style={styles.modalLabel}>{t('settings:shift.notesLabel')}</Text>
            <TextInput
              style={[styles.input, styles.notesInput]}
              value={notes}
              onChangeText={setNotes}
              placeholder={t('settings:shift.notesPlaceholder')}
              placeholderTextColor="#999"
              multiline
            />
            <View style={styles.modalActions}>
              <Pressable style={styles.secondaryBtn} onPress={closeEndModal}>
                <Text style={styles.secondaryBtnText}>{t('common:cancel')}</Text>
              </Pressable>
              <Pressable
                style={[styles.primaryBtn, styles.endBtn, isLoading && styles.btnDisabled]}
                onPress={() => void handleEnd()}
                disabled={isLoading}
              >
                <Text style={styles.primaryBtnText}>
                  {isLoading ? t('settings:shift.working') : t('settings:shift.confirmEnd')}
                </Text>
              </Pressable>
            </View>
          </View>
        </View>
      </Modal>

      <Modal
        visible={showDailyClosingModal}
        transparent
        animationType="slide"
        onRequestClose={closeDailyClosingModal}
      >
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={styles.modalTitle}>{t('settings:shift.dailyClosing.modalTitle')}</Text>
            <Text style={styles.modalLabel}>{t('settings:shift.dailyClosing.cashCountLabel')}</Text>
            <TextInput
              style={styles.input}
              keyboardType="decimal-pad"
              value={cashCount}
              onChangeText={setCashCount}
              placeholder="0,00"
              placeholderTextColor="#999"
            />
            <Text style={styles.modalLabel}>{t('settings:shift.dailyClosing.notesLabel')}</Text>
            <TextInput
              style={[styles.input, styles.notesInput]}
              value={dailyClosingNotes}
              onChangeText={setDailyClosingNotes}
              placeholder={t('settings:shift.notesPlaceholder')}
              placeholderTextColor="#999"
              multiline
            />
            <View style={styles.modalActions}>
              <Pressable style={styles.secondaryBtn} onPress={closeDailyClosingModal}>
                <Text style={styles.secondaryBtnText}>{t('common:cancel')}</Text>
              </Pressable>
              <Pressable
                style={[styles.primaryBtn, styles.closingBtn, isLoading && styles.btnDisabled]}
                onPress={() => void handleDailyClosing()}
                disabled={isLoading}
              >
                <Text style={styles.primaryBtnText}>
                  {isLoading ? t('settings:shift.working') : t('settings:shift.dailyClosing.confirm')}
                </Text>
              </Pressable>
            </View>
          </View>
        </View>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  section: {
    marginTop: 8,
    paddingVertical: 8,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 8,
    color: '#333',
  },
  muted: {
    fontSize: 14,
    color: '#666',
    marginBottom: 12,
    lineHeight: 20,
  },
  warnText: {
    fontSize: 14,
    color: '#b45309',
    marginBottom: 10,
    lineHeight: 20,
  },
  errorBox: {
    backgroundColor: '#ffebee',
    borderRadius: 8,
    padding: 10,
    marginBottom: 10,
  },
  errorText: {
    color: '#c62828',
    fontSize: 13,
  },
  retryBtn: { marginTop: 6 },
  retryText: { color: '#1976d2', fontWeight: '600' },
  activeCard: {
    backgroundColor: '#e8f5e9',
    borderRadius: 10,
    padding: 14,
    borderWidth: 1,
    borderColor: '#c8e6c9',
    gap: 6,
  },
  activeTitle: {
    fontSize: 15,
    fontWeight: '700',
    color: '#2e7d32',
    marginBottom: 4,
  },
  row: {
    fontSize: 14,
    color: '#333',
  },
  primaryBtn: {
    backgroundColor: '#1976d2',
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 8,
    alignItems: 'center',
    marginTop: 4,
  },
  endBtn: {
    backgroundColor: '#c62828',
  },
  closingBtn: {
    backgroundColor: '#5e35b1',
  },
  statusHint: {
    fontSize: 13,
    marginTop: 4,
    lineHeight: 18,
  },
  statusHintReady: {
    color: '#2e7d32',
  },
  statusHintBlocked: {
    color: '#6d4c41',
  },
  btnDisabled: {
    opacity: 0.5,
  },
  primaryBtnText: {
    color: '#fff',
    fontWeight: '600',
    fontSize: 15,
  },
  modalBackdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'center',
    padding: 24,
  },
  modalCard: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 20,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '700',
    color: '#333',
    marginBottom: 12,
  },
  modalLabel: {
    fontSize: 14,
    color: '#555',
    marginBottom: 6,
    marginTop: 8,
  },
  input: {
    borderWidth: 1,
    borderColor: '#ccc',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 16,
    color: '#333',
  },
  notesInput: {
    minHeight: 72,
    textAlignVertical: 'top',
  },
  modalActions: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    gap: 10,
    marginTop: 16,
  },
  secondaryBtn: {
    paddingVertical: 10,
    paddingHorizontal: 14,
  },
  secondaryBtnText: {
    color: '#666',
    fontWeight: '600',
  },
});
