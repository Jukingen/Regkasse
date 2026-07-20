import React, { useCallback, useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';

import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import { useTagesabschlussStatus } from '../hooks/useTagesabschlussStatus';
import { useShift } from '../hooks/useShift';
import { getFormattingLocaleForTextLocale } from '../i18n/localeUtils';
import { downloadDailyClosingReportPdf } from '../services/api/shiftService';
import {
  printDailyClosingReport,
  printDailyClosingReportPdf,
} from '../utils/dailyClosingReportPrint';
import {
  extractDailyClosingErrorDetails,
  getDailyClosingErrorMessage,
} from '../utils/errorMessages';
import { formatPrice } from '../utils/formatPrice';
import { DailyClosingModal } from './DailyClosingModal';

function parseMoneyInput(value: string): number | null {
  const normalized = value.trim().replace(/\s/g, '').replace(',', '.');
  if (!normalized) return null;
  const n = Number(normalized);
  return Number.isFinite(n) && n >= 0 ? n : null;
}

/**
 * POS Tagesabschluss reminder banner (working-hours / midnight window).
 *
 * CRITICAL:
 * - Display + optional CTA only — never blocks cart, orders, payments, or navigation.
 * - Never auto-closes the register (RKSV); cashier must confirm via DailyClosingModal.
 * - Renders as a sibling banner above tabs, not a modal gate or full-screen lock.
 */
export function TagesabschlussReminder() {
  const { t, i18n } = useTranslation(['settings', 'common']);
  const {
    canClose,
    shouldShowReminder,
    hoursRemaining,
    countdownLabel,
    closingTimeLabel,
    usedWorkingHours,
    loading,
    refresh,
  } = useTagesabschlussStatus();
  const {
    performDailyClosing,
    isLoading,
    refreshDailyClosingStatus,
  } = useShift();

  const [showModal, setShowModal] = useState(false);
  const [cashCount, setCashCount] = useState('');
  const [notes, setNotes] = useState('');
  const [modalError, setModalError] = useState<string | null>(null);

  const closeModal = useCallback(() => {
    setShowModal(false);
    setCashCount('');
    setNotes('');
    setModalError(null);
  }, []);

  /** Opens Tagesabschluss modal on demand (never auto-triggered). */
  const handleDailyClosing = useCallback(() => {
    setModalError(null);
    setShowModal(true);
  }, []);

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
    [t],
  );

  const confirmDailyClosing = useCallback(async () => {
    const amount = parseMoneyInput(cashCount);
    if (amount == null) {
      Alert.alert(t('settings:shift.invalidAmountTitle'), t('settings:shift.invalidAmountMessage'));
      return;
    }
    setModalError(null);
    try {
      const result = await performDailyClosing(amount, notes);
      closeModal();
      const report = result.report;
      try {
        if (result.dailyClosingId) {
          const pdf = await downloadDailyClosingReportPdf(result.dailyClosingId, i18n.language);
          await printDailyClosingReportPdf(pdf, result.dailyClosingId);
        } else if (report) {
          await printDailyClosingReport(
            report,
            reportLabels(),
            getFormattingLocaleForTextLocale(i18n.language),
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
        }),
      );
      void refresh();
      void refreshDailyClosingStatus();
    } catch (e) {
      const details = extractDailyClosingErrorDetails(e);
      const userMessage = getDailyClosingErrorMessage(details.code, { count: details.count });
      setModalError(userMessage);
      Alert.alert(t('settings:shift.errorTitle'), userMessage);
      void refreshDailyClosingStatus();
      void refresh();
    }
  }, [
    cashCount,
    notes,
    performDailyClosing,
    closeModal,
    reportLabels,
    i18n.language,
    t,
    refresh,
    refreshDailyClosingStatus,
  ]);

  // Reminder visibility only — never gate POS operations when hidden or shown.
  // canClose: backend says a closing is allowed for this register (not a sales lock).
  // shouldShowReminder: within working-hours / midnight reminder window.
  if (loading || !shouldShowReminder || !canClose) {
    return null;
  }

  const deadlineHint = usedWorkingHours
    ? closingTimeLabel
      ? t('settings:dailyClosing.reminder.deadlineWorkingHours', { time: closingTimeLabel })
      : t('settings:dailyClosing.reminder.deadlineWorkingHoursGeneric')
    : t('settings:dailyClosing.reminder.deadlineMidnight');

  return (
    <>
      <Pressable
        style={({ pressed }) => [styles.warningBanner, pressed && styles.pressed]}
        onPress={handleDailyClosing}
        accessibilityRole="button"
        accessibilityLabel={t('settings:dailyClosing.reminder.cta')}
        accessibilityHint={t('settings:dailyClosing.reminder.body', {
          count: hoursRemaining,
          deadlineHint,
        })}
      >
        <View style={styles.titleRow}>
          <Text style={styles.icon} accessibilityElementsHidden>
            ⚠️
          </Text>
          <Text style={styles.warningTitle}>
            {t('settings:dailyClosing.reminder.title')}
          </Text>
        </View>
        <Text style={styles.warningText}>
          {t('settings:dailyClosing.reminder.body', {
            count: hoursRemaining,
            deadlineHint,
          })}
        </Text>
        <Text
          style={styles.countdown}
          accessibilityLabel={t('settings:dailyClosing.reminder.countdownA11y', {
            countdown: countdownLabel,
          })}
        >
          {countdownLabel}
        </Text>
        <View style={styles.closeButton}>
          <Text style={styles.closeButtonText}>
            {t('settings:dailyClosing.reminder.cta')}
          </Text>
        </View>
      </Pressable>

      <DailyClosingModal
        visible={showModal}
        onClose={closeModal}
        onConfirm={() => void confirmDailyClosing()}
        cashCount={cashCount}
        onCashCountChange={setCashCount}
        notes={notes}
        onNotesChange={setNotes}
        error={modalError}
        isLoading={isLoading}
      />
    </>
  );
}

const styles = StyleSheet.create({
  warningBanner: {
    backgroundColor: '#fffbeb',
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#eab308',
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
  },
  titleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.xs,
    marginBottom: 4,
  },
  icon: {
    fontSize: 16,
    lineHeight: 20,
  },
  warningTitle: {
    fontWeight: '700',
    color: SoftColors.textPrimary,
    fontSize: 15,
    flexShrink: 1,
  },
  warningText: {
    color: SoftColors.textSecondary,
    fontSize: 13,
    marginBottom: SoftSpacing.sm,
  },
  countdown: {
    fontWeight: '700',
    fontSize: 22,
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.sm,
    letterSpacing: 1,
    fontFamily: 'monospace',
  },
  closeButton: {
    alignSelf: 'flex-start',
    backgroundColor: SoftColors.accent,
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: SoftRadius.md,
  },
  closeButtonText: {
    color: SoftColors.textInverse,
    fontWeight: '600',
    fontSize: 13,
  },
  pressed: {
    opacity: 0.85,
  },
});
