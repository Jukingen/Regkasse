'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Gun sonu admin sayfasi; metinler tagesabschluss namespace, para/tarih formatLocale ile.
 */
import React, { useCallback, useMemo, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { Alert, Button, Card, Col, DatePicker, Descriptions, Empty, Form, Row, Select, Skeleton, Space, Spin, Table, Tag, Typography } from 'antd';
import { CalendarOutlined, ReloadOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import dayjs, { type Dayjs } from 'dayjs';

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
import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell, AdminPageScopeSummary } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, formatNumber } from '@/i18n/formatting';
import { BackendRawTextBlock } from '@/components/admin-layout/BackendRawTextBlock';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';
import { FA_QUICK_CASH_REGISTER_QUERY_PARAM } from '@/features/cash-registers/constants/quickSwitch';

const { Title, Paragraph, Text } = Typography;
const { RangePicker } = DatePicker;

const EMPTY_REGISTER_ID = '00000000-0000-0000-0000-000000000000';

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

  const queryRegisterId = searchParams.get(FA_QUICK_CASH_REGISTER_QUERY_PARAM)?.trim();
  const [selectedRegisterId, setSelectedRegisterId] = useState<string | undefined>(() =>
    isOperationalRegisterId(queryRegisterId) ? queryRegisterId : undefined,
  );

  const {
    selectedRegister,
    selectedRegisterId: resolvedRegisterId,
    setSelectedRegisterId,
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

  const effectiveRegisterId = resolvedRegisterId?.trim() ?? '';
  const registerIdValid = isOperationalRegisterId(effectiveRegisterId);
  const registerDisplayName = formatRegisterDisplayName(selectedRegister);

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

  const canCloseQuery = useGetApiTagesabschlussCanCloseCashRegisterId(effectiveRegisterId, {
    query: { enabled: registerIdValid },
  });
  const canClose: TagesabschlussCanCloseResponse | undefined = canCloseQuery.data;

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
      onSuccess: async () => {
        message.success(t('tagesabschluss.messages.successDaily'));
        await invalidateTagesabschluss();
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
      onSuccess: async () => {
        message.success(t('tagesabschluss.messages.successMonthly'));
        await invalidateTagesabschluss();
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
      onSuccess: async () => {
        message.success(t('tagesabschluss.messages.successYearly'));
        await invalidateTagesabschluss();
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
    const modalTitle =
      kind === 'daily'
        ? t('tagesabschluss.actions.modalTitleDaily')
        : kind === 'monthly'
          ? t('tagesabschluss.actions.modalTitleMonthly')
          : t('tagesabschluss.actions.modalTitleYearly');
    modal.confirm({
      title: modalTitle,
      content: t('tagesabschluss.actions.modalContent'),
      okText: t('tagesabschluss.actions.modalOk'),
      cancelText: t('tagesabschluss.actions.modalCancel'),
      okButtonProps: { danger: kind !== 'daily' },
      onOk: async () => {
        const body = { data: { cashRegisterId: effectiveRegisterId } };
        if (kind === 'daily') await dailyMu.mutateAsync(body);
        else if (kind === 'monthly') await monthlyMu.mutateAsync(body);
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
        width: 200,
        render: (d: string) => (d ? formatDateTime(d, formatLocale) : FORMAT_EMPTY_DISPLAY),
      },
      {
        title: t('tagesabschluss.history.colType'),
        dataIndex: 'closingType',
        key: 'closingType',
        width: 100,
        render: (v: string | null | undefined) => closingTypeLabel(v),
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
    ],
    [t, formatLocale, closingTypeLabel, closingRowStatusLabel, historyFinanzOnlineStatusLabel]
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
      const register = registers[0];
      const label = formatRegisterDisplayName(register) ?? register.registerNumber;
      return (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
          <Text strong>{t('tagesabschluss.register.fromList')}:</Text>
          <Text>{label}</Text>
          <Tag color="blue" variant="filled">
            {t('cashRegisters.selector.autoSelectedTag')}
          </Tag>
        </div>
      );
    }
    return (
      <Form.Item label={t('tagesabschluss.register.fromList')} required style={{ marginBottom: 0 }}>
        <Select
          style={{ minWidth: 240 }}
          value={resolvedRegisterId}
          onChange={(next) => setSelectedRegisterId(next)}
          placeholder={t('tagesabschluss.register.selectPlaceholder')}
          options={registerOptions}
        />
      </Form.Item>
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
            <Spin />
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
              <Descriptions.Item label={t('tagesabschluss.check.lastClosing')}>
                {canClose?.lastClosingDate
                  ? formatDateTime(canClose.lastClosingDate, formatLocale)
                  : FORMAT_EMPTY_DISPLAY}
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
            <Space wrap>
              <Button
                type="primary"
                loading={closingBusy}
                disabled={!registerIdValid || !canExecute}
                onClick={() => runClosing('daily')}
              >
                {t('tagesabschluss.actions.daily')}
              </Button>
              <Button
                loading={closingBusy}
                disabled={!registerIdValid || !canExecute}
                onClick={() => runClosing('monthly')}
              >
                {t('tagesabschluss.actions.monthly')}
              </Button>
              <Button
                danger
                loading={closingBusy}
                disabled={!registerIdValid || !canExecute}
                onClick={() => runClosing('yearly')}
              >
                {t('tagesabschluss.actions.yearly')}
              </Button>
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
              <Spin />
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
              <Spin />
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
