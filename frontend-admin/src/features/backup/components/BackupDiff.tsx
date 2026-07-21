'use client';

/**
 * Side-by-side comparison of two backup runs (logical dump TOC presence + artifact size).
 */
import { Alert, Card, Checkbox, Descriptions, Select, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import React, { useMemo, useState } from 'react';

import { CardSkeleton } from '@/components/Skeleton';
import { formatBackupBytes } from '@/features/backup-dr/logic/backupFormat';
import { useBackupDiff } from '@/features/backup/hooks/useBackupDiff';
import {
  type BackupDiffRow,
  filterBackupDiffRows,
} from '@/features/backup/logic/backupDiffPresentation';
import { useI18n } from '@/i18n';

export type BackupDiffRunOption = {
  value: string;
  label: string;
};

export type BackupDiffProps = {
  backup1Id?: string | null;
  backup2Id?: string | null;
  /** When set, show Selects to pick runs (controlled via local state if ids omitted). */
  runOptions?: BackupDiffRunOption[];
  enabled?: boolean;
  size?: 'default' | 'small';
};

export function BackupDiff({
  backup1Id: backup1IdProp = null,
  backup2Id: backup2IdProp = null,
  runOptions,
  enabled = true,
  size = 'small',
}: BackupDiffProps) {
  const { t } = useI18n();
  const [local1, setLocal1] = useState<string | undefined>(backup1IdProp ?? undefined);
  const [local2, setLocal2] = useState<string | undefined>(backup2IdProp ?? undefined);
  const [onlyChanged, setOnlyChanged] = useState(true);

  const backup1Id = backup1IdProp ?? local1 ?? null;
  const backup2Id = backup2IdProp ?? local2 ?? null;
  const selectable = Boolean(runOptions?.length);

  const {
    data: diff,
    sameId,
    isLoading,
    isError,
  } = useBackupDiff(backup1Id, backup2Id, {
    enabled: enabled && Boolean(backup1Id && backup2Id),
  });

  const rows = useMemo(
    () => (diff ? filterBackupDiffRows(diff.differences, onlyChanged) : []),
    [diff, onlyChanged]
  );

  const columns: ColumnsType<BackupDiffRow> = useMemo(
    () => [
      {
        title: t('backupDr.backupDiff.columns.table'),
        dataIndex: 'table',
        key: 'table',
      },
      {
        title: t('backupDr.backupDiff.columns.backup1'),
        dataIndex: 'count1',
        key: 'count1',
        render: (v: number) =>
          v === 1 ? t('backupDr.backupDiff.presence.yes') : t('backupDr.backupDiff.presence.no'),
      },
      {
        title: t('backupDr.backupDiff.columns.backup2'),
        dataIndex: 'count2',
        key: 'count2',
        render: (v: number) =>
          v === 1 ? t('backupDr.backupDiff.presence.yes') : t('backupDr.backupDiff.presence.no'),
      },
      {
        title: t('backupDr.backupDiff.columns.diff'),
        dataIndex: 'diff',
        key: 'diff',
        render: (v: number, row: BackupDiffRow) => {
          if (v === 0) return t('backupDr.backupDiff.diff.same');
          if (row.onlyInBackup1) return t('backupDr.backupDiff.diff.only1');
          if (row.onlyInBackup2) return t('backupDr.backupDiff.diff.only2');
          return String(v);
        },
      },
    ],
    [t]
  );

  return (
    <Card size={size} title={t('backupDr.backupDiff.cardTitle')}>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 12 }}
        title={t('backupDr.backupDiff.alertTitle')}
        description={t('backupDr.backupDiff.alertDescription')}
      />

      {selectable ? (
        <Space wrap style={{ marginBottom: 12, width: '100%' }}>
          <Select
            style={{ minWidth: 220 }}
            placeholder={t('backupDr.backupDiff.selectBackup1')}
            options={runOptions}
            value={backup1Id ?? undefined}
            onChange={(v) => setLocal1(v)}
            allowClear
            showSearch
            optionFilterProp="label"
          />
          <Select
            style={{ minWidth: 220 }}
            placeholder={t('backupDr.backupDiff.selectBackup2')}
            options={runOptions}
            value={backup2Id ?? undefined}
            onChange={(v) => setLocal2(v)}
            allowClear
            showSearch
            optionFilterProp="label"
          />
        </Space>
      ) : null}

      {sameId ? (
        <Alert
          type="warning"
          showIcon
          title={t('backupDr.backupDiff.sameRun')}
          style={{ marginBottom: 12 }}
        />
      ) : null}

      {!backup1Id || !backup2Id ? (
        <Typography.Text type="secondary">{t('backupDr.backupDiff.pickTwo')}</Typography.Text>
      ) : null}

      {isLoading ? <CardSkeleton count={1} loading /> : null}

      {isError ? <Alert type="error" showIcon title={t('backupDr.backupDiff.loadFailed')} /> : null}

      {diff ? (
        <>
          {(!diff.dump1Analyzed || !diff.dump2Analyzed) && (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 12 }}
              title={t('backupDr.backupDiff.dumpNotAnalyzed')}
              description={t('backupDr.backupDiff.dumpNotAnalyzedDetail')}
            />
          )}

          <Descriptions bordered size="small" column={1} style={{ marginBottom: 12 }}>
            <Descriptions.Item label={t('backupDr.backupDiff.labels.size1')}>
              {formatBackupBytes(diff.sizeBytes1, t)}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.backupDiff.labels.size2')}>
              {formatBackupBytes(diff.sizeBytes2, t)}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.backupDiff.labels.sizeDiff')}>
              {formatBackupBytes(Math.abs(diff.sizeDiffBytes), t)}
              {diff.sizeDiffBytes !== 0 ? (
                <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
                  {diff.sizeDiffBytes > 0
                    ? t('backupDr.backupDiff.sizeLarger1')
                    : t('backupDr.backupDiff.sizeLarger2')}
                </Typography.Text>
              ) : null}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.backupDiff.labels.changedTables')}>
              {diff.changedCount}
            </Descriptions.Item>
          </Descriptions>

          <Checkbox
            checked={onlyChanged}
            onChange={(e) => setOnlyChanged(e.target.checked)}
            style={{ marginBottom: 8 }}
          >
            {t('backupDr.backupDiff.onlyChanged')}
          </Checkbox>

          <Table<BackupDiffRow>
            rowKey="key"
            dataSource={rows}
            columns={columns}
            pagination={false}
            size="small"
            locale={{ emptyText: t('backupDr.backupDiff.empty') }}
            scroll={rows.length > 10 ? { y: 320 } : undefined}
          />
        </>
      ) : null}
    </Card>
  );
}
