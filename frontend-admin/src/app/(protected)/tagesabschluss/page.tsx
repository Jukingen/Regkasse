'use client';

/**
 * Gun sonu admin sayfasi; metinler tagesabschluss namespace, para/tarih formatLocale ile.
 */
import React, { useCallback, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Descriptions,
  Empty,
  Input,
  Modal,
  Row,
  Select,
  Space,
  Spin,
  Table,
  Typography,
  message,
} from 'antd';
import { CalendarOutlined, ReloadOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs, { type Dayjs } from 'dayjs';

import { getApiCashRegister } from '@/api/generated/cash-register/cash-register';
import {
  getGetApiTagesabschlussCanCloseCashRegisterIdQueryKey,
  getGetApiTagesabschlussHistoryQueryKey,
  getGetApiTagesabschlussStatisticsQueryKey,
  useGetApiTagesabschlussCanCloseCashRegisterId,
  useGetApiTagesabschlussHistory,
  useGetApiTagesabschlussStatistics,
  usePostApiTagesabschlussDaily,
  usePostApiTagesabschlussMonthly,
  usePostApiTagesabschlussYearly,
} from '@/api/generated/tagesabschluss/tagesabschluss';
import {
  normalizeCashRegisterListBody,
} from '@/features/tagesabschluss/normalizers';
import type {
  TagesabschlussCanCloseResponse,
  TagesabschlussResult,
  TagesabschlussStatisticsResponse,
} from '@/api/generated/model';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell, AdminPageScopeSummary } from '@/components/admin-layout/AdminPageShell';
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, formatNumber } from '@/i18n/formatting';
import { BackendRawTextBlock } from '@/components/admin-layout/BackendRawTextBlock';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';

const { Title, Paragraph, Text } = Typography;
const { RangePicker } = DatePicker;

function isUuid(v: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
    v.trim()
  );
}

