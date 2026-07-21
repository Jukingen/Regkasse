'use client';

/**
 * Readable backup file list (GET /api/admin/backup/list) — logical dumps with tenant slug and manifest.
 */
import {
  DownloadOutlined,
  FileOutlined,
  InboxOutlined,
  RollbackOutlined,
  UploadOutlined,
} from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { Alert, App, Button, Space, Table, Tag, Tooltip, Typography, Upload } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import React, { useCallback, useMemo, useState } from 'react';

import {
  getGetApiAdminBackupListQueryKey,
  usePostApiAdminBackupArtifactsImport,
} from '@/api/generated/admin/admin';
import { TableSkeleton } from '@/components/Skeleton';
import {
  BackupArtifactDownloadError,
  downloadBackupArtifactFile,
} from '@/features/backup-dr/logic/downloadBackupArtifactFile';
import { BackupStatusBadge } from '@/features/backup/components/BackupStatusBadge';
import { RestoreModal, type RestoreModalBackup } from '@/features/backup/components/RestoreModal';
import {
  type BackupListItemResponseDto,
  useBackupList,
} from '@/features/backup/hooks/useBackupList';
import { useBackupPermissions } from '@/features/backup/hooks/useBackupPermissions';
import { useI18n } from '@/i18n';
import { formatBytes, formatDateTime } from '@/i18n/formatting';

export interface BackupListProps {
  onRetryInvalidate?: () => Promise<void>;
  /** When set, only the newest N rows are shown (overview hub). */
  limit?: number;
  /** Hide import dropzone — used on compact overview. */
  compact?: boolean;
  /**
   * Client-side strategy filter (API already enforces role).
   * `tenant` = Mandanten packages only; `system` = System only; `all` = no filter (Super Admin hub).
   */
  strategyFilter?: 'tenant' | 'system' | 'all';
}

type DownloadTarget = {
  backupRunId: string;
  artifactId: string;
  fileName: string;
};

function toDownloadTarget(
  backupRunId: string | null | undefined,
  artifactId: string | null | undefined,
  fileName: string | null | undefined
): DownloadTarget | null {
  if (!backupRunId || !artifactId || !fileName) return null;
  return { backupRunId, artifactId, fileName };
}

