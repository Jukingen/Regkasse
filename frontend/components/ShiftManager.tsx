import React, { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';

import { DailyClosingModal } from './DailyClosingModal';
import { useShift } from '../hooks/useShift';
import { getFormattingLocaleForTextLocale } from '../i18n/localeUtils';
import { downloadDailyClosingReportPdf } from '../services/api/shiftService';
import { WaveLoader } from '../src/components/common/WaveLoader';
import {
  printDailyClosingReport,
  printDailyClosingReportPdf,
} from '../utils/dailyClosingReportPrint';
import {
  extractDailyClosingErrorDetails,
  getDailyClosingErrorMessage,
} from '../utils/errorMessages';
import { isPrintCancelled } from '../utils/expoPrintShare';
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

  /** Opens the Tagesabschluss modal (settings → ShiftManager button). */
  const openDailyClosingModal = useCallback(() => {
    const canRun = Boolean(activeShift && dailyClosingStatus?.canClose && !isLoading);
    if (__DEV__) {
      console.log('🔄 Tagesabschluss button clicked', {
        canRunDailyClosing: canRun,
        hasActiveShift: Boolean(activeShift),
        canClose: dailyClosingStatus?.canClose ?? null,
        blockReason: dailyClosingStatus?.blockReason ?? null,
        isLoading,
      });
    }
    if (!canRun) {
      if (__DEV__) {
        console.log('⚠️ Tagesabschluss modal not opened (button gated)');
      }
      return;
    }
    setDailyClosingError(null);
    setShowDailyClosingModal(true);
    if (__DEV__) {
      console.log('✅ setShowDailyClosingModal(true)');
    }
  }, [activeShift, dailyClosingStatus, isLoading]);

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
      } catch (printErr) {
        if (!isPrintCancelled(printErr)) {
          Alert.alert(t('settings:shift.errorTitle'), t('settings:shift.dailyClosing.printFailed'));
        }
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

  const canRunDailyClosing = Boolean(activeShift && dailyClosingStatus?.canClose && !isLoading);
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
              ]}>
              {resolveDailyClosingStatusMessage(dailyClosingStatus, t)}
            </Text>
          ) : null}
          <Pressable
            style={[
              styles.primaryBtn,
              styles.closingBtn,
              !canRunDailyClosing && styles.btnDisabled,
            ]}
            onPress={openDailyClosingModal}
            // Keep pressable while gated so __DEV__ click logs still fire (disabled swallows onPress).
            accessibilityState={{ disabled: !canRunDailyClosing }}
            accessibilityRole="button"
            accessibilityLabel={t('settings:shift.dailyClosing.button')}>
            <Text style={styles.primaryBtnText}>{t('settings:shift.dailyClosing.button')}</Text>
          </Pressable>
        </View>
      ) : null}

      <DailyClosingModal
        visible={showDailyClosingModal}
        onClose={closeDailyClosingModal}
        onConfirm={() => void handleDailyClosing()}
        cashCount={cashCount}
        onCashCountChange={setCashCount}
        notes={dailyClosingNotes}
        onNotesChange={setDailyClosingNotes}
        error={dailyClosingError}
        isLoading={isLoading}
      />
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
});