export default function TagesabschlussPage() {
  const queryClient = useQueryClient();
  const { t, formatLocale } = useI18n();

  const { hasPermission } = usePermissions();
  const canListRegisters = hasPermission(PERMISSIONS.CASHREGISTER_VIEW);

  const [range, setRange] = useState<[Dayjs, Dayjs]>([
    dayjs().subtract(30, 'day'),
    dayjs(),
  ]);
  const [selectedRegisterId, setSelectedRegisterId] = useState<string>('');
  const [manualRegisterId, setManualRegisterId] = useState<string>('');

  const historyParams = useMemo(
    () => ({
      fromDate: range[0].format('YYYY-MM-DD'),
      toDate: range[1].format('YYYY-MM-DD'),
    }),
    [range]
  );

  const statsParams = historyParams;

  const { data: registersRaw, isLoading: registersLoading } = useQuery({
    queryKey: ['admin', 'cashRegisters', 'list'],
    queryFn: async () => getApiCashRegister(),
    enabled: canListRegisters,
  });

  const registerOptions = useMemo(() => {
    const list = normalizeCashRegisterListBody(registersRaw);
    return list
      .filter((r) => r.id)
      .map((r) => ({
        value: r.id as string,
        label: `${r.registerNumber ?? r.id} — ${r.location ?? ''}`,
      }));
  }, [registersRaw]);

  const effectiveRegisterId = selectedRegisterId || manualRegisterId.trim();
  const registerIdValid = effectiveRegisterId.length > 0 && isUuid(effectiveRegisterId);

  const historyQuery = useGetApiTagesabschlussHistory(historyParams);
  const historyRows: TagesabschlussResult[] = historyQuery.data ?? [];

  const statsQuery = useGetApiTagesabschlussStatistics(statsParams);
  const stats: TagesabschlussStatisticsResponse | undefined = statsQuery.data;

  const canCloseQuery = useGetApiTagesabschlussCanCloseCashRegisterId(effectiveRegisterId, {
    query: { enabled: registerIdValid },
  });
  const canClose: TagesabschlussCanCloseResponse | undefined = canCloseQuery.data;

  const invalidateTagesabschluss = useCallback(async () => {
    await queryClient.invalidateQueries({
      queryKey: getGetApiTagesabschlussHistoryQueryKey(historyParams),
    });
    await queryClient.invalidateQueries({
      queryKey: getGetApiTagesabschlussStatisticsQueryKey(statsParams),
    });
    if (registerIdValid) {
      await queryClient.invalidateQueries({
        queryKey: getGetApiTagesabschlussCanCloseCashRegisterIdQueryKey(effectiveRegisterId),
      });
    }
  }, [queryClient, historyParams, statsParams, registerIdValid, effectiveRegisterId]);

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
    if (!registerIdValid) {
      message.warning(t('tagesabschluss.messages.warningUuid'));
      return;
    }
    const modalTitle =
      kind === 'daily'
        ? t('tagesabschluss.actions.modalTitleDaily')
        : kind === 'monthly'
          ? t('tagesabschluss.actions.modalTitleMonthly')
          : t('tagesabschluss.actions.modalTitleYearly');
    Modal.confirm({
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
      { title: t('tagesabschluss.history.colType'), dataIndex: 'closingType', key: 'closingType', width: 100 },
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
      { title: t('tagesabschluss.history.colStatus'), dataIndex: 'status', key: 'status', width: 120 },
      { title: t('tagesabschluss.history.colFinanzOnline'), dataIndex: 'finanzOnlineStatus', key: 'fo', width: 140 },
    ],
    [t, formatLocale]
  );

  const closingBusy = dailyMu.isPending || monthlyMu.isPending || yearlyMu.isPending;

  const tagesabschlussScopeSummary = useMemo(() => {
    const regPart = registerIdValid
      ? t('tagesabschluss.scope.registerWithId', { id: effectiveRegisterId })
      : t('tagesabschluss.scope.registerInvalid');
    const fromStr = formatDateTime(range[0].startOf('day').toDate(), formatLocale, { dateStyle: 'short' });
    const toStr = formatDateTime(range[1].startOf('day').toDate(), formatLocale, { dateStyle: 'short' });
    const period = t('tagesabschluss.scope.historyPeriod', { from: fromStr, to: toStr });
    const hist = t('tagesabschluss.scope.historyCount', { count: historyRows.length });
    return `${regPart} · ${period} · ${hist}`;
  }, [registerIdValid, effectiveRegisterId, range, historyRows.length, t, formatLocale]);

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={ADMIN_NAV_LABELS.tagesabschluss}
        breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: ADMIN_NAV_LABELS.tagesabschluss }]}
        actions={
          <Button
            icon={<ReloadOutlined />}
            onClick={() => {
              void historyQuery.refetch();
              void statsQuery.refetch();
              if (registerIdValid) void canCloseQuery.refetch();
            }}
          >
            {t('tagesabschluss.toolbar.refresh')}
          </Button>
        }
      >
        <Paragraph type="secondary" style={{ marginBottom: 0 }}>
          <CalendarOutlined style={{ marginRight: 8 }} />
          {t('tagesabschluss.page.intro')}{' '}
          <Text code>{t('tagesabschluss.page.introApiPath')}</Text> ({t('tagesabschluss.page.introPermission')}{' '}
          <Text code>tse.sign</Text>).
        </Paragraph>
      </AdminPageHeader>

      <AdminPageScopeSummary label={t('tagesabschluss.scope.label')}>{tagesabschlussScopeSummary}</AdminPageScopeSummary>

      <Card title={t('tagesabschluss.card.register')}>
        <Space direction="vertical" style={{ width: '100%' }} size="middle">
          {canListRegisters ? (
            <div>
              <Text type="secondary">{t('tagesabschluss.register.fromList')}</Text>
              <Select
                showSearch
                allowClear
                placeholder={t('tagesabschluss.register.selectPlaceholder')}
                style={{ width: '100%', marginTop: 6 }}
                options={registerOptions}
                loading={registersLoading}
                value={selectedRegisterId || undefined}
                onChange={(v) => setSelectedRegisterId(v ?? '')}
                optionFilterProp="label"
              />
            </div>
          ) : (
            <Alert
              type="info"
              showIcon
              message={t('tagesabschluss.register.noListTitle')}
              description={t('tagesabschluss.register.noListDescription')}
            />
          )}
          <div>
            <Text type="secondary">{t('tagesabschluss.register.manualLabel')}</Text>
            <Input
              style={{ marginTop: 6 }}
              placeholder={t('tagesabschluss.register.manualPlaceholder')}
              value={manualRegisterId}
              onChange={(e) => setManualRegisterId(e.target.value)}
            />
          </div>

          {!registerIdValid ? (
            <Text type="warning">{t('tagesabschluss.register.uuidWarning')}</Text>
          ) : canCloseQuery.isLoading ? (
            <Spin />
          ) : canCloseQuery.isError ? (
            <Alert
              type="error"
              message={t('tagesabschluss.check.failedTitle')}
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

          <Space wrap>
            <Button
              type="primary"
              loading={closingBusy}
              disabled={!registerIdValid}
              onClick={() => runClosing('daily')}
            >
              {t('tagesabschluss.actions.daily')}
            </Button>
            <Button loading={closingBusy} disabled={!registerIdValid} onClick={() => runClosing('monthly')}>
              {t('tagesabschluss.actions.monthly')}
            </Button>
            <Button
              danger
              loading={closingBusy}
              disabled={!registerIdValid}
              onClick={() => runClosing('yearly')}
            >
              {t('tagesabschluss.actions.yearly')}
            </Button>
          </Space>
        </Space>
      </Card>

      <Card
        title={t('tagesabschluss.card.stats')}
        extra={
          <Space>
            <RangePicker value={range} onChange={(v) => v && v[0] && v[1] && setRange([v[0], v[1]])} />
            <Button
              icon={<ReloadOutlined />}
              onClick={() => {
                void historyQuery.refetch();
                void statsQuery.refetch();
                if (registerIdValid) void canCloseQuery.refetch();
              }}
            >
              {t('tagesabschluss.stats.refresh')}
            </Button>
          </Space>
        }
      >
        <Row gutter={[16, 16]}>
          <Col xs={24} lg={12}>
            <Title level={5}>{t('tagesabschluss.stats.sectionTitle')}</Title>
            {statsQuery.isLoading ? (
              <Spin />
            ) : statsQuery.isError ? (
              <Alert
              type="error"
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
            {historyQuery.isLoading ? (
              <Spin />
            ) : historyQuery.isError ? (
              <Alert
              type="error"
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