export function BackupList({
  onRetryInvalidate,
  limit,
  compact = false,
  strategyFilter = 'all',
}: BackupListProps) {
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const { t, formatLocale } = useI18n();
  const { canDownloadBackup, canRestore } = useBackupPermissions();
  const listQuery = useBackupList();
  const importMutation = usePostApiAdminBackupArtifactsImport();
  const [downloadingKey, setDownloadingKey] = useState<string | null>(null);
  const [dumpFile, setDumpFile] = useState<File | null>(null);
  const [manifestFile, setManifestFile] = useState<File | null>(null);
  const [restoreTarget, setRestoreTarget] = useState<RestoreModalBackup | null>(null);

  const formatDate = useCallback(
    (iso: string | undefined | null) => {
      if (!iso) return t('backupDr.runsTable.noValue');
      return formatDateTime(iso, formatLocale);
    },
    [formatLocale, t]
  );

  const formatFileSize = useCallback(
    (size: number | null | undefined) => {
      if (size == null || size < 0) return t('backupDr.runsTable.noValue');
      return formatBytes(size, formatLocale);
    },
    [formatLocale, t]
  );

  const downloadErrorMessage = useCallback(
    (err: unknown) => {
      if (err instanceof BackupArtifactDownloadError) {
        switch (err.code) {
          case 'forbidden':
          case 'unauthorized':
            return t('backupDr.download.errorForbidden');
          case 'file_missing':
          case 'not_found':
            return t('backupDr.download.errorNotFound');
          case 'simulated_not_downloadable':
            return t('backupDr.download.errorForbidden');
          default:
            return t('backupDr.download.errorUnknown');
        }
      }
      return t('backupDr.download.errorUnknown');
    },
    [t]
  );

  const handleDownload = useCallback(
    async (target: DownloadTarget) => {
      const key = `${target.backupRunId}:${target.artifactId}`;
      setDownloadingKey(key);
      try {
        await downloadBackupArtifactFile(target.backupRunId, target.artifactId, target.fileName);
      } catch (err) {
        message.error(downloadErrorMessage(err));
      } finally {
        setDownloadingKey(null);
      }
    },
    [downloadErrorMessage, message]
  );

  const renderDownloadButton = useCallback(
    (target: DownloadTarget, downloadUrl: string | null | undefined, label: string) => {
      if (!downloadUrl || !canDownloadBackup) return null;
      const key = `${target.backupRunId}:${target.artifactId}`;
      return (
        <Button
          type="primary"
          size="small"
          icon={<DownloadOutlined />}
          loading={downloadingKey === key}
          onClick={() => void handleDownload(target)}
        >
          {label}
        </Button>
      );
    },
    [canDownloadBackup, downloadingKey, handleDownload]
  );

  const renderBackupCard = useCallback(
    (row: BackupListItemResponseDto) => (
      <div style={{ padding: '4px 0 8px' }}>
        <Typography.Title level={5} style={{ marginTop: 0, marginBottom: 8 }}>
          {t('backupDr.backupList.cardTitle', { fileName: row.fileName ?? '' })}
        </Typography.Title>
        <Space orientation="vertical" size={4}>
          <Typography.Text>
            {t('backupDr.backupList.tenantLabel', { tenant: row.tenantSlug ?? '' })}
          </Typography.Text>
          <Typography.Text type="secondary">
            {t('backupDr.backupList.createdLabel', { date: formatDate(row.createdAt) })}
          </Typography.Text>
          <Typography.Text type="secondary">
            {t('backupDr.backupList.sizeLabel', { size: formatFileSize(row.fileSize) })}
          </Typography.Text>
          {row.manifestFileName ? (
            <Typography.Text type="secondary">
              <FileOutlined style={{ marginRight: 6 }} />
              {t('backupDr.backupList.manifestLabel', { fileName: row.manifestFileName })}
            </Typography.Text>
          ) : null}
        </Space>
        <Space style={{ marginTop: 12 }} wrap>
          {toDownloadTarget(row.backupRunId, row.artifactId, row.fileName)
            ? renderDownloadButton(
                toDownloadTarget(row.backupRunId, row.artifactId, row.fileName)!,
                row.downloadUrl,
                t('backupDr.backupList.downloadDump')
              )
            : null}
          {row.manifestArtifactId && row.manifestFileName
            ? toDownloadTarget(row.backupRunId, row.manifestArtifactId, row.manifestFileName)
              ? renderDownloadButton(
                  toDownloadTarget(row.backupRunId, row.manifestArtifactId, row.manifestFileName)!,
                  row.manifestDownloadUrl,
                  t('backupDr.backupList.downloadManifest')
                )
              : null
            : null}
        </Space>
      </div>
    ),
    [formatDate, formatFileSize, renderDownloadButton, t]
  );

  const columns: ColumnsType<BackupListItemResponseDto> = useMemo(() => {
    const dateColumn: ColumnsType<BackupListItemResponseDto>[number] = {
      title: t('backupDr.backupList.date'),
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 180,
      render: (date: string) => formatDate(date),
      sorter: (a, b) => new Date(a.createdAt ?? 0).getTime() - new Date(b.createdAt ?? 0).getTime(),
      defaultSortOrder: 'descend',
    };

    const sizeColumn: ColumnsType<BackupListItemResponseDto>[number] = {
      title: t('backupDr.backupList.size'),
      dataIndex: 'fileSize',
      key: 'fileSize',
      width: 100,
      render: (size: number | null | undefined) => formatFileSize(size),
    };

    const statusColumn: ColumnsType<BackupListItemResponseDto>[number] = {
      title: t('backupDr.backupList.status'),
      dataIndex: 'status',
      key: 'status',
      width: 150,
      render: (status: number | undefined) => <BackupStatusBadge status={status} />,
    };

    const strategyColumn: ColumnsType<BackupListItemResponseDto>[number] = {
      title: t('backupDr.backupList.strategy'),
      dataIndex: 'strategy',
      key: 'strategy',
      width: 110,
      render: (strategy: BackupListItemResponseDto['strategy']) => {
        const isTenant = strategy === 0 || strategy === 'Tenant';
        return (
          <Tag color={isTenant ? 'blue' : 'purple'}>
            {isTenant
              ? t('backupDr.backupList.strategyTenant')
              : t('backupDr.backupList.strategySystem')}
          </Tag>
        );
      },
    };

    const durationColumn: ColumnsType<BackupListItemResponseDto>[number] = {
      title: t('backupDr.backupList.duration'),
      dataIndex: 'durationFormatted',
      key: 'duration',
      width: 100,
      render: (formatted: string | null | undefined, row) => {
        if (formatted?.trim()) return formatted.trim();
        if (row.durationSeconds != null && row.durationSeconds >= 0) {
          return t('backupDr.runsTable.durationMinutes', {
            minutes: (row.durationSeconds / 60).toFixed(1),
          });
        }
        return t('backupDr.runsTable.noDuration');
      },
    };

    const restoreColumn: ColumnsType<BackupListItemResponseDto>[number] | null = canRestore
      ? {
          title: t('backupDr.manualRestore.table.actions'),
          key: 'restore',
          width: 160,
          render: (_: unknown, row: BackupListItemResponseDto) =>
            row.backupRunId ? (
              <Button
                type="link"
                size="small"
                danger
                icon={<RollbackOutlined />}
                onClick={() =>
                  setRestoreTarget({
                    backupRunId: row.backupRunId!,
                    fileName: row.fileName,
                    tenantSlug: row.tenantSlug,
                  })
                }
              >
                {t('backupDr.manualRestore.table.requestRestore')}
              </Button>
            ) : null,
        }
      : null;

    if (compact) {
      return [
        dateColumn,
        ...(strategyFilter === 'all' ? [strategyColumn] : []),
        sizeColumn,
        statusColumn,
        durationColumn,
        ...(restoreColumn ? [restoreColumn] : []),
      ];
    }

    return [
      {
        title: t('backupDr.backupList.fileName'),
        dataIndex: 'fileName',
        key: 'fileName',
        ellipsis: true,
        render: (name: string, row) => {
          const canDownloadRow = Boolean(row.downloadUrl) && canDownloadBackup;
          if (!canDownloadRow) {
            return (
              <Tooltip
                title={
                  !row.downloadUrl
                    ? t('backupDr.backupList.fileNotOnDisk')
                    : t('backupDr.backupList.noDownloadPermission')
                }
              >
                <Typography.Text strong>{name}</Typography.Text>
              </Tooltip>
            );
          }
          return (
            <Tooltip title={t('backupDr.backupList.downloadTooltip')}>
              <Button
                type="link"
                size="small"
                style={{ padding: 0, height: 'auto', fontWeight: 600 }}
                loading={downloadingKey === `${row.backupRunId}:${row.artifactId}`}
                onClick={() => {
                  const target = toDownloadTarget(row.backupRunId, row.artifactId, row.fileName);
                  if (target) void handleDownload(target);
                }}
              >
                {name}
              </Button>
            </Tooltip>
          );
        },
      },
      {
        title: t('backupDr.backupList.tenant'),
        dataIndex: 'tenantSlug',
        key: 'tenantSlug',
        width: 120,
        render: (slug: string) => <Tag color="blue">{slug || t('backupDr.runsTable.noValue')}</Tag>,
      },
      ...(strategyFilter === 'all' ? [strategyColumn] : []),
      dateColumn,
      sizeColumn,
      statusColumn,
      durationColumn,
      {
        title: t('backupDr.backupList.manifest'),
        key: 'manifest',
        width: 200,
        ellipsis: true,
        render: (_: unknown, row) => {
          if (!row.manifestFileName) return t('backupDr.runsTable.noValue');
          const canDownloadManifest = Boolean(row.manifestDownloadUrl) && canDownloadBackup;
          if (!canDownloadManifest) {
            return (
              <Tooltip title={t('backupDr.backupList.manifestUnavailable')}>
                <Typography.Text type="secondary">{row.manifestFileName}</Typography.Text>
              </Tooltip>
            );
          }
          return (
            <Button
              type="link"
              size="small"
              icon={<FileOutlined />}
              style={{ padding: 0, height: 'auto' }}
              loading={
                row.manifestArtifactId
                  ? downloadingKey === `${row.backupRunId}:${row.manifestArtifactId}`
                  : false
              }
              onClick={() => {
                const target = toDownloadTarget(
                  row.backupRunId,
                  row.manifestArtifactId,
                  row.manifestFileName
                );
                if (target) void handleDownload(target);
              }}
            >
              {row.manifestFileName}
            </Button>
          );
        },
      },
      {
        title: t('backupDr.backupList.type'),
        dataIndex: 'isFake',
        key: 'isFake',
        width: 110,
        render: (isFake: boolean) =>
          isFake ? (
            <Tag color="orange">{t('backupDr.backupList.typeTest')}</Tag>
          ) : (
            <Tag color="green">{t('backupDr.backupList.typeProduction')}</Tag>
          ),
      },
      ...(restoreColumn ? [restoreColumn] : []),
    ];
  }, [
    canDownloadBackup,
    canRestore,
    compact,
    downloadingKey,
    formatDate,
    formatFileSize,
    handleDownload,
    strategyFilter,
    t,
  ]);

  const onRetry = useCallback(async () => {
    if (onRetryInvalidate) await onRetryInvalidate();
    await listQuery.refetch();
  }, [listQuery, onRetryInvalidate]);

  const handleImport = useCallback(async () => {
    if (!dumpFile) {
      message.warning(t('backupDr.backupList.import.dumpRequired'));
      return;
    }
    try {
      const result = await importMutation.mutateAsync({
        data: {
          dumpFile,
          manifestFile: manifestFile ?? undefined,
        },
      });
      message.success(
        t('backupDr.backupList.import.success', { fileName: result.fileName ?? dumpFile.name })
      );
      setDumpFile(null);
      setManifestFile(null);
      await queryClient.invalidateQueries({ queryKey: getGetApiAdminBackupListQueryKey() });
      await listQuery.refetch();
    } catch {
      message.error(t('backupDr.backupList.import.failed'));
    }
  }, [dumpFile, importMutation, listQuery, manifestFile, message, queryClient, t]);

  const rows = useMemo(() => {
    let all = listQuery.data ?? [];
    if (strategyFilter === 'tenant') {
      all = all.filter((r) => r.strategy === 0 || r.strategy === 'Tenant');
    } else if (strategyFilter === 'system') {
      all = all.filter((r) => r.strategy === 1 || r.strategy === 'System');
    }
    if (limit == null || limit <= 0) return all;
    return all.slice(0, limit);
  }, [limit, listQuery.data, strategyFilter]);

  const showImport = canDownloadBackup && !compact;

  if (listQuery.isLoading && !listQuery.data) {
    return <TableSkeleton rows={6} cols={5} loading />;
  }

  return (
    <>
      {canDownloadBackup && !compact ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          title={t('backupDr.backupList.sharedDumpWarningTitle')}
          description={t('backupDr.backupList.sharedDumpWarningDescription')}
        />
      ) : null}
      {showImport ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          title={t('backupDr.backupList.import.title')}
          description={t('backupDr.backupList.import.description')}
        />
      ) : null}
      {showImport ? (
        <Space orientation="vertical" size={12} style={{ width: '100%', marginBottom: 16 }}>
          <Upload.Dragger
            accept=".dump,.sql"
            maxCount={1}
            fileList={dumpFile ? [{ uid: 'dump', name: dumpFile.name, status: 'done' }] : []}
            beforeUpload={(file) => {
              setDumpFile(file);
              return false;
            }}
            onRemove={() => {
              setDumpFile(null);
            }}
          >
            <p className="ant-upload-drag-icon">
              <InboxOutlined />
            </p>
            <p className="ant-upload-text">{t('backupDr.backupList.import.dumpHint')}</p>
          </Upload.Dragger>
          <Upload
            accept=".json"
            maxCount={1}
            fileList={
              manifestFile ? [{ uid: 'manifest', name: manifestFile.name, status: 'done' }] : []
            }
            beforeUpload={(file) => {
              setManifestFile(file);
              return false;
            }}
            onRemove={() => {
              setManifestFile(null);
            }}
          >
            <Button icon={<UploadOutlined />}>
              {t('backupDr.backupList.import.manifestOptional')}
            </Button>
          </Upload>
          <Button
            type="primary"
            icon={<UploadOutlined />}
            loading={importMutation.isPending}
            disabled={!dumpFile}
            onClick={() => void handleImport()}
          >
            {t('backupDr.backupList.import.submit')}
          </Button>
        </Space>
      ) : null}
      {listQuery.isError ? (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 12 }}
          title={t('backupDr.backupList.loadFailed')}
          action={
            <Button type="link" size="small" onClick={() => void onRetry()}>
              {t('backupDr.actions.refresh')}
            </Button>
          }
        />
      ) : null}
      <Table<BackupListItemResponseDto>
        rowKey={(row) => `${row.backupRunId}:${row.artifactId}`}
        size="small"
        loading={listQuery.isFetching}
        dataSource={rows}
        columns={columns}
        expandable={
          compact
            ? undefined
            : {
                expandedRowRender: (row) => renderBackupCard(row),
                rowExpandable: () => true,
              }
        }
        locale={{ emptyText: t('backupDr.backupList.empty') }}
        pagination={compact || limit != null ? false : { pageSize: 20, showSizeChanger: false }}
      />
      {restoreTarget ? (
        <RestoreModal open backup={restoreTarget} onClose={() => setRestoreTarget(null)} />
      ) : null}
    </>
  );
}
