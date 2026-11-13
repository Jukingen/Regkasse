'use client';

import { Typography } from 'antd';
import dayjs from 'dayjs';

import type { TseTrainingConsoleEntry } from '@/features/tse-training/types';
import { useI18n } from '@/i18n/I18nProvider';

type Props = {
  entries: TseTrainingConsoleEntry[];
  loading?: boolean;
};

function levelColor(level: string): string {
  const l = level.toLowerCase();
  if (l === 'error') return '#cf1322';
  if (l === 'warn' || l === 'warning') return '#d48806';
  return '#389e0d';
}

export function SimulationConsole({ entries, loading }: Props) {
  const { t } = useI18n();

  return (
    <div
      className="rounded border border-neutral-200 bg-neutral-950 p-3 font-mono text-xs text-neutral-100"
      style={{ minHeight: 180, maxHeight: 320, overflowY: 'auto' }}
      aria-busy={loading || undefined}
    >
      {entries.length === 0 ? (
        <Typography.Text style={{ color: '#8c8c8c' }}>
          {t('tseTraining.consoleEmpty')}
        </Typography.Text>
      ) : (
        entries.map((e) => (
          <div key={e.id} style={{ marginBottom: 6 }}>
            <span style={{ color: '#8c8c8c' }}>
              [{dayjs(e.timestampUtc).format('HH:mm:ss')}]
            </span>{' '}
            <span style={{ color: levelColor(e.level) }}>{e.scenario}</span>{' '}
            <span>{e.message}</span>
          </div>
        ))
      )}
    </div>
  );
}
