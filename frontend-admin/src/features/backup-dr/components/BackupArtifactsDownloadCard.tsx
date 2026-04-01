'use client';

/**
 * Başarılı son yedek çalıştırması için artefakt listesi ve güvenli indirme düğmeleri (API: SettingsManage).
 */

import React, { useCallback, useState } from 'react';
import { Button, Card, Table, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { BackupArtifactResponseDto } from '@/api/generated/model';
import {
  BackupArtifactDownloadError,
  downloadBackupArtifactFile,
} from '@/features/backup-dr/logic/downloadBackupArtifactFile';

export interface BackupArtifactsDownloadCardProps {
  runId: string;
  artifacts: BackupArtifactResponseDto[];
  canManage: boolean;
  /** API: Fake/ProductionStub yürütme (pg_dump yok). */
  isSimulatedExecution?: boolean;
  /** Bu çalıştırmanın adapter’ı (API yoksa yedek çıkarım). */
  runAdapterKind?: string | null;
  t: (key: string, options?: Record<string, string | number>) => string;
}

function artifactTypeLabel(t: (key: string) => string, type: number | undefined): string {
  const key = `backupDr.download.types.${type ?? 'unknown'}`;
  const label = t(key);
  if (label === key) return t('backupDr.download.types.unknown');
  return label;
}

function isSimulatedAdapterKind(kind: string | null | undefined): boolean {
  const k = (kind ?? '').trim();
  return k === 'Fake' || k === 'ProductionStub';
}

export function BackupArtifactsDownloadCard({
  runId,
  artifacts,
  canManage,
  isSimulatedExecution,
  runAdapterKind,
  t,
}: BackupArtifactsDownloadCardProps) {
  const [busyId, setBusyId] = useState<string | null>(null);
  const simulated =
    isSimulatedExecution === true ||
    (isSimulatedExecution === undefined && isSimulatedAdapterKind(runAdapterKind));

  const onDownload = useCallback(
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

  const baseColumns: ColumnsType<BackupArtifactResponseDto> = [
    {
      title: t('backupDr.download.typeLabel'),
      key: 'type',
      render: (_: unknown, row) => artifactTypeLabel(t, row.artifactType),
    },
    {
      title: t('backupDr.download.locatorLabel'),
      dataIndex: 'storageLocator',
      key: 'storageLocator',
      render: (v: string | undefined) => v ?? '—',
    },
    {
      title: t('backupDr.download.externalLabel'),
      dataIndex: 'externalRedactedLocator',
      key: 'externalRedactedLocator',
      render: (v: string | null | undefined) => v ?? '—',
    },
  ];

  const downloadColumn: ColumnsType<BackupArtifactResponseDto>[0] = {
    title: '',
    key: 'dl',
    width: 140,
    render: (_: unknown, row) => {
      const id = row.id;
      if (!id) return null;
      const fileMissing = row.isFilePresentForDownload === false;
      return (
        <Button
          type="link"
          size="small"
          disabled={!canManage || fileMissing}
          loading={busyId === id}
          onClick={() => onDownload(row)}
        >
          {t('backupDr.download.button')}
        </Button>
      );
    },
  };

  const columns = [...baseColumns, downloadColumn];

  return (
    <Card title={t('backupDr.download.title')} size="small" style={{ marginTop: 16 }}>
      {simulated ? (
        <Typography.Paragraph type="warning" style={{ marginBottom: 12 }}>
          {t('backupDr.download.simulatedWarning', { adapter: runAdapterKind ?? '—' })}
        </Typography.Paragraph>
      ) : null}
      {simulated ? (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          {t('backupDr.download.simulatedDownloadDisclaimer')}
        </Typography.Paragraph>
      ) : null}
      {artifacts.length === 0 ? (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          {t('backupDr.download.noArtifactsInLatestRun')}
        </Typography.Paragraph>
      ) : null}
      {artifacts.some((a) => a.isFilePresentForDownload === false) ? (
        <Typography.Paragraph type="warning" style={{ marginBottom: 12 }}>
          {t('backupDr.download.fileUnavailableOnServer')}
        </Typography.Paragraph>
      ) : null}
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        {simulated ? t('backupDr.download.stubHint') : t('backupDr.download.hint')}
      </Typography.Paragraph>
      {!canManage ? (
        <Typography.Text type="warning">{t('backupDr.download.needManage')}</Typography.Text>
      ) : null}
      <Table<BackupArtifactResponseDto>
        rowKey={(r) => r.id ?? String(r.storageLocator)}
        size="small"
        pagination={false}
        dataSource={artifacts}
        columns={columns}
      />
    </Card>
  );
}
