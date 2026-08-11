'use client';

/**
 * Paginated backup runs table (GET /api/admin/backup/runs) with operator columns.
 */
import { useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Badge,
  Button,
  Modal,
  Popconfirm,
  Progress,
  Select,
  Space,
  Table,
  Tooltip,
  Typography,
} from 'antd';
import type { ColumnsType, TableProps } from 'antd/es/table';
import React, { useCallback, useMemo, useState } from 'react';

import { dateColumnRender } from '@/components/DateColumn';
import { useGetApiAdminBackupStatusLatest } from '@/api/generated/admin-backup/admin-backup';
import type { BackupRunResponseDto } from '@/api/generated/model';
import { BackupRunStatus } from '@/api/generated/model/backupRunStatus';
import {
  BACKUP_RECENT_RUNS_PAGE_SIZE,
  usePollAlignedWithLatestDashboardBackup,
  usePollBackupLatestDashboardInterval,
} from '@/features/backup-dr/logic/backupDashboardQueryTiming';
import { apiNullableToUndefined } from '@/features/backup-dr/logic/backupDrDtoNormalize';
import { formatBackupBytes } from '@/features/backup-dr/logic/backupFormat';
import { triggerErrorMessageBackupDashboard } from '@/features/backup-dr/logic/backupManualTriggerMessaging';
import { describeBackupTriggerOutcome } from '@/features/backup-dr/logic/backupTriggerOutcome';
import {
  backupQueryKeys,
  useBackupRuns,
  useTriggerBackup,
} from '@/features/backup/api/backupHooks';
import { BackupDetailModal } from '@/features/backup/components/BackupDetailModal';
import {
  BackupContentValidationReport,
  ContentValidationStatusBadge,
} from '@/features/backup/components/BackupContentValidationReport';
import { BackupStatusBadge } from '@/features/backup/components/BackupStatusBadge';
import { BackupVerificationReport } from '@/features/backup/components/BackupVerificationReport';
import { useBackupPermissions } from '@/features/backup/hooks/useBackupPermissions';
import { useTenants } from '@/features/backup/hooks/useTenants';
import { verifyBackupChecksum } from '@/features/backup/logic/backupChecksumVerifyApi';
import {
  getBackupContentValidation,
  normalizeContentValidationStatus,
  type BackupContentValidationDto,
} from '@/features/backup/logic/backupContentValidationApi';
import { resolveContentValidationBadgeStatus } from '@/features/backup/logic/backupContentValidationPresentation';
import { runRestoreDrill } from '@/features/backup/logic/backupDrillApi';
import { isBackupRunSucceeded } from '@/features/backup/logic/backupRunDetailPresentation';
import {
  compareBackupRunsByRequestedAtDesc,
  filterBackupRunsByTenantIdempotency,
  isBackupRunFailed,
  resolveBackupRunDurationLabel,
  resolveBackupRunSizeLabel,
  resolveBackupRunTotalBytes,
} from '@/features/backup/logic/backupRunTablePresentation';
import {
  isVerificationFailed,
  isVerificationPassed,
  resolveLatestVerification,
} from '@/features/backup/logic/backupVerificationPresentation';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { formatUserTime } from '@/lib/dateFormatter';

export interface BackupRunsTableProps {
  onViewDetails?: (run: BackupRunResponseDto) => void;
  onRetryInvalidate?: () => Promise<void>;
  /** Hide table title row (parent Card supplies title). */
  hideTitle?: boolean;
}

