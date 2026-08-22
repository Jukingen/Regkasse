'use client';

import { Button, Card, Empty, Table } from 'antd';

import { DEV_LIMIT_LOG_ACTION_KEYS } from '@/features/dev-limits/constants/limitKeys';
import type { LimitEventLogEntry } from '@/features/dev-limits/hooks/useLimitEventLog';
import { formatGermanDateTime, useI18n } from '@/i18n';

type LimitEventLogProps = {
  entries: LimitEventLogEntry[];
  onClear: () => void;
};

export function LimitEventLog({ entries, onClear }: LimitEventLogProps) {
  const { t } = useI18n();

  return (
    <Card
      title={t('tenants.limits.devPanel.eventLogTitle')}
      extra={
        <Button size="small" disabled={entries.length === 0} onClick={onClear}>
          {t('tenants.limits.devPanel.logClear')}
        </Button>
      }
    >
      {entries.length === 0 ? (
        <Empty description={t('tenants.limits.devPanel.logEmpty')} />
      ) : (
        <Table<LimitEventLogEntry>
          size="small"
          rowKey="id"
          pagination={false}
          dataSource={entries}
          columns={[
            {
              title: t('tenants.limits.devPanel.logTime'),
              dataIndex: 'atIso',
              width: 200,
              render: (iso: string) => formatGermanDateTime(iso),
            },
            {
              title: t('tenants.limits.devPanel.logAction'),
              dataIndex: 'action',
              width: 140,
              render: (action: LimitEventLogEntry['action']) => t(DEV_LIMIT_LOG_ACTION_KEYS[action]),
            },
            {
              title: t('tenants.limits.devPanel.logDetail'),
              dataIndex: 'detail',
            },
          ]}
        />
      )}
    </Card>
  );
}
