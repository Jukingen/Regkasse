'use client';

/**
 * Başarılı son yedek çalıştırması için artefakt listesi ve güvenli indirme düğmeleri (API: SettingsManage).
 */

import React, { useCallback, useState } from 'react';
import { Button, Card, Table, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { BackupArtifactResponseDto } from '@/api/generated/model';
import { downloadBackupArtifactFile } from '@/features/backup-dr/logic/downloadBackupArtifactFile';

export interface BackupArtifactsDownloadCardProps {
  runId: string;
  artifacts: BackupArtifactResponseDto[];
  canManage: boolean;
  t: (key: string, options?: Record<string, string | number>) => string;
}

function artifactTypeLabel(t: (key: string) => string, type: number | undefined): string {
  const key = `backupDr.download.types.${type ?? 'unknown'}`;
  const label = t(key);
  if (label === key) return t('backupDr.download.types.unknown');
  return label;
}

export function BackupArtifactsDownloadCard({ runId, artifacts, canManage, t }: BackupArtifactsDownloadCardProps) {
  const [busyId, setBusyId] = useState<string | null>(null);

  const onDownload = useCallback(
    async (artifact: BackupArtifactResponseDto) => {
      const id = artifact.id;
      if (!id) return;
      const fallback = `backup-${runId}-${id}`;
      setBusyId(id);
      try {
        await downloadBackupArtifactFile(runId, id, fallback);
      } catch {
        message.error(t('backupDr.download.error'));
      } finally {
        setBusyId(null);
      }
    },
    [runId, t],
  );

  const columns: ColumnsType<BackupArtifactResponseDto> = [
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
    {
      title: '',
      key: 'dl',
      width: 140,
      render: (_: unknown, row) => {
        const id = row.id;
        if (!id) return null;
        return (
          <Button
            type="link"
            size="small"
            disabled={!canManage}
            loading={busyId === id}
            onClick={() => onDownload(row)}
          >
            {t('backupDr.download.button')}
          </Button>
        );
      },
    },
  ];

  return (
    <Card title={t('backupDr.download.title')} size="small" style={{ marginTop: 16 }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        {t('backupDr.download.hint')}
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
