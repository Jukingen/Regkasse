'use client';

/**
 * Correlation-ID–centred incident investigation.
 * Single source of truth: GET /api/admin/incidents/{correlationId}.
 * The endpoint returns one aggregate payload (replay batch + audit + FO rows).
 */
import { SearchOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Collapse,
  Descriptions,
  Divider,
  Input,
  Space,
  Table,
  Tag,
  Timeline,
  Tooltip,
  Typography,
} from 'antd';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import React, { useCallback, useMemo, useState } from 'react';

import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import { getApiAdminIncidentsCorrelationId } from '@/api/generated/admin/admin';
import type {
  AuditLogEntryDto,
  FinanzOnlineReconciliationItemDto,
  ReplayBatchPaymentItemDto,
} from '@/api/generated/model';
import { CardSkeleton } from '@/components/Skeleton';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { AdminTruthBadge } from '@/shared/adminTruthBadges';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import {
  finanzOnlineRetryUiPresentation,
  getFinanzOnlineRetryUiState,
} from '@/shared/foReconciliationRowTriage';
import { buildFinanzOnlineQueueInvestigationHref } from '@/shared/investigationNavigation';
import {
  OperatorBusinessSection,
  OperatorSummaryStrip,
  OperatorTechnicalSection,
} from '@/shared/operatorTriageLayout';
import {
  OPERATOR_FO_SUMMARY_SCREEN_COPY,
  OPERATOR_INCIDENT_COPY,
  OPERATOR_INVESTIGATION_CONTEXT_COPY,
  OPERATOR_LINK_LABELS,
} from '@/shared/operatorTruthCopy';
import { analyzeRegisterFkField } from '@/shared/utils/registerIdentity';
import { viewAuditLogStatusPresentation } from '@/shared/verificationsAuditView';

const RKSV_HANDOFF_PREFIX = 'RKSV_HANDOFF_V1:';

function normalizeCorrelationId(id: string): string {
  const cleaned = id.replace(/-/g, '').trim();
  if (cleaned.length !== 32) return id;
  return `${cleaned.slice(0, 8)}-${cleaned.slice(8, 12)}-${cleaned.slice(12, 16)}-${cleaned.slice(16, 20)}-${cleaned.slice(20, 32)}`;
}

function firstGuidLike(input: string): string | null {
  const match = input.match(
    /[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}/
  );
  return match?.[0] ?? null;
}

function resolveCorrelationInput(input: string): string {
  const raw = input.trim();
  if (!raw) return '';

  if (raw.startsWith(RKSV_HANDOFF_PREFIX)) {
    const jsonText = raw.slice(RKSV_HANDOFF_PREFIX.length).trim();
    try {
      const parsed = JSON.parse(jsonText) as { correlationId?: unknown };
      if (typeof parsed.correlationId === 'string' && parsed.correlationId.trim().length > 0) {
        return normalizeCorrelationId(parsed.correlationId.trim());
      }
    } catch {
      // ignore parse failure, continue with generic extraction
    }
  }

  try {
    if (raw.startsWith('http://') || raw.startsWith('https://')) {
      const url = new URL(raw);
      const qp = url.searchParams.get('correlationId');
      if (qp?.trim()) return normalizeCorrelationId(qp.trim());
    }
  } catch {
    // ignore URL parse failure
  }

  if (raw.startsWith('{') && raw.endsWith('}')) {
    try {
      const parsed = JSON.parse(raw) as {
        correlationId?: unknown;
        replayBatchCorrelationId?: unknown;
      };
      const fromCorrelation =
        typeof parsed.correlationId === 'string' ? parsed.correlationId : null;
      const fromReplayBatch =
        typeof parsed.replayBatchCorrelationId === 'string'
          ? parsed.replayBatchCorrelationId
          : null;
      if (fromCorrelation?.trim()) return normalizeCorrelationId(fromCorrelation.trim());
      if (fromReplayBatch?.trim()) return normalizeCorrelationId(fromReplayBatch.trim());
    } catch {
      // ignore parse failure
    }
  }

  const guid = firstGuidLike(raw);
  return guid ? normalizeCorrelationId(guid) : normalizeCorrelationId(raw);
}

function isPlainJsonObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** Reads optional replayPath / payloadRepaired from audit JSON blobs without asserting a full DTO. */
function parseReplayMeta(
  requestData?: string | null,
  responseData?: string | null
): {
  replayPath?: string;
  payloadRepaired?: boolean;
} {
  const out: { replayPath?: string; payloadRepaired?: boolean } = {};

  const mergeFromJsonText = (json: string | null | undefined) => {
    if (!json?.trim()) {
      return;
    }
    let parsed: unknown;
    try {
      parsed = JSON.parse(json);
    } catch {
      return;
    }
    if (!isPlainJsonObject(parsed)) {
      if (process.env.NODE_ENV === 'development') {
        technicalConsole.warn('[incident] parseReplayMeta: expected JSON object for replay meta');
      }
      return;
    }
    if (typeof parsed.replayPath === 'string') {
      out.replayPath = parsed.replayPath;
    }
    if (typeof parsed.payloadRepaired === 'boolean') {
      out.payloadRepaired = parsed.payloadRepaired;
    }
  };

  mergeFromJsonText(requestData);
  if (!out.replayPath) {
    mergeFromJsonText(responseData);
  }
  return out;
}

function formatIncidentShortTime(iso: string | null | undefined, formatLocale: string): string {
  if (!iso) return FORMAT_EMPTY_DISPLAY;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return FORMAT_EMPTY_DISPLAY;
  return formatDateTime(iso, formatLocale, {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  });
}

function formatAuditTimestamp(iso: string | null | undefined, formatLocale: string): string {
  if (!iso) return FORMAT_EMPTY_DISPLAY;
  return formatDateTime(iso, formatLocale, {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  });
}

function buildTimelineLabel(
  t: (key: string, options?: Record<string, string | number>) => string,
  action: string,
  description?: string | null,
  meta?: { replayPath?: string; payloadRepaired?: boolean }
): string {
  const parts: string[] = [];
  if (action) parts.push(action);
  if (meta?.replayPath)
    parts.push(t('rksvHub.incident.timelineReplayPath', { path: meta.replayPath }));
  if (meta?.payloadRepaired === true) parts.push(t('rksvHub.incident.timelinePayloadRepairedYes'));
  if (description && description.length < 120) parts.push(description);
  else if (description) parts.push(description.slice(0, 117) + '…');
  return parts.join(' · ') || FORMAT_EMPTY_DISPLAY;
}

