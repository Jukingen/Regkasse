'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Gun sonu admin sayfasi; metinler tagesabschluss namespace, para/tarih formatLocale ile.
 */
import React, { useCallback, useMemo, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { Alert, Button, Card, Col, DatePicker, Descriptions, Empty, Form, Input, Row, Select, Skeleton, Space, Table, Tag, Typography } from 'antd';
import { CalendarOutlined, FilePdfOutlined, ReloadOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import dayjs, { type Dayjs } from 'dayjs';
import { CardSkeleton, TableSkeleton } from '@/components/Skeleton';

import {
  getGetApiTagesabschlussCanCloseCashRegisterIdQueryKey,
  useGetApiTagesabschlussCanCloseCashRegisterId,
  useGetApiTagesabschlussHistory,
  useGetApiTagesabschlussStatistics,
  usePostApiTagesabschlussDaily,
  usePostApiTagesabschlussMonthly,
  usePostApiTagesabschlussYearly,
} from '@/api/generated/tagesabschluss/tagesabschluss';
import type {
  TagesabschlussCanCloseResponse,
  TagesabschlussResult,
  TagesabschlussStatisticsResponse,
} from '@/api/generated/model';
import { SelectedCashRegisterCard } from '@/components/SelectedCashRegisterCard';
import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell, AdminPageScopeSummary } from '@/components/admin-layout/AdminPageShell';
import { BackendRawTextBlock } from '@/components/admin-layout/BackendRawTextBlock';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, formatNumber } from '@/i18n/formatting';
import { downloadClosingReportPdf } from '@/features/tagesabschluss/downloadClosingReportPdf';
import {
  downloadReportPdf,
  reportPdfTypeFromClosingType,
  triggerReportPdfBlobDownload,
} from '@/features/reports/api/reportPdfApi';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';
import { FA_QUICK_CASH_REGISTER_QUERY_PARAM } from '@/features/cash-registers/constants/quickSwitch';
import { formatViennaCalendarDate } from '@/shared/utils/viennaCalendar';

const { Title, Paragraph, Text } = Typography;
const { RangePicker } = DatePicker;

const EMPTY_REGISTER_ID = '00000000-0000-0000-0000-000000000000';

type ExtendedCanCloseResponse = TagesabschlussCanCloseResponse & {
  canCloseMonthly?: boolean;
  canCloseYearly?: boolean;
  lastMonthlyClosingDate?: string | null;
  lastYearlyClosingDate?: string | null;
  lastClosingPerformedAt?: string | null;
  lastMonthlyClosingPerformedAt?: string | null;
  lastYearlyClosingPerformedAt?: string | null;
};

function viennaTodayDayjs(): Dayjs {
  return dayjs(formatViennaCalendarDate());
}

const BACKDATED_REASON_OTHER = 'other' as const;
type BackdatedReasonPreset = 'forgot' | 'technical' | 'staff' | typeof BACKDATED_REASON_OTHER;

function formatClosingPerformedAt(
  performedAt: string | null | undefined,
  closingDate: string | null | undefined,
  formatLocale: string,
): string | null {
  const source = performedAt ?? closingDate;
  if (!source) return null;
  return formatDateTime(source, formatLocale, {
    dateStyle: 'short',
    timeStyle: performedAt ? 'short' : undefined,
  });
}

function isOperationalRegisterId(v: string | null | undefined): boolean {
  const s = v?.trim();
  if (!s || s === EMPTY_REGISTER_ID) return false;
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(s);
}

function formatRegisterDisplayName(register: AdminCashRegisterListItem | null | undefined): string | null {
  if (!register) return null;
  const number = register.registerNumber?.trim();
  const location = register.location?.trim();
  if (number && location) return `${number} — ${location}`;
  return number || location || null;
}

