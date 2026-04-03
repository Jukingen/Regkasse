'use client';

/**
 * Artefakt listesi ve indirme: satır başına gerçeklik / kurtarılabilirlik / API dosya varlığı sinyali (iyimser etiket yok).
 */

import React, { useCallback, useMemo, useState } from 'react';
import { InfoCircleOutlined } from '@ant-design/icons';
import { Button, Card, Modal, Space, Spin, Table, Tag, Tooltip, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { BackupArtifactResponseDto } from '@/api/generated/model';
import {
  BackupArtifactDownloadError,
  downloadBackupArtifactFile,
} from '@/features/backup-dr/logic/downloadBackupArtifactFile';
import {
  type ArtifactDownloadRowTruth,
  type RunDownloadContext,
  artifactByteSizeFootnoteKey,
  artifactRealityBadgeKey,
  buildArtifactDownloadRowTruth,
  contentExpectationTableSummaryKey,
  formatArtifactByteSize,
  nonFakeSuspicionMessageKeys,
  recoverabilityUseShortKey,
  shouldConfirmDownloadUnprovenLogicalDump,
  sortArtifactsForOperatorDisplay,
} from '@/features/backup-dr/logic/backupArtifactDownloadTruth';
import { isSimulatedBackupAdapterKind } from '@/features/backup-dr/logic/backupDrMappers';

export type BackupArtifactsDownloadVariant = 'latest_success' | 'last_known_good';

export interface BackupArtifactsDownloadCardProps {
  variant: BackupArtifactsDownloadVariant;
  runId: string;
  artifacts: BackupArtifactResponseDto[];
  canManage: boolean;
  /** API bayrağı veya adaptör çıkarımı — kart üst bilgisinde kullanılır. */
  isSimulatedExecution?: boolean;
  runAdapterKind?: string | null;
  /** Recoverability summary: gerçek pg_dump yapılandırması (yoksa logical dump “kanıtlı” sayılmaz). */
  realPostgreSqlLogicalDumpConfigured?: boolean | null;
  /** Fake/Stub ortamı — başlık ve kapsam metni “gerçek yedek dosyası” ima etmez. */
  simulatedOperationalMode?: boolean;
  loadingArtifacts?: boolean;
  t: (key: string, options?: Record<string, string | number>) => string;
}

interface ArtifactRow extends BackupArtifactResponseDto {
  _truth: ArtifactDownloadRowTruth;
}

function recoverabilityUseKey(truth: ArtifactDownloadRowTruth): string {
  const m: Record<ArtifactDownloadRowTruth['recoverabilityUse'], string> = {
    not_dr_evidence_simulated: 'backupDr.download.recoverabilityUse.not_dr_evidence_simulated',
    not_dr_evidence_unverified_adapter: 'backupDr.download.recoverabilityUse.not_dr_evidence_unverified_adapter',
    possible_operational_artifact: 'backupDr.download.recoverabilityUse.possible_operational_artifact',
    unknown_recovery_value: 'backupDr.download.recoverabilityUse.unknown_recovery_value',
  };
  return m[truth.recoverabilityUse];
}

function filePresenceKey(truth: ArtifactDownloadRowTruth): string {
  const m: Record<ArtifactDownloadRowTruth['filePresence'], string> = {
    reported_present: 'backupDr.download.filePresence.reported_present',
    reported_absent: 'backupDr.download.filePresence.reported_absent',
    unknown: 'backupDr.download.filePresence.unknown',
  };
  return m[truth.filePresence];
}

function blockedReasonKey(reason: NonNullable<Extract<ArtifactDownloadRowTruth['download'], { state: 'blocked' }>['reason']>): string {
  const m = {
    no_manage: 'backupDr.download.downloadBlocked.no_manage',
    file_not_on_server: 'backupDr.download.downloadBlocked.file_not_on_server',
    file_presence_unknown: 'backupDr.download.downloadBlocked.file_presence_unknown',
  } as const;
  return m[reason];
}

export function BackupArtifactsDownloadCard({
  variant,
  runId,
  artifacts,
  canManage,
  isSimulatedExecution,
  runAdapterKind,
  realPostgreSqlLogicalDumpConfigured,
  simulatedOperationalMode = false,
  loadingArtifacts = false,
  t,
}: BackupArtifactsDownloadCardProps) {
  const [busyId, setBusyId] = useState<string | null>(null);

  const runCtx: RunDownloadContext = useMemo(
    () => ({
      isSimulatedExecutionFlag: isSimulatedExecution,
      runAdapterKind,
      realPostgreSqlLogicalDumpConfigured,
      canManage,
    }),
    [isSimulatedExecution, runAdapterKind, realPostgreSqlLogicalDumpConfigured, canManage],
  );

  const simulated =
    isSimulatedExecution === true ||
    (isSimulatedExecution === undefined && isSimulatedBackupAdapterKind(runAdapterKind));

  const rows: ArtifactRow[] = useMemo(() => {
    const ordered = sortArtifactsForOperatorDisplay(artifacts);
    return ordered.map((a) => ({
      ...a,
      _truth: buildArtifactDownloadRowTruth(a, runCtx),
    }));
  }, [artifacts, runCtx]);

  const hasNonFakeSuspicionRow = useMemo(
    () => rows.some((r) => r._truth.nonFakeSuspicion !== 'none'),
    [rows],
  );

  const showNonFakeIntegrityNote = useMemo(
    () => !simulated && rows.some((r) => r._truth.showIntegrityPrecheckDisclaimer),
    [rows, simulated],
  );

  const runDownloadBlob = useCallback(
    async (artifact: BackupArtifactResponseDto) => {
      const id = artifact.id;
      if (!id) return;
      const fallback = `backup-${runId}-${id}`;
      setBusyId(id);
      try {
        await downloadBackupArtifactFile(runId, id, fallback);
      } catch (e) {
        if (e instanceof BackupArtifactDownloadError) {
          const key =
            e.code === 'run_not_found'
              ? 'backupDr.download.errorRunNotFound'
              : e.code === 'artifact_not_found'
                ? 'backupDr.download.errorArtifactNotFound'
                : e.code === 'file_missing'
                  ? 'backupDr.download.errorFileMissing'
                  : e.code === 'not_found'
                    ? 'backupDr.download.errorNotFound'
                    : e.code === 'conflict'
                      ? 'backupDr.download.errorConflict'
                      : e.code === 'storage'
                        ? 'backupDr.download.errorStorage'
                        : e.code === 'simulated_not_downloadable'
                          ? 'backupDr.download.errorSimulated'
                          : e.code === 'forbidden'
                            ? 'backupDr.download.errorForbidden'
                            : e.code === 'unauthorized'
                              ? 'backupDr.download.errorUnauthorized'
                                : e.code === 'unknown'
                                  ? 'backupDr.download.errorUnknown'
                                  : e.code === 'empty_payload'
                                    ? 'backupDr.download.errorEmptyPayload'
                                    : 'backupDr.download.error';
          message.error(t(key));
          return;
        }
        message.error(t('backupDr.download.error'));
      } finally {
        setBusyId(null);
      }
    },
    [runId, t],
  );

  const onDownloadRow = useCallback(
    (row: ArtifactRow) => {
      const id = row.id;
      if (!id) return;
      const stubLike = simulated || row._truth.sourceExecutionReality === 'simulated_stub';
      const labelKey = row._truth.artifactClassLabelKey;
      const rawLabel = t(labelKey);
      const artifactName = rawLabel === labelKey ? t('backupDr.download.types.unknown') : rawLabel;

      if (stubLike && canManage) {
        Modal.confirm({
          title: t('backupDr.download.confirmStubTitle'),
          width: 520,
          content: (
            <Space direction="vertical" size="small" style={{ width: '100%' }}>
              <Typography.Paragraph style={{ marginBottom: 0 }}>
                {t('backupDr.download.confirmStubBody', {
                  artifact: artifactName,
                  size: formatArtifactByteSize(row.byteSize ?? null, t),
                })}
              </Typography.Paragraph>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                {t('backupDr.download.confirmStubNotEmptyFailedBackup')}
              </Typography.Paragraph>
              <Typography.Text strong>{t('backupDr.download.confirmStubExpectationLead')}</Typography.Text>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t(row._truth.contentExpectationKey)}
              </Typography.Paragraph>
            </Space>
          ),
          okText: t('backupDr.download.confirmStubOk'),
          cancelText: t('common.buttons.cancel'),
          onOk: () => runDownloadBlob(row),
        });
        return;
      }
      if (shouldConfirmDownloadUnprovenLogicalDump(row, row._truth) && canManage) {
        Modal.confirm({
          title: t('backupDr.download.confirmUnprovenLogicalTitle'),
          width: 520,
          content: (
            <Space direction="vertical" size="small" style={{ width: '100%' }}>
              <Typography.Paragraph style={{ marginBottom: 0 }}>{t('backupDr.download.confirmUnprovenLogicalLead')}</Typography.Paragraph>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                {t(row._truth.contentExpectationKey)}
              </Typography.Paragraph>
            </Space>
          ),
          okText: t('backupDr.download.confirmUnprovenLogicalOk'),
          cancelText: t('common.buttons.cancel'),
          onOk: () => runDownloadBlob(row),
        });
        return;
      }
      void runDownloadBlob(row);
    },
    [canManage, runDownloadBlob, simulated, t],
  );

  const titleKey = (() => {
    if (simulatedOperationalMode) {
      return variant === 'last_known_good'
        ? 'backupDr.download.titleLastKnownGoodFake'
        : 'backupDr.download.titleLatestSuccessFake';
    }
    return variant === 'last_known_good' ? 'backupDr.download.titleLastKnownGood' : 'backupDr.download.titleLatestSuccess';
  })();
  const scopeKey = (() => {
    if (simulatedOperationalMode) {
      return variant === 'last_known_good'
        ? 'backupDr.download.scopeLastKnownGoodFake'
        : 'backupDr.download.scopeLatestSuccessFake';
    }
    return variant === 'last_known_good' ? 'backupDr.download.scopeLastKnownGood' : 'backupDr.download.scopeLatestSuccess';
  })();

  const artifactColTitle = simulated ? t('backupDr.download.artifactLabelSimulated') : t('backupDr.download.artifactLabel');
  const byteColTitle = simulated ? t('backupDr.download.byteSizeColumnSimulated') : t('backupDr.download.byteSizeColumn');

  const columns: ColumnsType<ArtifactRow> = useMemo(
    () => [
    {
      title: artifactColTitle,
      key: 'artifact',
      render: (_: unknown, row, index) => {
        const k = row._truth.artifactClassLabelKey;
        const label = t(k);
        const text = label === k ? t('backupDr.download.types.unknown') : label;
        const stubRow = simulated || row._truth.sourceExecutionReality === 'simulated_stub';
        const realityKey = artifactRealityBadgeKey(row._truth.sourceExecutionReality);
        const summaryKey = contentExpectationTableSummaryKey(
          row.artifactType,
          row._truth.sourceExecutionReality,
          realPostgreSqlLogicalDumpConfigured,
        );
        const displayExpectKey = summaryKey ?? row._truth.contentExpectationKey;
        return (
          <div>
            {simulated && rows.length > 1 ? (
              <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', marginBottom: 4 }}>
                {t('backupDr.download.rowPosition', { current: index + 1, total: rows.length })}
              </Typography.Text>
            ) : null}
            <Typography.Text strong>{text}</Typography.Text>
            <div style={{ marginTop: 6 }}>
              <Space size={[4, 4]} wrap>
                <Tag
                  color={
                    stubRow ? 'blue' : row._truth.sourceExecutionReality === 'non_simulated' ? 'geekblue' : 'default'
                  }
                >
                  {t(realityKey)}
                </Tag>
                <Tooltip title={t(recoverabilityUseKey(row._truth))}>
                  <Tag>
                    {t(recoverabilityUseShortKey(row._truth.recoverabilityUse))}{' '}
                    <InfoCircleOutlined style={{ fontSize: 11, opacity: 0.75 }} />
                  </Tag>
                </Tooltip>
              </Space>
            </div>
            <div style={{ marginTop: 8 }}>
              <Tooltip title={t(row._truth.contentExpectationKey)}>
                <Typography.Paragraph
                  type="secondary"
                  style={{ fontSize: 12, marginBottom: 0, maxWidth: 360 }}
                  ellipsis={{ rows: 2, expandable: false }}
                >
                  {summaryKey ? (
                    <>
                      <span style={{ fontWeight: 600 }}>{t('backupDr.download.preDownloadWhatYouGet')} </span>
                      {t(displayExpectKey)}
                    </>
                  ) : (
                    t(row._truth.contentExpectationKey)
                  )}
                </Typography.Paragraph>
              </Tooltip>
            </div>
          </div>
        );
      },
    },
    {
      title: byteColTitle,
      key: 'byteSize',
      width: 200,
      render: (_: unknown, row) => {
        const suspicionKeys = nonFakeSuspicionMessageKeys(row._truth.nonFakeSuspicion);
        const footKey = artifactByteSizeFootnoteKey(row.artifactType, row._truth.sourceExecutionReality);
        return (
          <div>
            <Space size={4} wrap align="start">
              <div>
                <Typography.Text type="secondary">{formatArtifactByteSize(row.byteSize ?? null, t)}</Typography.Text>
                {footKey ? (
                  <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', marginTop: 4 }}>
                    {t(footKey)}
                  </Typography.Text>
                ) : null}
              </div>
              {suspicionKeys ? (
                <Tooltip title={t(suspicionKeys.detail)}>
                  <Tag color="warning" style={{ marginInlineEnd: 0 }}>
                    {t(suspicionKeys.short)}
                  </Tag>
                </Tooltip>
              ) : null}
            </Space>
          </div>
        );
      },
    },
    {
      title: t('backupDr.download.filePresenceLabel'),
      key: 'presence',
      width: 200,
      render: (_: unknown, row) => (
        <Typography.Text type={row._truth.filePresence === 'reported_absent' ? 'danger' : 'secondary'}>
          {t(filePresenceKey(row._truth))}
        </Typography.Text>
      ),
    },
    {
      title: t('backupDr.download.actionLabel'),
      key: 'dl',
      width: 160,
      render: (_: unknown, row) => {
        const id = row.id;
        if (!id) return null;
        const { download } = row._truth;
        const disabled = download.state !== 'eligible';
        const stubRow = simulated || row._truth.sourceExecutionReality === 'simulated_stub';
        const labelKey = stubRow ? 'backupDr.download.buttonStub' : 'backupDr.download.button';
        const suspicionKeys = nonFakeSuspicionMessageKeys(row._truth.nonFakeSuspicion);
        const tooltipTitle =
          download.state === 'blocked'
            ? t(blockedReasonKey(download.reason))
            : stubRow
              ? t('backupDr.download.downloadStubTooltip')
              : suspicionKeys
                ? (
                    <div>
                      <div>{t('backupDr.download.downloadNotProvenUntilRequest')}</div>
                      <div style={{ marginTop: 8 }}>{t(suspicionKeys.detail)}</div>
                    </div>
                  )
                : t('backupDr.download.downloadNotProvenUntilRequest');
        return (
          <Tooltip title={tooltipTitle}>
            <span>
              <Button
                type="link"
                size="small"
                disabled={disabled}
                loading={busyId === id}
                onClick={() => onDownloadRow(row)}
              >
                {t(labelKey)}
              </Button>
            </span>
          </Tooltip>
        );
      },
    },
  ],
    [artifactColTitle, byteColTitle, busyId, canManage, onDownloadRow, realPostgreSqlLogicalDumpConfigured, rows.length, simulated, t],
  );

  return (
    <Card title={t(titleKey)} size="small" style={{ marginTop: 16 }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        {t(scopeKey)}
      </Typography.Paragraph>

      {simulated ? (
        <div
          style={{
            marginBottom: 12,
            padding: '10px 12px',
            borderRadius: 6,
            border: '1px solid #91caff',
            background: '#e6f4ff',
          }}
        >
          <Typography.Text strong style={{ display: 'block', marginBottom: 6 }}>
            {t('backupDr.download.stubZoneAlertTitle')}
          </Typography.Text>
          <Typography.Paragraph style={{ marginBottom: 8 }}>{t('backupDr.download.stubZoneAlertDescription')}</Typography.Paragraph>
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
            {t('backupDr.download.simulatedWarning', { adapter: runAdapterKind ?? '—' })}
          </Typography.Text>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {t('backupDr.download.simulatedDownloadDisclaimer')}
          </Typography.Paragraph>
        </div>
      ) : null}

      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        {t('backupDr.download.pathsOmitted')}
      </Typography.Paragraph>

      {!simulated && (showNonFakeIntegrityNote || hasNonFakeSuspicionRow) ? (
        <div
          style={{
            marginBottom: 12,
            padding: '8px 10px',
            borderRadius: 6,
            border: `1px solid ${hasNonFakeSuspicionRow ? '#ffd591' : '#d9d9d9'}`,
            background: hasNonFakeSuspicionRow ? '#fffbe6' : '#fafafa',
          }}
        >
          {showNonFakeIntegrityNote ? (
            <Typography.Paragraph style={{ marginBottom: hasNonFakeSuspicionRow ? 8 : 0 }}>
              {t('backupDr.download.integrityPrecheckNote')}
            </Typography.Paragraph>
          ) : null}
          {hasNonFakeSuspicionRow ? (
            <Typography.Paragraph type="warning" style={{ marginBottom: 0 }}>
              {t('backupDr.download.suspicionBulkIntro')}
            </Typography.Paragraph>
          ) : null}
        </div>
      ) : null}

      {loadingArtifacts ? (
        <Spin style={{ display: 'block', marginBottom: 12 }} />
      ) : null}

      {!loadingArtifacts && artifacts.length === 0 ? (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          {t('backupDr.download.noArtifactsInRun')}
        </Typography.Paragraph>
      ) : null}

      {!loadingArtifacts && artifacts.some((a) => a.isFilePresentForDownload === false) ? (
        <Typography.Paragraph type="warning" style={{ marginBottom: 12 }}>
          {t('backupDr.download.fileUnavailableOnServer')}
        </Typography.Paragraph>
      ) : null}

      {!loadingArtifacts && artifacts.some((a) => a.isFilePresentForDownload === undefined) ? (
        <Typography.Paragraph type="warning" style={{ marginBottom: 12 }}>
          {t('backupDr.download.filePresenceUnknownBulk')}
        </Typography.Paragraph>
      ) : null}

      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        {simulated ? t('backupDr.download.stubHint') : t('backupDr.download.hint')}
      </Typography.Paragraph>
      {simulated ? (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          {t('backupDr.download.stubPayloadNote')}
        </Typography.Paragraph>
      ) : null}

      {!canManage ? (
        <Typography.Text type="warning">{t('backupDr.download.needManage')}</Typography.Text>
      ) : null}

      <Table<ArtifactRow>
        rowKey={(r, i) => r.id ?? `row-${i}-${r.storageLocator ?? 'na'}`}
        size="small"
        pagination={false}
        dataSource={rows}
        columns={columns}
      />
    </Card>
  );
}