export default function IncidentInvestigationPage() {
  const searchParams = useSearchParams();
  const { t, formatLocale } = useI18n();
  const ti = useCallback(
    (path: string, options?: Record<string, string | number>) =>
      t(`rksvHub.incident.${path}`, options),
    [t]
  );
  const backendApiTooltip = t('reporting.backend.apiStringsTooltip');

  const initialId = searchParams?.get('correlationId') ?? searchParams?.get('handoff') ?? '';
  const [inputId, setInputId] = useState(initialId);
  const [correlationId, setCorrelationId] = useState(resolveCorrelationInput(initialId));

  const normalizedId = correlationId.trim() ? normalizeCorrelationId(correlationId.trim()) : '';

  const {
    data: incident,
    isLoading: incidentLoading,
    error: incidentError,
  } = useQuery({
    queryKey: rksvAdminQueryKeys.incident(normalizedId),
    queryFn: () => getApiAdminIncidentsCorrelationId(normalizedId),
    enabled: !!normalizedId && normalizedId.length >= 32,
  });

  const batch = incident?.replayBatch;
  const hints = incident?.hints;

  const foByPayment = useMemo(() => {
    const map = new Map<string, FinanzOnlineReconciliationItemDto>();
    const rows = incident?.finanzOnlineReconciliation;
    if (!rows?.length) return map;
    for (const item of rows) {
      map.set(String(item.paymentId), item);
    }
    return map;
  }, [incident?.finanzOnlineReconciliation]);

  const auditLogs: AuditLogEntryDto[] = useMemo(
    () => incident?.auditLogs ?? [],
    [incident?.auditLogs]
  );
  const timelineItems = useMemo(() => {
    return auditLogs
      .sort((a, b) => new Date(a.timestamp ?? 0).getTime() - new Date(b.timestamp ?? 0).getTime())
      .map((log) => {
        const meta = parseReplayMeta(log.requestData, log.responseData);
        return {
          key: log.id ?? log.timestamp ?? 'unknown',
          timestamp: log.timestamp ?? '',
          action: log.action ?? '',
          status: log.status,
          description: log.description,
          meta,
          full: log,
        };
      });
  }, [auditLogs]);

  const onSearch = () => {
    const id = resolveCorrelationInput(inputId);
    if (id) setCorrelationId(id);
  };

  const isLoading = incidentLoading;
  const notFound = !incidentLoading && correlationId && !batch && !incidentError;
  const hasBatch = !!batch;

  const paymentColumns = useMemo(
    () => [
      {
        title: ti('colPayment'),
        key: 'payment',
        render: (_: unknown, r: ReplayBatchPaymentItemDto) => (
          <Link
            href={`/payments?paymentId=${r.paymentId}`}
            target="_blank"
            rel="noopener noreferrer"
          >
            <Typography.Text code>{String(r.paymentId).slice(0, 8)}…</Typography.Text>
          </Link>
        ),
      },
      {
        title: ti('colReceipt'),
        key: 'receipt',
        render: (_: unknown, r: ReplayBatchPaymentItemDto) =>
          r.receiptId ? (
            <Link href={`/receipts/${r.receiptId}`} target="_blank" rel="noopener noreferrer">
              {r.receiptNumber ?? r.receiptId}
            </Link>
          ) : (
            <Typography.Text type="secondary">{FORMAT_EMPTY_DISPLAY}</Typography.Text>
          ),
      },
      {
        title: (
          <Tooltip title={OPERATOR_INCIDENT_COPY.foStatusFromJoinTooltip}>
            <span>{ti('colFoStatus')}</span>
          </Tooltip>
        ),
        key: 'fo',
        width: 120,
        render: (_: unknown, r: ReplayBatchPaymentItemDto) => {
          const fo = foByPayment.get(String(r.paymentId));
          if (!fo)
            return <Typography.Text type="secondary">{FORMAT_EMPTY_DISPLAY}</Typography.Text>;
          const color =
            fo.finanzOnlineStatus === 'Submitted'
              ? 'green'
              : fo.finanzOnlineStatus === 'Failed'
                ? 'red'
                : 'orange';
          return (
            <Space orientation="vertical" size={2}>
              <Tag color={color} title={backendApiTooltip}>
                {fo.finanzOnlineStatus ?? FORMAT_EMPTY_DISPLAY}
              </Tag>
              {fo.finanzOnlineError ? (
                <Tooltip title={fo.finanzOnlineError}>
                  <Typography.Text type="secondary" style={{ fontSize: 11 }} ellipsis>
                    {fo.finanzOnlineError}
                  </Typography.Text>
                </Tooltip>
              ) : null}
              <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                <AdminTruthBadge kind="derived_from_foreign_row" /> {ti('viaPaymentIdJoin')}
              </Typography.Text>
            </Space>
          );
        },
      },
      {
        title: (
          <Tooltip title={OPERATOR_INCIDENT_COPY.foActionIncidentTooltip}>
            <span>{ti('colFoActionUi')}</span>
          </Tooltip>
        ),
        key: 'foRetryUi',
        width: 112,
        render: (_: unknown, r: ReplayBatchPaymentItemDto) => {
          const fo = foByPayment.get(String(r.paymentId));
          const ui = finanzOnlineRetryUiPresentation(
            getFinanzOnlineRetryUiState(fo?.finanzOnlineStatus)
          );
          return (
            <Tooltip title={ui.tooltip}>
              <Tag color={ui.tagColor}>{ui.tagLabel}</Tag>
            </Tooltip>
          );
        },
      },
      {
        title: (
          <Tooltip title={OPERATOR_INCIDENT_COPY.timesColumnIncidentTooltip}>
            <span>{ti('colTimes')}</span>
          </Tooltip>
        ),
        key: 'rowTiming',
        width: 152,
        render: (_: unknown, r: ReplayBatchPaymentItemDto) => {
          const fo = foByPayment.get(String(r.paymentId));
          return (
            <Space orientation="vertical" size={0}>
              <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                {ti('timeReplay')}{' '}
                {r.createdAtUtc
                  ? formatIncidentShortTime(r.createdAtUtc, formatLocale)
                  : FORMAT_EMPTY_DISPLAY}
              </Typography.Text>
              <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                {ti('timeFoRowCreated')}{' '}
                {fo?.createdAt
                  ? formatIncidentShortTime(fo.createdAt, formatLocale)
                  : FORMAT_EMPTY_DISPLAY}
              </Typography.Text>
              <Typography.Text style={{ fontSize: 11 }}>
                {ti('timeFoAttempt')}{' '}
                {fo?.finanzOnlineLastAttemptAtUtc
                  ? formatIncidentShortTime(fo.finanzOnlineLastAttemptAtUtc, formatLocale)
                  : FORMAT_EMPTY_DISPLAY}
              </Typography.Text>
              <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                {ti('timeRetryCount')} {fo?.finanzOnlineRetryCount ?? 0}
              </Typography.Text>
            </Space>
          );
        },
      },
      {
        title: (
          <Tooltip title={OPERATOR_INCIDENT_COPY.foRefColumnTooltip}>
            <span>{ti('colFoRef')}</span>
          </Tooltip>
        ),
        key: 'foRef',
        width: 100,
        ellipsis: true,
        render: (_: unknown, r: ReplayBatchPaymentItemDto) => {
          const fo = foByPayment.get(String(r.paymentId));
          const ref = fo?.finanzOnlineReferenceId?.trim();
          return ref ? (
            <Typography.Text code copyable ellipsis style={{ maxWidth: 92 }}>
              {ref}
            </Typography.Text>
          ) : (
            FORMAT_EMPTY_DISPLAY
          );
        },
      },
      {
        title: (
          <Tooltip title={OPERATOR_INCIDENT_COPY.registerFkColumnTooltip}>
            <span>{ti('colRegisterFk')}</span>
          </Tooltip>
        ),
        key: 'register',
        render: (_: unknown, r: ReplayBatchPaymentItemDto) => {
          const fo = foByPayment.get(String(r.paymentId));
          const reg = analyzeRegisterFkField(fo?.cashRegisterId);
          if (!fo) {
            return <Typography.Text type="secondary">{FORMAT_EMPTY_DISPLAY}</Typography.Text>;
          }
          if (!reg.rawTrimmed) {
            return (
              <Space orientation="vertical" size={2}>
                <Typography.Text type="secondary">{FORMAT_EMPTY_DISPLAY}</Typography.Text>
                <AdminTruthBadge kind="link_incomplete" />
              </Space>
            );
          }
          return (
            <Space orientation="vertical" size={4}>
              <Typography.Text code copyable={{ text: reg.rawTrimmed }}>
                {reg.linkSafeUuid ? `${reg.rawTrimmed.slice(0, 8)}…` : reg.rawTrimmed}
              </Typography.Text>
              <AdminTruthBadge kind="derived_from_foreign_row" />
              {reg.isRawPresentButNotLinkSafe ? (
                <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                  {ti('registerFkNotUuidHint')}
                </Typography.Text>
              ) : null}
            </Space>
          );
        },
      },
      {
        title: ti('colAmount'),
        dataIndex: 'totalAmount',
        key: 'totalAmount',
        render: (v: number) =>
          v != null ? formatCurrency(Number(v), formatLocale) : FORMAT_EMPTY_DISPLAY,
      },
    ],
    [ti, foByPayment, formatLocale, backendApiTooltip]
  );

  const auditColumns = useMemo(
    () => [
      {
        title: ti('auditColTime'),
        width: 152,
        render: (_: unknown, r: AuditLogEntryDto) =>
          formatAuditTimestamp(r.timestamp, formatLocale),
      },
      {
        title: ti('auditColAction'),
        dataIndex: 'action',
        width: 200,
        ellipsis: true,
      },
      {
        title: ti('auditColStatus'),
        width: 88,
        render: (_: unknown, r: AuditLogEntryDto) => {
          const p = viewAuditLogStatusPresentation(r.status);
          return <Tag color={p.antColor}>{p.label}</Tag>;
        },
      },
      {
        title: ti('auditColEntity'),
        width: 120,
        ellipsis: true,
        render: (_: unknown, r: AuditLogEntryDto) => r.entityType ?? FORMAT_EMPTY_DISPLAY,
      },
      {
        title: ti('auditColReplayRepair'),
        width: 200,
        render: (_: unknown, r: AuditLogEntryDto) => {
          const m = parseReplayMeta(r.requestData, r.responseData);
          return (
            <Space size={4} wrap>
              {m.replayPath ? <Tag>{ti('replayPathTag', { path: m.replayPath })}</Tag> : null}
              {m.payloadRepaired === true ? (
                <Tag color="orange">{ti('tagPayloadRepaired')}</Tag>
              ) : null}
              {!m.replayPath && m.payloadRepaired !== true ? (
                <Typography.Text type="secondary">{FORMAT_EMPTY_DISPLAY}</Typography.Text>
              ) : null}
            </Space>
          );
        },
      },
      {
        title: ti('auditColDescription'),
        dataIndex: 'description',
        ellipsis: true,
      },
    ],
    [ti, formatLocale]
  );

  return (
    <>
      <AdminPageHeader
        title={ti('pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('adminShell.group.rksv'), href: '/rksv' },
          { title: t('rksvHub.link.incident') },
        ]}
        actions={
          normalizedId ? (
            <Typography.Text code copyable={{ text: normalizedId }}>
              {normalizedId}
            </Typography.Text>
          ) : undefined
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, maxWidth: 980 }}>
          {ti('introLead')}{' '}
          <Link href="/rksv/finanz-online-queue">
            {OPERATOR_FO_SUMMARY_SCREEN_COPY.abgleichPrimaryLinkLabel}
          </Link>
          .
        </Typography.Paragraph>
      </AdminPageHeader>

      <Card size="small" style={{ marginBottom: 16 }}>
        <Space.Compact style={{ width: '100%', maxWidth: 520 }}>
          <Input
            placeholder={ti('searchPlaceholder')}
            value={inputId}
            onChange={(e) => setInputId(e.target.value)}
            onPressEnter={onSearch}
            allowClear
          />
          <Button type="primary" icon={<SearchOutlined />} onClick={onSearch}>
            {ti('searchButton')}
          </Button>
        </Space.Compact>
        <Typography.Paragraph
          type="secondary"
          style={{ marginTop: 10, marginBottom: 0, fontSize: 12 }}
        >
          {ti('searchTip', { prefix: RKSV_HANDOFF_PREFIX })}
        </Typography.Paragraph>
      </Card>

      {incidentError && (
        <Alert
          type="error"
          title={t('common.loadErrors.incidentAggregate')}
          description={
            incidentError instanceof Error
              ? incidentError.message
              : t('common.messages.noTechnicalDetail')
          }
          style={{ marginBottom: 16 }}
        />
      )}

      {notFound && (
        <Alert
          type="info"
          title={t('common.incident.aggregateNotFoundTitle')}
          description={t('common.incident.aggregateNotFoundDescription')}
          style={{ marginBottom: 16 }}
        />
      )}

      {isLoading && <CardSkeleton count={2} loading />}

      {hasBatch && batch && !incidentLoading && (
        <>
          <OperatorSummaryStrip>
            <Space orientation="vertical" size={10} style={{ width: '100%' }}>
              <div>
                <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
                  {ti('summaryCorrelationLabel')}
                </Typography.Text>
                <Typography.Text code copyable>
                  {normalizedId || correlationId || FORMAT_EMPTY_DISPLAY}
                </Typography.Text>
              </div>
              <Space wrap>
                <Tag color="blue">{ti('batchItemsTag', { count: batch.totalItems ?? 0 })}</Tag>
                <Tag color="green">{ti('successTag', { count: batch.successCount ?? 0 })}</Tag>
                <Tag color="orange">
                  {ti('failedDuplicateTag', { count: batch.failedOrDuplicateCount ?? 0 })}
                </Tag>
                {batch.offlineSyncedAuditCount != null && (
                  <Tag>{ti('auditOfflineSyncedTag', { count: batch.offlineSyncedAuditCount })}</Tag>
                )}
                {batch.offlineFinalFailureAuditCount != null &&
                  batch.offlineFinalFailureAuditCount > 0 && (
                    <Tag color="red">
                      {ti('auditFailFinalTag', { count: batch.offlineFinalFailureAuditCount })}
                    </Tag>
                  )}
                {batch.coverageSampleCount != null && (
                  <Tag color="default">
                    {ti('coverageSamplesTag', { count: batch.coverageSampleCount })}
                  </Tag>
                )}
              </Space>
              <Space orientation="vertical" size={4}>
                <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                  {ti('batchCorrelationApiLabel')}
                </Typography.Text>
                <Typography.Text code copyable>
                  {String(batch.correlationId)}
                </Typography.Text>
                <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                  {ti('auditCorrelationApiLabel')}
                </Typography.Text>
                <Typography.Text code copyable>
                  {batch.auditCorrelationId ?? FORMAT_EMPTY_DISPLAY}
                </Typography.Text>
              </Space>
              {hints ? (
                <Typography.Paragraph style={{ marginBottom: 0 }} type="secondary">
                  {OPERATOR_INCIDENT_COPY.foAggregateLine(
                    hints.finanzOnlineSubmittedCount ?? 0,
                    hints.finanzOnlineOpenOrProblemCount ?? 0
                  )}
                </Typography.Paragraph>
              ) : null}
              <Alert
                type="info"
                showIcon
                title={ti('foTruthNoticeTitle')}
                description={
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {ti('foTruthNoticeBefore')} <strong>{ti('foTruthNoticeStrong')}</strong>{' '}
                    {ti('foTruthNoticeAfter')}
                  </Typography.Text>
                }
              />
              {batch.correlationId ? (
                <Space wrap size={8}>
                  <Typography.Text type="secondary">
                    {t('common.investigation.furtherLabel')}:
                  </Typography.Text>
                  <Link
                    href={`/rksv/replay-batch/${encodeURIComponent(String(batch.correlationId))}`}
                  >
                    {OPERATOR_LINK_LABELS.replayBatchDetail}
                  </Link>
                  <Typography.Text type="secondary">·</Typography.Text>
                  <Link
                    href={`/rksv/verifications?correlationId=${encodeURIComponent(String(batch.auditCorrelationId ?? batch.correlationId))}`}
                  >
                    {OPERATOR_LINK_LABELS.verificationsAudit}
                  </Link>
                  <Typography.Text type="secondary">·</Typography.Text>
                  <Link
                    href={buildFinanzOnlineQueueInvestigationHref({
                      investigationBatchCorrelationId: String(batch.correlationId),
                    })}
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    {OPERATOR_LINK_LABELS.finanzQueueContext}
                  </Link>
                  <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                    {OPERATOR_INVESTIGATION_CONTEXT_COPY.foRowsNoCorrelationNote}
                  </Typography.Text>
                </Space>
              ) : null}
              {hints &&
                (hints.hasLockTimeoutAudit ||
                  hints.hasPayloadImmutableMismatchAudit ||
                  (hints.finanzOnlineOpenOrProblemCount ?? 0) > 0) && (
                  <Space orientation="vertical" size={8} style={{ width: '100%' }}>
                    {hints.hasLockTimeoutAudit && (
                      <Alert type="warning" showIcon title={ti('advisoryLockTimeoutTitle')} />
                    )}
                    {hints.hasPayloadImmutableMismatchAudit && (
                      <Alert type="error" showIcon title={ti('payloadImmutableMismatchTitle')} />
                    )}
                  </Space>
                )}
            </Space>
          </OperatorSummaryStrip>

          <OperatorBusinessSection>
            <Card size="small" title={ti('paymentsCardTitle')}>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 12, fontSize: 12 }}>
                {OPERATOR_INCIDENT_COPY.paymentsCardIntro}
              </Typography.Paragraph>
              <Divider style={{ margin: '0 0 12px' }} />
              <Table<ReplayBatchPaymentItemDto>
                columns={paymentColumns}
                dataSource={batch.payments ?? []}
                rowKey={(row) =>
                  row.paymentId ?? row.offlineTransactionId ?? row.receiptId ?? 'unknown'
                }
                pagination={false}
                size="small"
                scroll={{ x: 1180 }}
                expandable={{
                  expandedRowRender: (r) => {
                    const fo = foByPayment.get(String(r.paymentId));
                    return (
                      <div style={{ padding: '4px 8px 12px', background: '#fafafa' }}>
                        <Typography.Text strong style={{ fontSize: 12 }}>
                          {ti('expandRowTitle')}
                        </Typography.Text>
                        <Descriptions bordered size="small" column={1} style={{ marginTop: 8 }}>
                          <Descriptions.Item label={ti('labelOfflineTx')}>
                            {r.offlineTransactionId?.trim() ? (
                              <Typography.Text code copyable>
                                {r.offlineTransactionId}
                              </Typography.Text>
                            ) : (
                              FORMAT_EMPTY_DISPLAY
                            )}
                          </Descriptions.Item>
                          <Descriptions.Item label={ti('labelFoErrorFull')}>
                            {fo?.finanzOnlineError?.trim() ? (
                              <Typography.Text type="danger" copyable>
                                {fo.finanzOnlineError}
                              </Typography.Text>
                            ) : (
                              FORMAT_EMPTY_DISPLAY
                            )}
                          </Descriptions.Item>
                          <Descriptions.Item label={ti('labelFoRefFull')}>
                            {fo?.finanzOnlineReferenceId?.trim() ? (
                              <Typography.Text code copyable>
                                {fo.finanzOnlineReferenceId}
                              </Typography.Text>
                            ) : (
                              FORMAT_EMPTY_DISPLAY
                            )}
                          </Descriptions.Item>
                          <Descriptions.Item label={ti('labelBatchCorrelationContext')}>
                            <Typography.Text code copyable>
                              {String(batch.correlationId ?? FORMAT_EMPTY_DISPLAY)}
                            </Typography.Text>
                          </Descriptions.Item>
                          <Descriptions.Item label={ti('labelOpenAbgleich')}>
                            <Link
                              href={buildFinanzOnlineQueueInvestigationHref({
                                focusPaymentId: String(r.paymentId ?? ''),
                                investigationBatchCorrelationId: String(batch.correlationId ?? ''),
                              })}
                              target="_blank"
                              rel="noopener noreferrer"
                            >
                              {OPERATOR_FO_SUMMARY_SCREEN_COPY.abgleichPrimaryLinkLabel}
                            </Link>
                          </Descriptions.Item>
                          <Descriptions.Item label={ti('labelHint')}>
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                              {OPERATOR_INCIDENT_COPY.expandDtoNote}
                            </Typography.Text>
                          </Descriptions.Item>
                        </Descriptions>
                      </div>
                    );
                  },
                }}
              />
            </Card>
          </OperatorBusinessSection>

          <OperatorTechnicalSection>
            {timelineItems.length > 0 && (
              <Card size="small" title={ti('timelineCardTitle')} style={{ marginBottom: 16 }}>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                  {ti('timelineIntro')}
                </Typography.Paragraph>
                <Timeline
                  items={timelineItems.map((item) => ({
                    color: item.status === 0 ? 'green' : item.status === 1 ? 'red' : 'blue',
                    children: (
                      <div>
                        <Typography.Text strong>
                          {formatAuditTimestamp(item.timestamp, formatLocale)}
                        </Typography.Text>
                        <br />
                        <Typography.Text type="secondary">
                          {buildTimelineLabel(t, item.action, item.description, item.meta)}
                        </Typography.Text>
                        <div style={{ marginTop: 4 }}>
                          <Tag color={viewAuditLogStatusPresentation(item.full.status).antColor}>
                            {viewAuditLogStatusPresentation(item.full.status).label}
                          </Tag>
                        </div>
                        {(item.meta?.replayPath || item.meta?.payloadRepaired !== undefined) && (
                          <div style={{ marginTop: 4 }}>
                            {item.meta.replayPath && (
                              <Tag>{ti('replayPathTag', { path: item.meta.replayPath })}</Tag>
                            )}
                            {item.meta.payloadRepaired === true && (
                              <Tag color="orange">{ti('tagPayloadRepaired')}</Tag>
                            )}
                          </div>
                        )}
                      </div>
                    ),
                  }))}
                />
              </Card>
            )}

            {auditLogs.length > 0 && (
              <Card size="small" title={ti('auditStructureTitle')} style={{ marginBottom: 16 }}>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                  {ti('auditStructureIntro')}
                </Typography.Paragraph>
                <Table
                  size="small"
                  scroll={{ x: 900 }}
                  pagination={{ pageSize: 12 }}
                  rowKey={(r) => String(r.id ?? r.timestamp)}
                  dataSource={[...auditLogs].sort(
                    (a, b) =>
                      new Date(a.timestamp ?? 0).getTime() - new Date(b.timestamp ?? 0).getTime()
                  )}
                  columns={auditColumns}
                />
              </Card>
            )}

            {auditLogs.length > 0 && (
              <Collapse
                items={[
                  {
                    key: 'raw',
                    label: ti('rawAuditCollapseLabel'),
                    children: (
                      <pre
                        style={{
                          fontSize: 11,
                          maxHeight: 400,
                          overflow: 'auto',
                          background: '#f5f5f5',
                          padding: 12,
                        }}
                      >
                        {JSON.stringify(auditLogs, null, 2)}
                      </pre>
                    ),
                  },
                ]}
              />
            )}
          </OperatorTechnicalSection>
        </>
      )}
    </>
  );
}