export default function TagesabschlussPage() {
  const { message, modal } = useAntdApp();

  const queryClient = useQueryClient();
  const searchParams = useSearchParams();
  const { t, formatLocale } = useI18n();

  const closingTypeLabel = useCallback(
    (v: string | null | undefined) => {
      const s = v?.trim();
      if (!s) return FORMAT_EMPTY_DISPLAY;
      const m: Record<string, string> = {
        Daily: 'tagesabschluss.history.closingTypes.Daily',
        Monthly: 'tagesabschluss.history.closingTypes.Monthly',
        Yearly: 'tagesabschluss.history.closingTypes.Yearly',
      };
      const p = m[s];
      return p ? t(p) : s;
    },
    [t],
  );

  const closingRowStatusLabel = useCallback(
    (v: string | null | undefined) => {
      const s = v?.trim();
      if (!s) return FORMAT_EMPTY_DISPLAY;
      const m: Record<string, string> = {
        Completed: 'tagesabschluss.history.rowStatus.Completed',
        Failed: 'tagesabschluss.history.rowStatus.Failed',
        Pending: 'tagesabschluss.history.rowStatus.Pending',
      };
      const p = m[s];
      return p ? t(p) : s;
    },
    [t],
  );

  const historyFinanzOnlineStatusLabel = useCallback(
    (v: string | null | undefined) => {
      const s = v?.trim();
      if (!s) return FORMAT_EMPTY_DISPLAY;
      const m: Record<string, string> = {
        Submitted: 'tagesabschluss.history.finanzOnlineStatus.Submitted',
        Failed: 'tagesabschluss.history.finanzOnlineStatus.Failed',
        Pending: 'tagesabschluss.history.finanzOnlineStatus.Pending',
        NeedsReconciliation: 'tagesabschluss.history.finanzOnlineStatus.NeedsReconciliation',
      };
      const p = m[s];
      return p ? t(p) : s;
    },
    [t],
  );

  const { hasPermission } = usePermissions();
  const canView = hasPermission(PERMISSIONS.DAILY_CLOSING_VIEW);
  const canExecute = hasPermission(PERMISSIONS.DAILY_CLOSING_EXECUTE);
  const canDownloadPdf = hasPermission(PERMISSIONS.REPORT_VIEW);

  const queryRegisterId = searchParams.get(FA_QUICK_CASH_REGISTER_QUERY_PARAM)?.trim();
  const [selectedRegisterId, setSelectedRegisterId] = useState<string | undefined>(() =>
    isOperationalRegisterId(queryRegisterId) ? queryRegisterId : undefined,
  );

  const {
    selectedRegister,
    selectedRegisterId: resolvedRegisterId,
    setSelectedRegisterId: updateRegisterSelection,
    registerOptions,
    registers,
    isLoading: registersLoading,
    error: registersError,
    hasRegisters,
    isSingleRegister,
    hasMultipleRegisters,
  } = useCashRegisterSelection({
    value: selectedRegisterId,
    onChange: (next) => setSelectedRegisterId(next),
    controlled: true,
    autoSelect: true,
    persistSelection: true,
  });

  const [range, setRange] = useState<[Dayjs, Dayjs]>([
    dayjs().subtract(30, 'day'),
    dayjs(),
  ]);
  const [closingDay, setClosingDay] = useState<Dayjs>(() => viennaTodayDayjs());
  const [backdatedReasonPreset, setBackdatedReasonPreset] = useState<BackdatedReasonPreset | undefined>(
    undefined,
  );
  const [customBackdatedReason, setCustomBackdatedReason] = useState('');
  const viennaToday = useMemo(() => viennaTodayDayjs(), []);
  const isBackdatedClosing = closingDay.isBefore(viennaToday, 'day');
  const closingDayLabel = formatDateTime(closingDay.startOf('day').toDate(), formatLocale, {
    dateStyle: 'short',
  });

  const backdatedReasonOptions = useMemo(
    () =>
      (
        [
          { value: 'forgot' as const, labelKey: 'tagesabschluss.backdated.reasons.forgot' },
          { value: 'technical' as const, labelKey: 'tagesabschluss.backdated.reasons.technical' },
          { value: 'staff' as const, labelKey: 'tagesabschluss.backdated.reasons.staff' },
          { value: BACKDATED_REASON_OTHER, labelKey: 'tagesabschluss.backdated.reasons.other' },
        ] as const
      ).map((o) => ({ value: o.value, label: t(o.labelKey) })),
    [t],
  );

  const resolvedBackdatedReason = useMemo(() => {
    if (!isBackdatedClosing || !backdatedReasonPreset) return null;
    if (backdatedReasonPreset === BACKDATED_REASON_OTHER) {
      const custom = customBackdatedReason.trim();
      return custom.length >= 10 ? custom : null;
    }
    return t(`tagesabschluss.backdated.reasons.${backdatedReasonPreset}`);
  }, [backdatedReasonPreset, customBackdatedReason, isBackdatedClosing, t]);

  const backdatedReasonReady = !isBackdatedClosing || resolvedBackdatedReason != null;

  const effectiveRegisterId = resolvedRegisterId?.trim() ?? '';
  const registerIdValid = isOperationalRegisterId(effectiveRegisterId);
  const registerDisplayName = formatRegisterDisplayName(selectedRegister);
  const canCloseParams = useMemo(
    () => ({ closingDate: closingDay.format('YYYY-MM-DD') }),
    [closingDay],
  );

  const registerSelectionHint = useMemo(() => {
    if (registersLoading) return t('tagesabschluss.scope.registerLoading');
    if (!hasRegisters) return t('tagesabschluss.scope.noRegisters');
    if (!registerIdValid) {
      return hasMultipleRegisters
        ? t('tagesabschluss.scope.registerSelectPrompt')
        : t('tagesabschluss.scope.registerNotSelected');
    }
    if (registerDisplayName) {
      return t('tagesabschluss.scope.registerWithName', { name: registerDisplayName });
    }
    return t('tagesabschluss.scope.registerSelected');
  }, [
    hasMultipleRegisters,
    hasRegisters,
    registerDisplayName,
    registerIdValid,
    registersLoading,
    t,
  ]);

  const dataBlockedHint = useMemo(() => {
    if (!hasRegisters) return t('tagesabschluss.register.noRegistersTitle');
    if (!registerIdValid) {
      return hasMultipleRegisters
        ? t('tagesabschluss.register.selectRequired')
        : t('tagesabschluss.register.noSelection');
    }
    return null;
  }, [hasMultipleRegisters, hasRegisters, registerIdValid, t]);

  const historyParams = useMemo(() => {
    const base = {
      fromDate: range[0].format('YYYY-MM-DD'),
      toDate: range[1].format('YYYY-MM-DD'),
    };
    if (registerIdValid) {
      return { ...base, cashRegisterId: effectiveRegisterId };
    }
    return base;
  }, [range, effectiveRegisterId, registerIdValid]);

  const statsParams = historyParams;

  const historyQuery = useGetApiTagesabschlussHistory(historyParams, {
    query: { enabled: registerIdValid },
  });
  const historyRows: TagesabschlussResult[] = historyQuery.data ?? [];

  const statsQuery = useGetApiTagesabschlussStatistics(statsParams, {
    query: { enabled: registerIdValid },
  });
  const stats: TagesabschlussStatisticsResponse | undefined = statsQuery.data;

  const canCloseQuery = useGetApiTagesabschlussCanCloseCashRegisterId(effectiveRegisterId, canCloseParams, {
    query: { enabled: registerIdValid },
  });
  const canClose: ExtendedCanCloseResponse | undefined = canCloseQuery.data;
  const canCloseMonthly = canClose?.canCloseMonthly === true;
  const canCloseYearly = canClose?.canCloseYearly === true;

  const downloadPdf = useCallback(
    async (reportType: string, reportId: string, fileNameBase?: string) => {
      const id = reportId.trim();
      if (!id) {
        message.warning(t('tagesabschluss.messages.pdfUnavailable'));
        return;
      }

      const messageKey = 'tagesabschluss-history-pdf';
      message.loading({ content: t('reporting.storedPdf.loading'), key: messageKey });
      try {
        const lang = (formatLocale ?? 'de').split('-')[0] || 'de';
        const blob = await downloadReportPdf(reportType, id, { language: lang });
        triggerReportPdfBlobDownload(blob, fileNameBase ?? `${reportType}_${id}`);
        message.success({ content: t('reporting.storedPdf.success'), key: messageKey });
      } catch (error) {
        message.destroy(messageKey);
        openApiErrorMessage(message.open, t, error, {
          logContext: 'TagesabschlussPage.downloadPdf',
          fallbackKey: 'tagesabschluss.errors.pdfDownload',
        });
      }
    },
    [formatLocale, message, t],
  );

  const invalidateTagesabschluss = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: ['/api/Tagesabschluss/history'] });
    await queryClient.invalidateQueries({ queryKey: ['/api/Tagesabschluss/statistics'] });
    if (registerIdValid) {
      await queryClient.invalidateQueries({
        queryKey: getGetApiTagesabschlussCanCloseCashRegisterIdQueryKey(effectiveRegisterId),
      });
    }
  }, [queryClient, registerIdValid, effectiveRegisterId]);

  const dailyMu = usePostApiTagesabschlussDaily({
    mutation: {
      onSuccess: async (result) => {
        const dateLabel = formatDateTime(
          (result?.closingDate ? dayjs(result.closingDate) : closingDay).startOf('day').toDate(),
          formatLocale,
          { dateStyle: 'short' },
        );
        message.success(
          result?.isBackdated
            ? t('tagesabschluss.messages.successDailyBackdated', { date: dateLabel })
            : t('tagesabschluss.messages.successDaily'),
        );
        await invalidateTagesabschluss();
        const closingId = result?.closingId?.trim();
        if (closingId) {
          try {
            await downloadClosingReportPdf(closingId, {
              language: formatLocale,
              closingType: 'Daily',
            });
          } catch {
            message.warning(t('tagesabschluss.messages.pdfAutoDownloadFailed'));
          }
        }
      },
      onError: (e) =>
        message.error(
          getUserFacingApiErrorMessage(t, e, {
            logContext: 'TagesabschlussDaily',
            fallbackKey: 'tagesabschluss.errors.unknown',
          }),
        ),
    },
  });
  const monthlyMu = usePostApiTagesabschlussMonthly({
    mutation: {
      onSuccess: async (result) => {
        message.success(t('tagesabschluss.messages.successMonthly'));
        await invalidateTagesabschluss();
        const closingId = result?.closingId?.trim();
        if (closingId) {
          try {
            await downloadClosingReportPdf(closingId, {
              language: formatLocale,
              closingType: 'Monthly',
            });
          } catch {
            message.warning(t('tagesabschluss.messages.pdfAutoDownloadFailed'));
          }
        }
      },
      onError: (e) =>
        message.error(
          getUserFacingApiErrorMessage(t, e, {
            logContext: 'TagesabschlussMonthly',
            fallbackKey: 'tagesabschluss.errors.unknown',
          }),
        ),
    },
  });
  const yearlyMu = usePostApiTagesabschlussYearly({
    mutation: {
      onSuccess: async (result) => {
        message.success(t('tagesabschluss.messages.successYearly'));
        await invalidateTagesabschluss();
        const closingId = result?.closingId?.trim();
        if (closingId) {
          try {
            await downloadClosingReportPdf(closingId, {
              language: formatLocale,
              closingType: 'Yearly',
            });
          } catch {
            message.warning(t('tagesabschluss.messages.pdfAutoDownloadFailed'));
          }
        }
      },
      onError: (e) =>
        message.error(
          getUserFacingApiErrorMessage(t, e, {
            logContext: 'TagesabschlussYearly',
            fallbackKey: 'tagesabschluss.errors.unknown',
          }),
        ),
    },
  });

  const runClosing = (kind: 'daily' | 'monthly' | 'yearly') => {
    if (!canExecute) {
      return;
    }
    if (!registerIdValid) {
      message.warning(t('tagesabschluss.messages.warningNoRegister'));
      return;
    }
    if (kind === 'daily' && isBackdatedClosing && !resolvedBackdatedReason) {
      message.warning(t('tagesabschluss.backdated.reasonRequired'));
      return;
    }
    if (kind === 'daily' && canClose && !canClose.canClose) {
      const dateTime = formatClosingPerformedAt(
        canClose.lastClosingPerformedAt,
        canClose.lastClosingDate,
        formatLocale,
      );
      message.warning(
        dateTime && (canClose.paymentsWithoutInvoiceCount ?? 0) === 0
          ? t('tagesabschluss.messages.warningDailyAlreadyClosed', { dateTime })
          : t('tagesabschluss.messages.warningDailyNotAllowed'),
      );
      return;
    }
    if (kind === 'monthly' && !canCloseMonthly) {
      const dateTime = formatClosingPerformedAt(
        canClose?.lastMonthlyClosingPerformedAt,
        canClose?.lastMonthlyClosingDate,
        formatLocale,
      );
      message.warning(
        dateTime && (canClose?.paymentsWithoutInvoiceCount ?? 0) === 0
          ? t('tagesabschluss.messages.warningMonthlyAlreadyClosed', { dateTime })
          : t('tagesabschluss.messages.warningMonthlyNotAllowed'),
      );
      return;
    }
    if (kind === 'yearly' && !canCloseYearly) {
      const dateTime = formatClosingPerformedAt(
        canClose?.lastYearlyClosingPerformedAt,
        canClose?.lastYearlyClosingDate,
        formatLocale,
      );
      message.warning(
        dateTime && (canClose?.paymentsWithoutInvoiceCount ?? 0) === 0
          ? t('tagesabschluss.messages.warningYearlyAlreadyClosed', { dateTime })
          : t('tagesabschluss.messages.warningYearlyNotAllowed'),
      );
      return;
    }
    const modalTitle =
      kind === 'daily'
        ? isBackdatedClosing
          ? t('tagesabschluss.actions.modalTitleDailyBackdated', { date: closingDayLabel })
          : t('tagesabschluss.actions.modalTitleDaily')
        : kind === 'monthly'
          ? t('tagesabschluss.actions.modalTitleMonthly')
          : t('tagesabschluss.actions.modalTitleYearly');
    modal.confirm({
      title: modalTitle,
      content:
        kind === 'daily' && isBackdatedClosing
          ? t('tagesabschluss.actions.modalContentBackdated', { date: closingDayLabel })
          : t('tagesabschluss.actions.modalContent'),
      okText: t('tagesabschluss.actions.modalOk'),
      cancelText: t('tagesabschluss.actions.modalCancel'),
      okButtonProps: { danger: kind !== 'daily' },
      onOk: async () => {
        if (kind === 'daily') {
          await dailyMu.mutateAsync({
            data: {
              cashRegisterId: effectiveRegisterId,
              closingDate: closingDay.format('YYYY-MM-DD'),
              reason: isBackdatedClosing ? resolvedBackdatedReason : undefined,
            },
          });
          return;
        }
        const body = { data: { cashRegisterId: effectiveRegisterId } };
        if (kind === 'monthly') await monthlyMu.mutateAsync(body);
        else await yearlyMu.mutateAsync(body);
      },
    });
  };

  const historyColumns = useMemo(
    () => [
      {
        title: t('tagesabschluss.history.colDate'),
        dataIndex: 'closingDate',
        key: 'closingDate',
        width: 220,
        render: (d: string, row: TagesabschlussResult) => {
          if (!d) return FORMAT_EMPTY_DISPLAY;
          return (
            <Space size={4} wrap>
              <span>{formatDateTime(d, formatLocale)}</span>
              {row.isBackdated ? (
                <Tag color="orange" variant="filled">
                  {t('tagesabschluss.messages.historyBackdatedTag')}
                </Tag>
              ) : null}
            </Space>
          );
        },
      },
      {
        title: t('tagesabschluss.history.colCreatedAt'),
        dataIndex: 'createdAt',
        key: 'createdAt',
        width: 168,
        render: (d: string | null | undefined, row: TagesabschlussResult) => {
          if (!d) return FORMAT_EMPTY_DISPLAY;
          return (
            <span title={row.isBackdated ? t('tagesabschluss.history.createdAtBackdatedHint') : undefined}>
              {formatDateTime(d, formatLocale)}
            </span>
          );
        },
      },
      {
        title: t('tagesabschluss.history.colType'),
        dataIndex: 'closingType',
        key: 'closingType',
        width: 100,
        render: (v: string | null | undefined) => closingTypeLabel(v),
      },
      {
        title: t('tagesabschluss.history.colLateReason'),
        dataIndex: 'lateCreationReason',
        key: 'lateCreationReason',
        ellipsis: true,
        width: 220,
        render: (v: string | null | undefined, row: TagesabschlussResult) => {
          if (!row.isBackdated) return FORMAT_EMPTY_DISPLAY;
          const reason = v?.trim();
          return reason && reason.length > 0 ? reason : FORMAT_EMPTY_DISPLAY;
        },
      },
      {
        title: t('tagesabschluss.history.colGross'),
        dataIndex: 'totalAmount',
        key: 'totalAmount',
        render: (v: number) => formatCurrency(v ?? 0, formatLocale),
      },
      {
        title: t('tagesabschluss.history.colTax'),
        dataIndex: 'totalTaxAmount',
        key: 'totalTaxAmount',
        render: (v: number) => formatCurrency(v ?? 0, formatLocale),
      },
      {
        title: t('tagesabschluss.history.colTransactions'),
        dataIndex: 'transactionCount',
        key: 'transactionCount',
        width: 100,
        render: (v: number) => formatNumber(v ?? 0, formatLocale, { maximumFractionDigits: 0 }),
      },
      {
        title: t('tagesabschluss.history.colStatus'),
        dataIndex: 'status',
        key: 'status',
        width: 120,
        render: (v: string | null | undefined) => closingRowStatusLabel(v),
      },
      {
        title: t('tagesabschluss.history.colFinanzOnline'),
        dataIndex: 'finanzOnlineStatus',
        key: 'fo',
        width: 140,
        render: (v: string | null | undefined) => historyFinanzOnlineStatusLabel(v),
      },
      {
        title: t('tagesabschluss.history.colPdf'),
        key: 'pdf',
        width: 90,
        render: (_: unknown, row: TagesabschlussResult) => {
          const closingId = row.closingId?.trim();
          if (!closingId || !canDownloadPdf) {
            return FORMAT_EMPTY_DISPLAY;
          }

          const reportType = reportPdfTypeFromClosingType(row.closingType);
          return (
            <Button
              type="link"
              size="small"
              icon={<FilePdfOutlined />}
              onClick={() => void downloadPdf(reportType, closingId, `${reportType}_${closingId}`)}
            >
              {t('reporting.storedPdf.button')}
            </Button>
          );
        },
      },
    ],
    [
      t,
      formatLocale,
      closingTypeLabel,
      closingRowStatusLabel,
      historyFinanzOnlineStatusLabel,
      canDownloadPdf,
      downloadPdf,
    ],
  );

  const closingBusy = dailyMu.isPending || monthlyMu.isPending || yearlyMu.isPending;

  const tagesabschlussScopeSummary = useMemo(() => {
    const fromStr = formatDateTime(range[0].startOf('day').toDate(), formatLocale, { dateStyle: 'short' });
    const toStr = formatDateTime(range[1].startOf('day').toDate(), formatLocale, { dateStyle: 'short' });
    const period = t('tagesabschluss.scope.historyPeriod', { from: fromStr, to: toStr });
    const hist = t('tagesabschluss.scope.historyCount', { count: historyRows.length });
    return `${registerSelectionHint} · ${period} · ${hist}`;
  }, [registerSelectionHint, range, historyRows.length, t, formatLocale]);

  const registerPicker = (() => {
    if (registersLoading) {
      return <Skeleton.Input active size="small" style={{ width: 240 }} />;
    }
    if (registersError) {
      return (
        <Alert
          type="error"
          showIcon
          title={t('cashRegisters.selector.loadErrorTitle')}
        />
      );
    }
    if (!hasRegisters) {
      return (
        <Alert
          type="warning"
          showIcon
          title={t('tagesabschluss.register.noRegistersTitle')}
        />
      );
    }
    if (isSingleRegister && registers[0]) {
      return (
        <Space orientation="vertical" size={8} style={{ width: '100%' }}>
          <Text strong>{t('tagesabschluss.register.fromList')}</Text>
          <SelectedCashRegisterCard register={registers[0]} showAutoSelectedTag />
        </Space>
      );
    }
    return (
      <Space orientation="vertical" size={8} style={{ width: '100%' }}>
        {selectedRegister ? (
          <SelectedCashRegisterCard register={selectedRegister} showAutoSelectedTag={false} />
        ) : null}
        <Form.Item label={t('tagesabschluss.register.fromList')} required style={{ marginBottom: 0 }}>
          <Select
            style={{ minWidth: 240, maxWidth: 420 }}
            value={resolvedRegisterId}
            onChange={(next) => updateRegisterSelection(next)}
            placeholder={t('tagesabschluss.register.selectPlaceholder')}
            options={registerOptions}
          />
        </Form.Item>
      </Space>
    );
  })();

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('nav.tagesabschluss')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('nav.tagesabschluss') }]}
        actions={
          <Button
            icon={<ReloadOutlined />}
            onClick={() => {
              void historyQuery.refetch();
              void statsQuery.refetch();
              if (registerIdValid) void canCloseQuery.refetch();
            }}
            disabled={!registerIdValid}
          >
            {t('tagesabschluss.toolbar.refresh')}
          </Button>
        }
      >
        <Paragraph type="secondary" style={{ marginBottom: 0 }}>
          <CalendarOutlined style={{ marginRight: 8 }} />
          {t('tagesabschluss.page.intro')}
        </Paragraph>
      </AdminPageHeader>

      <AdminPageScopeSummary label={t('tagesabschluss.scope.label')}>{tagesabschlussScopeSummary}</AdminPageScopeSummary>

      <Card title={t('tagesabschluss.card.register')}>
        <Space orientation="vertical" style={{ width: '100%' }} size="middle">
          {registerPicker}

          {!registerIdValid ? null : canCloseQuery.isLoading ? (
            <CardSkeleton count={1} loading />
          ) : canCloseQuery.isError ? (
            <Alert
              type="error"
              title={t('tagesabschluss.check.failedTitle')}
              description={getUserFacingApiErrorMessage(t, canCloseQuery.error, {
                logContext: 'TagesabschlussCanClose',
                fallbackKey: 'tagesabschluss.errors.unknown',
                skipLog: true,
              })}
            />
          ) : (
            <Descriptions bordered size="small" column={1}>
              <Descriptions.Item label={t('tagesabschluss.check.canCloseLabel')}>
                {canClose?.canClose ? (
                  <Text type="success">{t('tagesabschluss.check.yes')}</Text>
                ) : (
                  <Text type="danger">{t('tagesabschluss.check.no')}</Text>
                )}
              </Descriptions.Item>
              <Descriptions.Item label={t('tagesabschluss.check.canCloseMonthlyLabel')}>
                {canCloseMonthly ? (
                  <Text type="success">{t('tagesabschluss.check.yes')}</Text>
                ) : (
                  <Text type="danger">{t('tagesabschluss.check.no')}</Text>
                )}
              </Descriptions.Item>
              <Descriptions.Item label={t('tagesabschluss.check.canCloseYearlyLabel')}>
                {canCloseYearly ? (
                  <Text type="success">{t('tagesabschluss.check.yes')}</Text>
                ) : (
                  <Text type="danger">{t('tagesabschluss.check.no')}</Text>
                )}
              </Descriptions.Item>
              <Descriptions.Item label={t('tagesabschluss.check.lastClosing')}>
                {formatClosingPerformedAt(
                  canClose?.lastClosingPerformedAt,
                  canClose?.lastClosingDate,
                  formatLocale,
                ) ?? FORMAT_EMPTY_DISPLAY}
              </Descriptions.Item>
              <Descriptions.Item label={t('tagesabschluss.check.lastMonthlyClosing')}>
                {formatClosingPerformedAt(
                  canClose?.lastMonthlyClosingPerformedAt,
                  canClose?.lastMonthlyClosingDate,
                  formatLocale,
                ) ?? FORMAT_EMPTY_DISPLAY}
              </Descriptions.Item>
              <Descriptions.Item label={t('tagesabschluss.check.lastYearlyClosing')}>
                {formatClosingPerformedAt(
                  canClose?.lastYearlyClosingPerformedAt,
                  canClose?.lastYearlyClosingDate,
                  formatLocale,
                ) ?? FORMAT_EMPTY_DISPLAY}
              </Descriptions.Item>
              <Descriptions.Item label={t('tagesabschluss.check.paymentsWithoutInvoice')}>
                {formatNumber(canClose?.paymentsWithoutInvoiceCount ?? 0, formatLocale, { maximumFractionDigits: 0 })}
              </Descriptions.Item>
              <Descriptions.Item label={t('tagesabschluss.check.hint')}>
                {canClose?.message?.trim() ? (
                  <BackendRawTextBlock introKey="common.backend.serverHintIntro" body={canClose.message} />
                ) : (
                  FORMAT_EMPTY_DISPLAY
                )}
              </Descriptions.Item>
            </Descriptions>
          )}

          {canView && (
            <Space orientation="vertical" style={{ width: '100%' }} size="middle">
              <Alert
                type="info"
                showIcon
                title={t('tagesabschluss.backdated.infoTitle')}
                description={t('tagesabschluss.backdated.infoDescription')}
              />
              <Form.Item label={t('tagesabschluss.backdated.dateLabel')} style={{ marginBottom: 0 }}>
                <DatePicker
                  value={closingDay}
                  format={DAYJS_DATE_FORMAT}
                  allowClear={false}
                  disabledDate={(current) => !!current && current.isAfter(viennaToday, 'day')}
                  onChange={(next) => {
                    if (next) {
                      setClosingDay(next.startOf('day'));
                      if (!next.isBefore(viennaToday, 'day')) {
                        setBackdatedReasonPreset(undefined);
                        setCustomBackdatedReason('');
                      }
                    }
                  }}
                />
              </Form.Item>
              {isBackdatedClosing ? (
                <>
                  <Form.Item
                    label={t('tagesabschluss.backdated.reasonLabel')}
                    required
                    style={{ marginBottom: 0 }}
                    validateStatus={
                      backdatedReasonPreset === BACKDATED_REASON_OTHER &&
                      customBackdatedReason.trim().length > 0 &&
                      customBackdatedReason.trim().length < 10
                        ? 'error'
                        : undefined
                    }
                    help={
                      backdatedReasonPreset === BACKDATED_REASON_OTHER &&
                      customBackdatedReason.trim().length > 0 &&
                      customBackdatedReason.trim().length < 10
                        ? t('tagesabschluss.backdated.customReasonMin')
                        : undefined
                    }
                  >
                    <Select
                      style={{ minWidth: 280, maxWidth: 480 }}
                      placeholder={t('tagesabschluss.backdated.reasonPlaceholder')}
                      value={backdatedReasonPreset}
                      options={backdatedReasonOptions}
                      onChange={(next: BackdatedReasonPreset) => setBackdatedReasonPreset(next)}
                    />
                  </Form.Item>
                  {backdatedReasonPreset === BACKDATED_REASON_OTHER ? (
                    <Form.Item
                      label={t('tagesabschluss.backdated.customReasonLabel')}
                      required
                      style={{ marginBottom: 0 }}
                    >
                      <Input.TextArea
                        rows={2}
                        maxLength={450}
                        value={customBackdatedReason}
                        placeholder={t('tagesabschluss.backdated.customReasonPlaceholder')}
                        onChange={(e) => setCustomBackdatedReason(e.target.value)}
                      />
                    </Form.Item>
                  ) : null}
                  <Alert
                    type="warning"
                    showIcon
                    title={t('tagesabschluss.backdated.legalTitle')}
                    description={
                      <div>
                        <p style={{ marginBottom: 8 }}>
                          {t('tagesabschluss.backdated.legalLineDate', { date: closingDayLabel })}
                        </p>
                        <p style={{ marginBottom: 8 }}>{t('tagesabschluss.backdated.legalLineAudit')}</p>
                        <p style={{ marginBottom: 0 }}>
                          {t('tagesabschluss.backdated.legalLineInspection')}
                        </p>
                      </div>
                    }
                    action={
                      <Button
                        size="small"
                        type="primary"
                        loading={closingBusy}
                        disabled={
                          !registerIdValid || !canExecute || !canClose?.canClose || !backdatedReasonReady
                        }
                        onClick={() => runClosing('daily')}
                      >
                        {t('tagesabschluss.backdated.createAnyway')}
                      </Button>
                    }
                  />
                </>
              ) : null}
              <Space wrap>
                <Button
                  type="primary"
                  loading={closingBusy}
                  disabled={
                    !registerIdValid || !canExecute || !canClose?.canClose || !backdatedReasonReady
                  }
                  onClick={() => runClosing('daily')}
                >
                  {t('tagesabschluss.actions.daily')}
                </Button>
                <Button
                  loading={closingBusy}
                  disabled={!registerIdValid || !canExecute || !canCloseMonthly}
                  onClick={() => runClosing('monthly')}
                >
                  {t('tagesabschluss.actions.monthly')}
                </Button>
                <Button
                  danger
                  loading={closingBusy}
                  disabled={!registerIdValid || !canExecute || !canCloseYearly}
                  onClick={() => runClosing('yearly')}
                >
                  {t('tagesabschluss.actions.yearly')}
                </Button>
              </Space>
            </Space>
          )}
        </Space>
      </Card>

      <Card
        title={t('tagesabschluss.card.stats')}
        extra={
          <Space>
            <RangePicker format={DAYJS_DATE_FORMAT} value={range} onChange={(v) => v && v[0] && v[1] && setRange([v[0], v[1]])} />
            <Button
              icon={<ReloadOutlined />}
              onClick={() => {
                void historyQuery.refetch();
                void statsQuery.refetch();
                if (registerIdValid) void canCloseQuery.refetch();
              }}
              disabled={!registerIdValid}
            >
              {t('tagesabschluss.stats.refresh')}
            </Button>
          </Space>
        }
      >
        <Row gutter={[16, 16]}>
          <Col xs={24} lg={12}>
            <Title level={5}>{t('tagesabschluss.stats.sectionTitle')}</Title>
            {dataBlockedHint ? (
              <Empty description={dataBlockedHint} />
            ) : statsQuery.isLoading ? (
              <CardSkeleton count={1} loading />
            ) : statsQuery.isError ? (
              <Alert
              type="error"
              title={t('tagesabschluss.errors.loadStatsTitle')}
              description={getUserFacingApiErrorMessage(t, statsQuery.error, {
                logContext: 'TagesabschlussStatistics',
                fallbackKey: 'tagesabschluss.errors.unknown',
                skipLog: true,
              })}
            />
            ) : /* No aggregate closings in range */ stats == null ||
              (stats.totalClosings === 0 && stats.totalAmount === 0) ? (
              <Empty description={t('tagesabschluss.stats.empty')} />
            ) : (
              <Descriptions bordered size="small" column={1}>
                <Descriptions.Item label={t('tagesabschluss.stats.labelClosings')}>
                  {formatNumber(stats.totalClosings, formatLocale, { maximumFractionDigits: 0 })}
                </Descriptions.Item>
                <Descriptions.Item label={t('tagesabschluss.stats.labelGrossSum')}>
                  {formatCurrency(stats.totalAmount, formatLocale)}
                </Descriptions.Item>
                <Descriptions.Item label={t('tagesabschluss.stats.labelTaxSum')}>
                  {formatCurrency(stats.totalTaxAmount, formatLocale)}
                </Descriptions.Item>
                <Descriptions.Item label={t('tagesabschluss.stats.labelTransactions')}>
                  {formatNumber(stats.totalTransactions, formatLocale, { maximumFractionDigits: 0 })}
                </Descriptions.Item>
                <Descriptions.Item label={t('tagesabschluss.stats.labelAvgDaily')}>
                  {formatCurrency(stats.averageDailyAmount, formatLocale)}
                </Descriptions.Item>
                <Descriptions.Item label={t('tagesabschluss.stats.labelLastInRange')}>
                  {stats.lastClosingDate
                    ? formatDateTime(stats.lastClosingDate, formatLocale)
                    : FORMAT_EMPTY_DISPLAY}
                </Descriptions.Item>
              </Descriptions>
            )}
          </Col>
          <Col xs={24} lg={24}>
            <Title level={5}>{t('tagesabschluss.history.sectionTitle')}</Title>
            {dataBlockedHint ? (
              <Empty description={dataBlockedHint} />
            ) : historyQuery.isLoading ? (
              <TableSkeleton rows={6} cols={5} loading />
            ) : historyQuery.isError ? (
              <Alert
              type="error"
              title={t('tagesabschluss.errors.loadHistoryTitle')}
              description={getUserFacingApiErrorMessage(t, historyQuery.error, {
                logContext: 'TagesabschlussHistory',
                fallbackKey: 'tagesabschluss.errors.unknown',
                skipLog: true,
              })}
            />
            ) : historyRows.length === 0 ? (
              <Empty description={t('tagesabschluss.history.empty')} />
            ) : (
              <Table
                rowKey={(r) => r.closingId ?? `${r.closingDate}-${r.closingType}`}
                size="small"
                pagination={{ pageSize: 10 }}
                columns={historyColumns}
                dataSource={historyRows}
              />
            )}
          </Col>
        </Row>
      </Card>
    </AdminPageShell>
  );
}
