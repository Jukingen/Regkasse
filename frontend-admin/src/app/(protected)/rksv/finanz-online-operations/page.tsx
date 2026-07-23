'use client';

import { CheckCircleOutlined, CloseCircleOutlined, ReloadOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  Descriptions,
  Input,
  Row,
  Space,
  Statistic,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import dayjs from 'dayjs';
import Link from 'next/link';
import React, { useMemo, useState } from 'react';

import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import {
  getApiFinanzOnlineConfig,
  getApiFinanzOnlineErrors,
  getApiFinanzOnlineHistoryInvoiceId,
  getApiFinanzOnlineStatus,
  postApiFinanzOnlineTestConnection,
} from '@/api/generated/finanz-online/finanz-online';
import { CardSkeleton, TableSkeleton } from '@/components/Skeleton';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { BackendRawTextBlock } from '@/components/admin-layout/BackendRawTextBlock';
import { useAntdApp } from '@/hooks/useAntdApp';
import { dateColumnRender } from '@/components/DateColumn';
import { useI18n } from '@/i18n';
import { ADMIN_NAV_GROUP_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';
import { OPERATOR_FO_OPERATIONS_PAGE_COPY } from '@/shared/operatorTruthCopy';

function connectionTagColor(
  data:
    | {
        isConnected?: boolean;
        finanzOnlineTransportsSimulated?: boolean;
        isAuthoritative?: boolean;
      }
    | undefined
): string {
  if (data?.finanzOnlineTransportsSimulated) return 'orange';
  if (data?.isAuthoritative === false) return 'gold';
  if (data?.isConnected === true) return 'green';
  if (data?.isConnected === false) return 'red';
  return 'default';
}

export default function FinanzOnlineOperationsPage() {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [invoiceId, setInvoiceId] = useState('');
  const [appliedInvoiceId, setAppliedInvoiceId] = useState('');

  const statusQuery = useQuery({
    queryKey: rksvAdminQueryKeys.finanzOnlineOps.status,
    queryFn: getApiFinanzOnlineStatus,
    staleTime: 15_000,
  });
  const configQuery = useQuery({
    queryKey: rksvAdminQueryKeys.finanzOnlineOps.config,
    queryFn: getApiFinanzOnlineConfig,
    staleTime: 60_000,
  });
  const errorsQuery = useQuery({
    queryKey: rksvAdminQueryKeys.finanzOnlineOps.errors,
    queryFn: getApiFinanzOnlineErrors,
    staleTime: 20_000,
  });
  const historyQuery = useQuery({
    queryKey: rksvAdminQueryKeys.finanzOnlineOps.history(appliedInvoiceId),
    queryFn: () => getApiFinanzOnlineHistoryInvoiceId(appliedInvoiceId),
    enabled: appliedInvoiceId.length > 0,
  });

  const testMutation = useMutation({
    mutationFn: () => postApiFinanzOnlineTestConnection(),
    onSuccess: (result) => {
      if (result.success)
        message.success(result.message || t('rksvHub.finanzOnlineOpsPage.testSuccess'));
      else message.warning(result.message || t('rksvHub.finanzOnlineOpsPage.testWarning'));
      queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnlineOps.base });
    },
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, {
        logContext: 'FinanzOnlineOperations.testConnection',
        fallbackKey: 'rksvHub.finanzOnlineOpsPage.testErrorFallback',
      }),
  });

  const errorsPayload = errorsQuery.data;
  const errors = errorsPayload?.items ?? [];
  const history = historyQuery.data ?? [];
  const testResult = testMutation.data;

  const canRunHistory = useMemo(() => appliedInvoiceId.trim().length > 0, [appliedInvoiceId]);

  return (
    <>
      <AdminPageHeader
        title={
          <Space align="center" wrap size="small">
            <span>{OPERATOR_FO_OPERATIONS_PAGE_COPY.pageTitle}</span>
            <Tooltip title={t('rksvHub.finanzOnlineOpsPage.headerDiagnosticBadgeTooltip')}>
              <Tag>{t('rksvHub.finanzOnlineOpsPage.headerDiagnosticBadge')}</Tag>
            </Tooltip>
          </Space>
        }
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t(ADMIN_NAV_GROUP_LABEL_KEYS.rksv), href: '/rksv' },
          { title: OPERATOR_FO_OPERATIONS_PAGE_COPY.breadcrumbTitle },
        ]}
        actions={
          <Button
            icon={<ReloadOutlined />}
            onClick={() =>
              queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnlineOps.base })
            }
          >
            {t('common.buttons.refresh')}
          </Button>
        }
      />

      <Alert
        type="warning"
        showIcon
        style={{ marginBottom: 12 }}
        title={t('rksvHub.finanzOnlineOpsPage.nonAuthoritativeBannerTitle')}
        description={t('rksvHub.finanzOnlineOpsPage.nonAuthoritativeBannerDescription')}
      />
      {statusQuery.data?.finanzOnlineTransportsSimulated ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          title={OPERATOR_FO_OPERATIONS_PAGE_COPY.simulatedTransportNote}
        />
      ) : null}

      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        {OPERATOR_FO_OPERATIONS_PAGE_COPY.introLead}{' '}
        <Link href="/rksv/finanz-online-queue">
          {OPERATOR_FO_OPERATIONS_PAGE_COPY.introAbgleichLinkLabel}
        </Link>
        .
      </Typography.Paragraph>

      <Row gutter={[16, 16]}>
        <Col xs={24} lg={12}>
          <Card title={t('rksvHub.finanzOnlineOpsPage.detailedStatusCard')} size="small">
            {statusQuery.isLoading ? (
              <CardSkeleton count={1} loading />
            ) : statusQuery.isError ? (
              <Alert
                type="error"
                title={t('rksvHub.finanzOnlineOpsPage.statusLoadFailed')}
                description={
                  <ApiErrorAlertDescription
                    t={t}
                    error={statusQuery.error}
                    logContext="FinanzOnlineOperations.statusQuery"
                    fallbackKey="rksvHub.finanzOnlineOpsPage.statusLoadFailed"
                  />
                }
              />
            ) : (
              <Descriptions bordered size="small" column={1}>
                <Descriptions.Item label={t('rksvHub.finanzOnlineOpsPage.connectionLabel')}>
                  <Tag
                    color={connectionTagColor(statusQuery.data)}
                    icon={
                      statusQuery.data?.finanzOnlineTransportsSimulated ? undefined : statusQuery
                          .data?.isAuthoritative === false ? undefined : statusQuery.data
                          ?.isConnected ? (
                        <CheckCircleOutlined />
                      ) : (
                        <CloseCircleOutlined />
                      )
                    }
                  >
                    {statusQuery.data?.finanzOnlineTransportsSimulated
                      ? t('rksvHub.finanzOnlineOpsPage.connectionSimulatedLabel')
                      : statusQuery.data?.isAuthoritative === false
                        ? t('rksvHub.finanzOnlineOpsPage.connectionPendingLabel')
                        : statusQuery.data?.isConnected
                          ? t('rksvHub.finanzOnlineOpsPage.connected')
                          : t('rksvHub.finanzOnlineOpsPage.disconnected')}
                  </Tag>
                </Descriptions.Item>
                <Descriptions.Item label={t('rksvHub.finanzOnlineOpsPage.apiVersionLabel')}>
                  {statusQuery.data?.apiVersion || '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('rksvHub.finanzOnlineOpsPage.lastSyncLabel')}>
                  {statusQuery.data?.lastSync || '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('rksvHub.finanzOnlineOpsPage.pendingInvoicesLabel')}>
                  {statusQuery.data?.pendingInvoices ?? 0}
                </Descriptions.Item>
                <Descriptions.Item label={t('rksvHub.finanzOnlineOpsPage.pendingReportsLabel')}>
                  {statusQuery.data?.pendingReports ?? 0}
                </Descriptions.Item>
                <Descriptions.Item label={t('rksvHub.finanzOnlineOpsPage.errorMessageLabel')}>
                  {statusQuery.data?.errorMessage || '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('rksvHub.finanzOnlineOpsPage.authoritativeLabel')}>
                  {statusQuery.data?.isAuthoritative === true
                    ? t('rksvHub.finanzOnlineOpsPage.yes')
                    : statusQuery.data?.isAuthoritative === false
                      ? t('rksvHub.finanzOnlineOpsPage.no')
                      : '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('rksvHub.finanzOnlineOpsPage.diagnosticWarningLabel')}>
                  {statusQuery.data?.diagnosticWarning || '—'}
                </Descriptions.Item>
                <Descriptions.Item
                  label={t('rksvHub.finanzOnlineOpsPage.transportDiagnosticsLabel')}
                >
                  <Typography.Text code copyable style={{ fontSize: 12 }}>
                    {statusQuery.data?.transportDiagnostics || '—'}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item
                  label={t('rksvHub.finanzOnlineOpsPage.enableRealTestSubmissionLabel')}
                >
                  {statusQuery.data?.enableRealTestSubmission
                    ? t('rksvHub.finanzOnlineOpsPage.yes')
                    : t('rksvHub.finanzOnlineOpsPage.no')}
                </Descriptions.Item>
                <Descriptions.Item label={t('rksvHub.finanzOnlineOpsPage.sessionProbeLabel')}>
                  {statusQuery.data?.sessionProbeSucceeded == null
                    ? '—'
                    : `${statusQuery.data.sessionProbeSucceeded ? 'OK' : 'fail'} · ${statusQuery.data.sessionProbeIntegrationMode ?? '—'} · ${statusQuery.data.sessionProbeTimestamp ?? '—'}`}
                </Descriptions.Item>
              </Descriptions>
            )}
          </Card>
        </Col>

        <Col xs={24} lg={12}>
          <Card
            title={t('rksvHub.finanzOnlineOpsPage.configCardTitle')}
            size="small"
            extra={
              <Button
                loading={testMutation.isPending}
                onClick={() => testMutation.mutate()}
                type="primary"
              >
                {t('rksvHub.finanzOnlineOpsPage.testConnectionButton')}
              </Button>
            }
          >
            {configQuery.isLoading ? (
              <CardSkeleton count={1} loading />
            ) : configQuery.isError ? (
              <Alert
                type="error"
                title={t('rksvHub.finanzOnlineOpsPage.configLoadFailed')}
                description={
                  <ApiErrorAlertDescription
                    t={t}
                    error={configQuery.error}
                    logContext="FinanzOnlineOperations.configQuery"
                    fallbackKey="rksvHub.finanzOnlineOpsPage.configLoadFailed"
                  />
                }
              />
            ) : (
              <Space orientation="vertical" style={{ width: '100%' }} size="middle">
                <Row gutter={[12, 12]}>
                  <Col span={12}>
                    <Statistic
                      title={t('rksvHub.finanzOnlineOpsPage.statEnabled')}
                      value={
                        configQuery.data?.isEnabled
                          ? t('rksvHub.finanzOnlineOpsPage.yes')
                          : t('rksvHub.finanzOnlineOpsPage.no')
                      }
                    />
                  </Col>
                  <Col span={12}>
                    <Statistic
                      title={t('rksvHub.finanzOnlineOpsPage.statAutoSubmit')}
                      value={
                        configQuery.data?.autoSubmit
                          ? t('rksvHub.finanzOnlineOpsPage.yes')
                          : t('rksvHub.finanzOnlineOpsPage.no')
                      }
                    />
                  </Col>
                  <Col span={12}>
                    <Statistic
                      title={t('rksvHub.finanzOnlineOpsPage.statSubmitInterval')}
                      value={configQuery.data?.submitInterval ?? 0}
                    />
                  </Col>
                  <Col span={12}>
                    <Statistic
                      title={t('rksvHub.finanzOnlineOpsPage.statRetryAttempts')}
                      value={configQuery.data?.retryAttempts ?? 0}
                    />
                  </Col>
                </Row>
                <Descriptions bordered size="small" column={1}>
                  <Descriptions.Item label={t('rksvHub.finanzOnlineOpsPage.apiUrlLabel')}>
                    {configQuery.data?.apiUrl || '—'}
                  </Descriptions.Item>
                  <Descriptions.Item label={t('rksvHub.finanzOnlineOpsPage.usernameLabel')}>
                    {configQuery.data?.username || '—'}
                  </Descriptions.Item>
                  <Descriptions.Item label={t('rksvHub.finanzOnlineOpsPage.validationLabel')}>
                    {configQuery.data?.enableValidation
                      ? t('rksvHub.finanzOnlineOpsPage.validationEnabled')
                      : t('rksvHub.finanzOnlineOpsPage.validationDisabled')}
                  </Descriptions.Item>
                </Descriptions>
                {testResult && (
                  <Alert
                    type={testResult.success ? 'success' : 'warning'}
                    title={
                      testResult.success
                        ? t('rksvHub.finanzOnlineOpsPage.testSuccess')
                        : t('rksvHub.finanzOnlineOpsPage.testWarning')
                    }
                    description={
                      <Space orientation="vertical" size={8} style={{ width: '100%' }}>
                        <Typography.Text>
                          {t('rksvHub.finanzOnlineOpsPage.responseTimeLine', {
                            ms: String(testResult.responseTime ?? ''),
                            timestamp: String(testResult.timestamp ?? ''),
                          })}
                        </Typography.Text>
                        {testResult.probeIntegrationMode ? (
                          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            Mode: {testResult.probeIntegrationMode}
                          </Typography.Text>
                        ) : null}
                        {testResult.transportDiagnostics ? (
                          <Typography.Text
                            code
                            copyable
                            style={{ fontSize: 11, display: 'block', whiteSpace: 'pre-wrap' }}
                          >
                            {testResult.transportDiagnostics}
                          </Typography.Text>
                        ) : null}
                        {testResult.diagnosticWarning ? (
                          <Typography.Text
                            type="warning"
                            style={{ fontSize: 12, display: 'block' }}
                          >
                            {testResult.diagnosticWarning}
                          </Typography.Text>
                        ) : null}
                        <BackendRawTextBlock
                          introKey="common.backend.serverHintIntro"
                          body={testResult.message}
                        />
                      </Space>
                    }
                    showIcon
                  />
                )}
              </Space>
            )}
          </Card>
        </Col>
      </Row>

      <Card
        title={t('rksvHub.finanzOnlineOpsPage.recentErrorsTitle', { count: errors.length })}
        size="small"
        style={{ marginTop: 16 }}
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12, fontSize: 12 }}>
          {t('rksvHub.finanzOnlineOpsPage.errorsTableHint')}
        </Typography.Paragraph>
        {errorsPayload?.diagnosticWarning ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            title={errorsPayload.diagnosticWarning}
          />
        ) : null}
        {errorsQuery.isLoading ? (
          <TableSkeleton rows={5} cols={4} loading />
        ) : errorsQuery.isError ? (
          <Alert
            type="error"
            title={t('rksvHub.finanzOnlineOpsPage.errorsLoadFailed')}
            description={
              <ApiErrorAlertDescription
                t={t}
                error={errorsQuery.error}
                logContext="FinanzOnlineOperations.errorsQuery"
                fallbackKey="rksvHub.finanzOnlineOpsPage.errorsLoadFailed"
              />
            }
          />
        ) : errors.length === 0 ? (
          <Alert type="info" title={t('rksvHub.finanzOnlineOpsPage.noErrorsFound')} />
        ) : (
          <Table
            size="small"
            pagination={{ pageSize: 5 }}
            rowKey={(r) => (r.id ? String(r.id) : `${r.code}-${r.timestamp}-${r.invoiceNumber}`)}
            dataSource={errors}
            columns={[
              {
                title: t('rksvHub.finanzOnlineOpsPage.colCode'),
                dataIndex: 'code',
                key: 'code',
                width: 120,
              },
              {
                title: t('rksvHub.finanzOnlineOpsPage.colMessage'),
                dataIndex: 'message',
                key: 'message',
              },
              {
                title: t('rksvHub.finanzOnlineOpsPage.colTimestamp'),
                dataIndex: 'timestamp',
                key: 'timestamp',
                width: 180,
              },
              {
                title: t('rksvHub.finanzOnlineOpsPage.colInvoice'),
                dataIndex: 'invoiceNumber',
                key: 'invoiceNumber',
                width: 180,
                render: (v: string) => v || '—',
              },
              {
                title: t('rksvHub.finanzOnlineOpsPage.colRetry'),
                dataIndex: 'retryCount',
                key: 'retryCount',
                width: 90,
              },
            ]}
          />
        )}
      </Card>

      <Card
        title={t('rksvHub.finanzOnlineOpsPage.historyCardTitle')}
        size="small"
        style={{ marginTop: 16 }}
        extra={
          <Space>
            <Input
              placeholder={t('rksvHub.finanzOnlineOpsPage.invoiceIdPlaceholder')}
              style={{ width: 280 }}
              value={invoiceId}
              onChange={(e) => setInvoiceId(e.target.value)}
            />
            <Button type="primary" onClick={() => setAppliedInvoiceId(invoiceId.trim())}>
              {t('rksvHub.finanzOnlineOpsPage.loadHistoryButton')}
            </Button>
          </Space>
        }
      >
        {!canRunHistory ? (
          <Alert type="info" title={t('rksvHub.finanzOnlineOpsPage.enterInvoiceIdHint')} />
        ) : historyQuery.isLoading ? (
          <TableSkeleton rows={5} cols={4} loading />
        ) : historyQuery.isError ? (
          <Alert
            type="error"
            title={t('rksvHub.finanzOnlineOpsPage.historyLoadFailed')}
            description={
              <ApiErrorAlertDescription
                t={t}
                error={historyQuery.error}
                logContext="FinanzOnlineOperations.historyQuery"
                fallbackKey="rksvHub.finanzOnlineOpsPage.historyLoadFailed"
              />
            }
          />
        ) : history.length === 0 ? (
          <Alert type="info" title={t('rksvHub.finanzOnlineOpsPage.historyEmpty')} />
        ) : (
          <Table
            size="small"
            dataSource={history}
            rowKey={(r) => `${r.id}-${r.submittedAt}`}
            pagination={{ pageSize: 10 }}
            columns={[
              {
                title: t('rksvHub.finanzOnlineOpsPage.colSubmittedAt'),
                dataIndex: 'submittedAt',
                key: 'submittedAt',
                width: 180,
                render: dateColumnRender('datetimeSeconds'),
              },
              {
                title: t('rksvHub.finanzOnlineOpsPage.colSuccess'),
                dataIndex: 'success',
                key: 'success',
                width: 90,
                render: (v: boolean) => (
                  <Tag color={v ? 'green' : 'red'}>
                    {v
                      ? t('rksvHub.finanzOnlineOpsPage.yesShort')
                      : t('rksvHub.finanzOnlineOpsPage.noShort')}
                  </Tag>
                ),
              },
              {
                title: t('rksvHub.finanzOnlineOpsPage.colHttp'),
                dataIndex: 'responseStatusCode',
                key: 'responseStatusCode',
                width: 80,
              },
              {
                title: t('rksvHub.finanzOnlineOpsPage.colError'),
                dataIndex: 'errorMessage',
                key: 'errorMessage',
                ellipsis: true,
              },
            ]}
          />
        )}
      </Card>
    </>
  );
}
