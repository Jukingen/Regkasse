'use client';

import { Alert, Card, Empty, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';

import type { BackupChainItem, BackupChainResponse } from '@/features/backup/logic/backupPitrApi';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';

export interface BackupChainProps {
  chain: BackupChainResponse | undefined;
  loading?: boolean;
}

export function BackupChain({ chain, loading }: BackupChainProps) {
  const { t, formatLocale } = useI18n();

  const rows: BackupChainItem[] = [];
  if (chain?.fullBackup) rows.push(chain.fullBackup);
  if (chain?.incrementals?.length) rows.push(...chain.incrementals);
  if (chain?.systemBackups?.length) rows.push(...chain.systemBackups);

  const kindColor = (kind: string) => {
    if (kind === 'full') return 'blue';
    if (kind === 'incremental') return 'cyan';
    if (kind === 'system') return 'purple';
    return 'default';
  };

  const columns: ColumnsType<BackupChainItem> = [
    {
      title: t('backupDr.pitrPage.chainKind'),
      dataIndex: 'packageKind',
      key: 'packageKind',
      render: (kind: string) => <Tag color={kindColor(kind)}>{t(`backupDr.pitrPage.kind.${kind}`)}</Tag>,
    },
    {
      title: t('backupDr.pitrPage.chainCompleted'),
      dataIndex: 'completedAtUtc',
      key: 'completedAtUtc',
      render: (iso: string | null) => (iso ? formatDateTime(iso, formatLocale) : '—'),
    },
    {
      title: t('backupDr.pitrPage.chainTenant'),
      key: 'tenant',
      render: (_, row) => row.tenantSlug ?? row.tenantId ?? '—',
    },
    {
      title: t('backupDr.pitrPage.chainWal'),
      dataIndex: 'coveredByWal',
      key: 'coveredByWal',
      render: (covered: boolean) =>
        covered ? (
          <Tag color="green">{t('backupDr.pitrPage.walCovered')}</Tag>
        ) : (
          <Tag>{t('backupDr.pitrPage.walNotCovered')}</Tag>
        ),
    },
    {
      title: t('backupDr.pitrPage.chainRunId'),
      dataIndex: 'runId',
      key: 'runId',
      ellipsis: true,
    },
  ];

  return (
    <Card title={t('backupDr.pitrPage.chainTitle')} loading={loading}>
      <Space direction="vertical" size={12} style={{ width: '100%' }}>
        {chain?.message ? (
          <Alert type="info" showIcon title={t('backupDr.pitrPage.chainNote')} description={chain.message} />
        ) : null}
        <Typography.Text type="secondary">
          {t('backupDr.pitrPage.walWindow', {
            start: chain?.walCoverageStartUtc
              ? formatDateTime(chain.walCoverageStartUtc, formatLocale)
              : '—',
            end: chain?.walCoverageEndUtc
              ? formatDateTime(chain.walCoverageEndUtc, formatLocale)
              : '—',
            count: String(chain?.walFileCount ?? 0),
          })}
        </Typography.Text>
        {rows.length === 0 ? (
          <Empty description={t('backupDr.pitrPage.chainEmpty')} />
        ) : (
          <Table<BackupChainItem>
            rowKey="runId"
            size="small"
            pagination={false}
            columns={columns}
            dataSource={rows}
          />
        )}
      </Space>
    </Card>
  );
}
