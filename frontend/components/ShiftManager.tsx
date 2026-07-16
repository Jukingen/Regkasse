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
  downloadDailyClosingReportPdf,
} from '../services/api/shiftService';
import { useShift } from '../hooks/useShift';
import { WaveLoader } from '../src/components/common/WaveLoader';
import { getFormattingLocaleForTextLocale } from '../i18n/localeUtils';
import {
  printDailyClosingReport,
  printDailyClosingReportPdf,
} from '../utils/dailyClosingReportPrint';
import {
  extractDailyClosingErrorDetails,
  getDailyClosingErrorMessage,
} from '../utils/errorMessages';
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
    performDailyClosing,
    isLoading,
    error,
    refresh,
    refreshDailyClosingStatus,
  } = useShift();
  const [showDailyClosingModal, setShowDailyClosingModal] = useState(false);
  const [cashCount, setCashCount] = useState('');
  const [dailyClosingNotes, setDailyClosingNotes] = useState('');
  const [dailyClosingError, setDailyClosingError] = useState<string | null>(null);

  const reportLabels = useCallback(
    () => ({
      title: t('settings:shift.dailyClosing.reportTitle'),
      date: t('settings:shift.dailyClosing.reportDate'),
      period: t('settings:shift.dailyClosing.reportPeriod'),
      cashRegisterId: t('settings:shift.dailyClosing.reportCashRegisterId'),
      register: t('settings:shift.dailyClosing.reportRegister'),
      cashier: t('settings:shift.dailyClosing.reportCashier'),
      tseStatus: t('settings:shift.dailyClosing.reportTseStatus'),
      tseProvider: t('settings:shift.dailyClosing.reportTseProvider'),
      depExport: t('settings:shift.dailyClosing.reportDepExport'),
      sales: t('settings:shift.dailyClosing.reportSales'),
      cash: t('settings:shift.dailyClosing.reportCash'),
      card: t('settings:shift.dailyClosing.reportCard'),
      voucher: t('settings:shift.dailyClosing.reportVoucher'),
      other: t('settings:shift.dailyClosing.reportOther'),
      paymentMethodsSection: t('settings:shift.dailyClosing.reportPaymentMethods'),
      financialSummarySection: t('settings:shift.dailyClosing.reportFinancialSummary'),
      transactionBreakdownSection: t('settings:shift.dailyClosing.reportTransactionBreakdown'),
      rksvStatusSection: t('settings:shift.dailyClosing.reportRksvStatus'),
      startbeleg: t('settings:shift.dailyClosing.reportStartbeleg'),
      monatsbeleg: t('settings:shift.dailyClosing.reportMonatsbeleg'),
      jahresbeleg: t('settings:shift.dailyClosing.reportJahresbeleg'),
      breakdownCash: t('settings:shift.dailyClosing.reportBreakdownCash'),
      breakdownCard: t('settings:shift.dailyClosing.reportBreakdownCard'),
      breakdownVoucher: t('settings:shift.dailyClosing.reportBreakdownVoucher'),
      breakdownCancellations: t('settings:shift.dailyClosing.reportBreakdownCancellations'),
      breakdownTotal: t('settings:shift.dailyClosing.reportBreakdownTotal'),
      cashCount: t('settings:shift.dailyClosing.reportCashCount'),
      difference: t('settings:shift.dailyClosing.reportDifference'),
      fiscalTotal: t('settings:shift.dailyClosing.reportFiscalTotal'),
      fiscalNet: t('settings:shift.dailyClosing.reportFiscalNet'),
      fiscalTax: t('settings:shift.dailyClosing.reportFiscalTax'),
      taxSection: t('settings:shift.dailyClosing.reportTaxSection'),
      tax20: t('settings:shift.dailyClosing.reportTax20'),
      tax10: t('settings:shift.dailyClosing.reportTax10'),
      tax0: t('settings:shift.dailyClosing.reportTax0'),
      transactions: t('settings:shift.dailyClosing.reportTransactions'),
      tseSignature: t('settings:shift.dailyClosing.reportTse'),
      tseVerification: t('settings:shift.dailyClosing.reportTseVerification'),
      previousSignature: t('settings:shift.dailyClosing.reportPreviousSignature'),
      disclaimer: t('settings:shift.dailyClosing.reportDisclaimer'),
    }),
    [t]
  );

  const closeDailyClosingModal = useCallback(() => {
    setShowDailyClosingModal(false);
    setCashCount('');
    setDailyClosingNotes('');
    setDailyClosingError(null);
  }, []);

  const handleDailyClosing = useCallback(async () => {
    const amount = parseMoneyInput(cashCount);
    if (amount == null) {
      Alert.alert(t('settings:shift.invalidAmountTitle'), t('settings:shift.invalidAmountMessage'));
      return;
    }
    setDailyClosingError(null);
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
      const details = extractDailyClosingErrorDetails(e);
      const userMessage = getDailyClosingErrorMessage(details.code, { count: details.count });
      setDailyClosingError(userMessage);
      if (__DEV__) {
        console.error('[DailyClosing] Technical error:', {
          errorCode: details.code,
          technicalMessage: details.technicalMessage,
        });
      }
      Alert.alert(t('settings:shift.errorTitle'), userMessage);
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
  const shiftStatus = activeShift ? 'open' : 'closed';

  if (isLoading && !activeShift && !showDailyClosingModal) {
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

      <View style={styles.shiftStatus} accessibilityRole="text">
        <Text style={styles.statusText}>
          {shiftStatus === 'open'
            ? t('settings:shift.statusIndicatorOpen')
            : t('settings:shift.statusIndicatorClosed')}
        </Text>
      </View>

      {activeShift ? (
        <View style={styles.dailyClosingBlock}>
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
            onPress={() => {
              setDailyClosingError(null);
              setShowDailyClosingModal(true);
            }}
            disabled={!canRunDailyClosing}
            accessibilityRole="button"
            accessibilityLabel={t('settings:shift.dailyClosing.button')}
          >
            <Text style={styles.primaryBtnText}>{t('settings:shift.dailyClosing.button')}</Text>
          </Pressable>
        </View>
      ) : null}

      <Modal
        visible={showDailyClosingModal}
        transparent
        animationType="slide"
        onRequestClose={closeDailyClosingModal}
      >
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={styles.modalTitle}>{t('settings:shift.dailyClosing.modalTitle')}</Text>
            <Text style={styles.modalHint}>{t('settings:shift.dailyClosing.modalHint')}</Text>
            {dailyClosingError ? (
              <View style={styles.modalErrorBox} accessibilityRole="alert">
                <Text style={styles.modalErrorText}>{dailyClosingError}</Text>
              </View>
            ) : null}
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
  shiftStatus: {
    marginBottom: 12,
    paddingVertical: 10,
    paddingHorizontal: 12,
    borderRadius: 8,
    backgroundColor: '#f1f5f9',
  },
  statusText: {
    fontSize: 15,
    fontWeight: '600',
    color: '#334155',
  },
  dailyClosingBlock: {
    gap: 8,
  },
  primaryBtn: {
    backgroundColor: '#1976d2',
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 8,
    alignItems: 'center',
    marginTop: 4,
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
    marginBottom: 8,
  },
  modalHint: {
    fontSize: 14,
    color: '#64748b',
    marginBottom: 12,
    lineHeight: 20,
  },
  modalErrorBox: {
    backgroundColor: '#fee2e2',
    borderRadius: 8,
    padding: 12,
    marginBottom: 12,
  },
  modalErrorText: {
    color: '#dc2626',
    fontSize: 14,
    lineHeight: 20,
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