export function BackupRunsTable({
  onViewDetails,
  onRetryInvalidate,
  hideTitle = false,
}: BackupRunsTableProps) {
  const notify = useNotify();

  const { t, formatLocale } = useI18n();
  const queryClient = useQueryClient();
  const permissions = useBackupPermissions();
  const { canTrigger, canFilterRunsByTenant, isSuperAdmin, canRestore } = permissions;
  const [page, setPage] = useState(1);
  const [selectedTenantId, setSelectedTenantId] = useState<string | undefined>();
  const [detailRunId, setDetailRunId] = useState<string | null>(null);
  const [detailModalOpen, setDetailModalOpen] = useState(false);
  const [verificationReportRunId, setVerificationReportRunId] = useState<string | null>(null);
  const [verificationReportOpen, setVerificationReportOpen] = useState(false);
  const [verifyingRunId, setVerifyingRunId] = useState<string | null>(null);
  const [contentValidatingRunId, setContentValidatingRunId] = useState<string | null>(null);
  const [contentReport, setContentReport] = useState<BackupContentValidationDto | null>(null);
  const [contentReportOpen, setContentReportOpen] = useState(false);
  const [contentStatusByRunId, setContentStatusByRunId] = useState<Record<string, string>>({});
  const [drillingRunId, setDrillingRunId] = useState<string | null>(null);

  const { tenants, isLoading: tenantsLoading } = useTenants({
    enabled: canFilterRunsByTenant,
  });

  const triggerBackup = useTriggerBackup();

  const handleRetrySuccess = useCallback(
    async (res: Awaited<ReturnType<typeof triggerBackup.mutateAsync>>) => {
      const fb = describeBackupTriggerOutcome(res);
      const suffix = res.orchestrationState?.trim()
        ? ` ${t('backupDr.messages.orchestrationStateSuffix', { state: res.orchestrationState })}`
        : '';
      const text = `${t(fb.messageKey)}${suffix}`;
      if (fb.level === 'success') notify.success(text);
      else notify.info(text);
      await queryClient.invalidateQueries({ queryKey: backupQueryKeys.all });
      await queryClient.invalidateQueries({ queryKey: backupQueryKeys.dashboardStats() });
      await queryClient.invalidateQueries({ queryKey: backupQueryKeys.recoverability() });
      if (onRetryInvalidate) await onRetryInvalidate();
    },
    [notify, onRetryInvalidate, queryClient, t]
  );

  const pollPeek = usePollBackupLatestDashboardInterval();
  const alignSource = useGetApiAdminBackupStatusLatest({
    query: { refetchInterval: pollPeek, refetchOnWindowFocus: true },
  });
  const latestPeek = apiNullableToUndefined(alignSource.data?.latestRun);
  const pollAlignedRuns = usePollAlignedWithLatestDashboardBackup(latestPeek?.status);

  const runsQuery = useBackupRuns(
    {
      page,
      pageSize: BACKUP_RECENT_RUNS_PAGE_SIZE,
      tenantId: canFilterRunsByTenant ? selectedTenantId : undefined,
    },
    { refetchInterval: pollAlignedRuns }
  );

  const formatTime = useCallback(
    (iso: string | undefined | null) => {
      if (!iso) return t('backupDr.runsTable.noValue');
      return formatUserTime(iso) || iso;
    },
    [t]
  );

  const artifactTypeLabel = useCallback(
    (type: number | undefined) => {
      if (type === undefined) return t('backupDr.runsTable.noValue');
      const key = `backupDr.runsTable.artifactType.${type}`;
      const label = t(key);
      return label === key ? String(type) : label;
    },
    [t]
  );

  const viewDetails = useCallback(
    (run: BackupRunResponseDto) => {
      if (onViewDetails) {
        onViewDetails(run);
        return;
      }
      if (run.id) {
        setDetailRunId(run.id);
        setDetailModalOpen(true);
      }
    },
    [onViewDetails]
  );

  const handleVerifyChecksum = useCallback(
    async (runId: string) => {
      setVerifyingRunId(runId);
      try {
        const result = await verifyBackupChecksum(runId);
        if (result.isValid) {
          notify.successKey('backupDr.checksumVerify.checksumPassed');
        } else if (
          result.artifacts?.some((a) => a.status === 'missing_hash') &&
          !result.artifacts.some((a) => a.status === 'failed' || a.status === 'missing_file')
        ) {
          notify.warning('backupDr.checksumVerify.checksumNotAvailable');
        } else {
          notify.error('backupDr.checksumVerify.checksumFailed', {
            description: result.failureReason ?? undefined,
          });
        }
        await queryClient.invalidateQueries({ queryKey: backupQueryKeys.all });
      } catch (err) {
        notify.apiError(err, {
          logContext: 'BackupRunsTable.verifyChecksum',
          fallbackKey: 'backupDr.checksumVerify.checksumFailed',
        });
      } finally {
        setVerifyingRunId(null);
      }
    },
    [notify, queryClient]
  );

  const handleValidateContent = useCallback(
    async (runId: string) => {
      setContentValidatingRunId(runId);
      try {
        const result = await getBackupContentValidation(runId);
        setContentReport(result);
        setContentReportOpen(true);
        setContentStatusByRunId((prev) => ({ ...prev, [runId]: result.overallStatus }));
        const status = normalizeContentValidationStatus(result.overallStatus);
        if (status === 'passed') {
          notify.successKey('backup.contentValidationPassed');
        } else if (status === 'partial') {
          notify.warning(t('backup.contentValidationPartial'));
        } else if (status === 'unavailable') {
          notify.warning(t('backup.contentValidationUnavailable'));
        } else {
          notify.error(t('backup.contentValidationFailed'), {
            description: result.summary ?? undefined,
          });
        }
      } catch (err) {
        notify.apiError(err, {
          logContext: 'BackupRunsTable.validateContent',
          fallbackKey: 'backup.contentValidationFailed',
        });
      } finally {
        setContentValidatingRunId(null);
      }
    },
    [notify, t]
  );

  const handleRunDrill = useCallback(
    async (runId: string) => {
      setDrillingRunId(runId);
      notify.info(t('backup.drillRunning'));
      try {
        const result = await runRestoreDrill({ backupRunId: runId });
        if (result.success) {
          if (result.newQueuedRunCreated) {
            notify.successKey('backup.drillCompleted');
          } else if (result.existingRunReturned) {
            notify.info(t('backup.drillExisting'));
          } else {
            notify.successKey('backup.drillCompleted');
          }
        } else {
          notify.error(t('backup.drillFailed'), {
            description: result.errors?.join(' · ') || result.status,
          });
        }
        await queryClient.invalidateQueries({ queryKey: backupQueryKeys.dashboardStats() });
        await queryClient.invalidateQueries({ queryKey: ['/api/admin/restore-verification'] });
      } catch (err) {
        notify.apiError(err, {
          logContext: 'BackupRunsTable.runDrill',
          fallbackKey: 'backup.drillFailed',
        });
      } finally {
        setDrillingRunId(null);
      }
    },
    [notify, queryClient, t]
  );

  const columns: ColumnsType<BackupRunResponseDto> = useMemo(
    () => [
      {
        title: t('backupDr.runsTable.startTime'),
        dataIndex: 'requestedAt',
        key: 'requestedAt',
        render: dateColumnRender('datetime'),
        sorter: (a, b) => compareBackupRunsByRequestedAtDesc(a, b) * -1,
        defaultSortOrder: 'descend',
      },
      {
        title: t('backupDr.runsTable.statusColumn'),
        dataIndex: 'status',
        key: 'status',
        render: (status: number | undefined) => <BackupStatusBadge status={status} />,
        filters: [
          { text: t('backupDr.runsTable.statusLabels.succeeded'), value: BackupRunStatus.NUMBER_3 },
          { text: t('backupDr.runsTable.statusLabels.failed'), value: BackupRunStatus.NUMBER_4 },
          {
            text: t('backupDr.runsTable.statusLabels.verificationFailed'),
            value: BackupRunStatus.NUMBER_5,
          },
          { text: t('backupDr.runsTable.statusLabels.running'), value: BackupRunStatus.NUMBER_1 },
          { text: t('backupDr.runsTable.statusLabels.queued'), value: BackupRunStatus.NUMBER_0 },
        ],
        onFilter: (value, record) => record.status === value,
      },
      {
        title: t('backupDr.runsTable.duration'),
        key: 'duration',
        render: (_: unknown, record: BackupRunResponseDto) => {
          const label = resolveBackupRunDurationLabel(record, t);
          return (
            <Tooltip
              title={t('backupDr.runsTable.durationTooltip', {
                start: formatTime(record.startedAt),
                end: formatTime(record.completedAt),
              })}
            >
              <span>{label}</span>
            </Tooltip>
          );
        },
      },
      {
        title: t('backupDr.runsTable.size'),
        key: 'size',
        render: (_: unknown, record: BackupRunResponseDto) => {
          const label = resolveBackupRunSizeLabel(record, t);
          const bytes = resolveBackupRunTotalBytes(record);
          const tooltip =
            bytes > 0
              ? t('backupDr.runsTable.sizeBytesTooltip', {
                  bytes: bytes.toLocaleString(formatLocale),
                })
              : undefined;
          return (
            <Tooltip title={tooltip}>
              <span>{label}</span>
            </Tooltip>
          );
        },
      },
      {
        title: t('backupDr.runsTable.compression'),
        key: 'compression',
        render: (_: unknown, record: BackupRunResponseDto) => {
          const ratio = record.compressionRatio;
          if (ratio == null || Number.isNaN(ratio)) {
            return t('backupDr.runsTable.noValue');
          }
          const percent = Math.round(ratio);
          return (
            <Progress
              percent={percent}
              size="small"
              status={percent < 50 ? 'success' : 'normal'}
              format={() => `${percent}%`}
            />
          );
        },
      },
      {
        title: t('backupDr.runsTable.artifacts'),
        key: 'artifacts',
        render: (_: unknown, record: BackupRunResponseDto) => {
          const list = record.artifacts ?? [];
          if (!list.length) return t('backupDr.runsTable.noValue');
          return (
            <Space size={[4, 4]} wrap>
              {list.map((artifact) => {
                const typeLabel = artifactTypeLabel(artifact.artifactType);
                const sizeLabel =
                  artifact.formattedSize?.trim() ||
                  formatBackupBytes(artifact.byteSize ?? undefined, t);
                return (
                  <Tooltip
                    key={artifact.id ?? `${typeLabel}-${artifact.storageLocator ?? ''}`}
                    title={`${typeLabel}: ${sizeLabel}`}
                  >
                    <Badge
                      status={(artifact.byteSize ?? 0) > 0 ? 'success' : 'default'}
                      text={<Typography.Text style={{ fontSize: 12 }}>{typeLabel}</Typography.Text>}
                    />
                  </Tooltip>
                );
              })}
            </Space>
          );
        },
      },
      {
        title: t('backupDr.runsTable.error'),
        dataIndex: 'failureDetail',
        key: 'failureDetail',
        ellipsis: true,
        render: (text: string | null | undefined, record: BackupRunResponseDto) => {
          const detail = text?.trim() || record.failureCode?.trim();
          if (!detail) return t('backupDr.runsTable.noValue');
          return (
            <Typography.Text type="danger" ellipsis={{ tooltip: detail }}>
              {detail}
            </Typography.Text>
          );
        },
      },
      {
        title: t('backupDr.runsTable.lastVerification'),
        key: 'lastVerification',
        render: (_: unknown, record: BackupRunResponseDto) => {
          const latest = resolveLatestVerification(record);
          if (!latest) return t('backupDr.runsTable.noValue');
          const when = formatTime(latest.completedAt ?? latest.startedAt);
          if (isVerificationPassed(latest.status)) {
            return (
              <Badge
                status="success"
                text={
                  <Typography.Text style={{ fontSize: 12 }}>
                    {when}
                  </Typography.Text>
                }
              />
            );
          }
          if (isVerificationFailed(latest.status)) {
            return (
              <Badge
                status="error"
                text={
                  <Typography.Text type="danger" style={{ fontSize: 12 }}>
                    {when}
                  </Typography.Text>
                }
              />
            );
          }
          return (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {when}
            </Typography.Text>
          );
        },
      },
      {
        title: t('backup.contentValidation'),
        key: 'contentValidation',
        render: (_: unknown, record: BackupRunResponseDto) => {
          const status = resolveContentValidationBadgeStatus(
            record,
            record.id ? contentStatusByRunId[record.id] : null
          );
          if (!status) {
            return (
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('backup.contentValidationUnknown')}
              </Typography.Text>
            );
          }
          return <ContentValidationStatusBadge status={status} />;
        },
      },
      {
        title: t('backupDr.runsTable.actions'),
        key: 'actions',
        render: (_: unknown, record: BackupRunResponseDto) => (
          <Space
            size="small"
            wrap
            onClick={(e) => e.stopPropagation()}
            onKeyDown={(e) => e.stopPropagation()}
          >
            <Button type="link" size="small" onClick={() => viewDetails(record)}>
              {t('backupDr.runsTable.details')}
            </Button>
            {record.id && isBackupRunSucceeded(record.status) ? (
              <Button
                type="link"
                size="small"
                loading={verifyingRunId === record.id}
                disabled={
                  verifyingRunId != null ||
                  contentValidatingRunId != null ||
                  drillingRunId != null
                }
                onClick={() => void handleVerifyChecksum(record.id!)}
              >
                {t('backupDr.runsTable.verifyChecksum')}
              </Button>
            ) : null}
            {record.id && isBackupRunSucceeded(record.status) ? (
              <Button
                type="link"
                size="small"
                loading={contentValidatingRunId === record.id}
                disabled={
                  verifyingRunId != null ||
                  contentValidatingRunId != null ||
                  drillingRunId != null
                }
                onClick={() => void handleValidateContent(record.id!)}
              >
                {t('backup.validateContent')}
              </Button>
            ) : null}
            {record.id && isBackupRunSucceeded(record.status) && canRestore ? (
              <Button
                type="link"
                size="small"
                loading={drillingRunId === record.id}
                disabled={
                  verifyingRunId != null ||
                  contentValidatingRunId != null ||
                  drillingRunId != null
                }
                onClick={() => void handleRunDrill(record.id!)}
              >
                {t('backup.runRestoreDrill')}
              </Button>
            ) : null}
            {record.id && isBackupRunSucceeded(record.status) ? (
              <Button
                type="link"
                size="small"
                onClick={() => {
                  setVerificationReportRunId(record.id!);
                  setVerificationReportOpen(true);
                }}
              >
                {t('backupDr.verificationReport.openReportShort')}
              </Button>
            ) : null}
            {isBackupRunFailed(record.status) && canTrigger ? (
              <Popconfirm
                title={t('backupDr.runsTable.retryConfirmTitle')}
                description={t('backupDr.runsTable.retryConfirmDescription')}
                okText={t('backupDr.manual.confirmBackupOk')}
                cancelText={t('backupDr.manual.confirmBackupCancel')}
                onConfirm={() =>
                  void triggerBackup
                    .mutateAsync({ tenantId: selectedTenantId })
                    .then(handleRetrySuccess)
                    .catch((err) =>
                      notify.error(triggerErrorMessageBackupDashboard(err, t))
                    )
                }
              >
                <Button
                  type="link"
                  size="small"
                  loading={triggerBackup.isPending}
                  disabled={triggerBackup.isPending}
                >
                  {t('backupDr.runsTable.retry')}
                </Button>
              </Popconfirm>
            ) : null}
            {isSuperAdmin ? (
              <Tooltip title={t('backupDr.runsTable.deleteUnavailable')}>
                <Button type="link" size="small" danger disabled>
                  {t('backupDr.runsTable.delete')}
                </Button>
              </Tooltip>
            ) : null}
          </Space>
        ),
      },
    ],
    [
      artifactTypeLabel,
      canRestore,
      canTrigger,
      contentStatusByRunId,
      contentValidatingRunId,
      drillingRunId,
      formatLocale,
      formatTime,
      handleRetrySuccess,
      handleRunDrill,
      handleValidateContent,
      handleVerifyChecksum,
      isSuperAdmin,
      notify,
      selectedTenantId,
      t,
      triggerBackup.isPending,
      verifyingRunId,
      viewDetails,
    ]
  );

  const tenantOptions = useMemo(
    () =>
      tenants.map((row) => ({
        label: `${row.name} (${row.slug})`,
        value: row.id,
      })),
    [tenants]
  );

  const dataSource = useMemo(() => {
    const items = runsQuery.data?.items ?? [];
    const filtered = filterBackupRunsByTenantIdempotency(items, selectedTenantId);
    return [...filtered].sort(compareBackupRunsByRequestedAtDesc);
  }, [runsQuery.data?.items, selectedTenantId]);

  const onRetry = useCallback(async () => {
    if (onRetryInvalidate) await onRetryInvalidate();
    await runsQuery.refetch();
  }, [onRetryInvalidate, runsQuery]);

  const tableProps: TableProps<BackupRunResponseDto> = {
    rowKey: (r) => r.id ?? r.requestedAt ?? '',
    size: 'small',
    loading: runsQuery.isFetching,
    dataSource,
    columns,
    pagination: {
      current: page,
      pageSize: BACKUP_RECENT_RUNS_PAGE_SIZE,
      total: runsQuery.data?.totalCount ?? 0,
      showSizeChanger: false,
      onChange: (p) => setPage(p),
    },
    onRow: (record) => ({
      onClick: () => viewDetails(record),
      style: { cursor: 'pointer' },
    }),
  };

  return (
    <>
      {runsQuery.isError ? (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 12 }}
          title={t('backupDr.errors.partialTable')}
          action={
            <Button type="link" size="small" onClick={() => void onRetry()}>
              {t('backupDr.actions.refresh')}
            </Button>
          }
        />
      ) : null}
      {!hideTitle ? (
        <Typography.Title level={5} style={{ marginTop: 0 }}>
          {t('backupDr.adminBackup.recentBackupsTitle')}
        </Typography.Title>
      ) : null}
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 12 }}
        title={t('backupDr.runsTable.instanceScopeTitle')}
        description={t('backupDr.runsTable.instanceScopeDescription')}
      />
      {canFilterRunsByTenant ? (
        <Space style={{ marginBottom: 12 }} wrap>
          <Typography.Text type="secondary">
            {t('backupDr.runsTable.tenantFilterLabel')}
          </Typography.Text>
          <Select
            allowClear
            showSearch
            style={{ minWidth: 280 }}
            placeholder={t('backupDr.runsTable.tenantFilterPlaceholder')}
            value={selectedTenantId}
            onChange={(v) => {
              setSelectedTenantId(v);
              setPage(1);
            }}
            loading={tenantsLoading}
            options={tenantOptions}
            optionFilterProp="label"
          />
        </Space>
      ) : null}
      <Table<BackupRunResponseDto> {...tableProps} />
      <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
        {t('backupDr.runs.statusHint')}
      </Typography.Paragraph>
      <BackupDetailModal
        runId={detailRunId}
        open={detailModalOpen}
        onClose={() => setDetailModalOpen(false)}
      />
      {verificationReportRunId ? (
        <BackupVerificationReport
          backupRunId={verificationReportRunId}
          open={verificationReportOpen}
          onClose={() => {
            setVerificationReportOpen(false);
            setVerificationReportRunId(null);
          }}
        />
      ) : null}
      <Modal
        title={t('backup.contentValidation')}
        open={contentReportOpen}
        onCancel={() => setContentReportOpen(false)}
        footer={
          <Button onClick={() => setContentReportOpen(false)}>
            {t('backupDr.verificationReport.close')}
          </Button>
        }
        width={720}
        destroyOnHidden
      >
        {contentReport ? <BackupContentValidationReport report={contentReport} /> : null}
      </Modal>
    </>
  );
}
