'use client';

/**
 * Yedek pipeline adımları: kaynak `BackupStatusCard` içinde çözülür; sunucu projeksiyonu öncelikli, istemci türetimi yalnızca env ile açıkken.
 */
import { Space, Typography } from 'antd';
import React from 'react';

import type {
  DerivedPipelineStep,
  DerivedPipelineStepState,
} from '@/features/backup-dr/logic/backupPipelineDerived';

function stepIcon(state: DerivedPipelineStepState): string {
  switch (state) {
    case 'success':
      return '✔️';
    case 'failed':
      return '❌';
    case 'degraded':
      return '⚠️';
    case 'running':
      return '⏳';
    case 'skipped':
      return '⊘';
    default:
      return '…';
  }
}

function stateLabel(state: DerivedPipelineStepState, t: (k: string) => string): string {
  const k = `backupDr.pipelineSteps.state.${state}`;
  const x = t(k);
  return x === k ? state : x;
}

export interface BackupPipelineStepperProps {
  steps: DerivedPipelineStep[];
  t: (k: string) => string;
}

export function BackupPipelineStepper({ steps, t }: BackupPipelineStepperProps) {
  if (!steps.length) return null;

  return (
    <Space orientation="vertical" size={10} style={{ width: '100%' }}>
      {steps.map((s) => (
        <div key={s.id} style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
          <Typography.Text aria-hidden style={{ fontSize: 16, lineHeight: 1.5, flexShrink: 0 }}>
            {stepIcon(s.state)}
          </Typography.Text>
          <div style={{ flex: 1, minWidth: 0 }}>
            <Typography.Text strong>{t(s.titleKey)}</Typography.Text>
            <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
              ({stateLabel(s.state, t)})
            </Typography.Text>
            {s.hintKey ? (
              <div>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {t(s.hintKey)}
                </Typography.Text>
              </div>
            ) : null}
          </div>
        </div>
      ))}
    </Space>
  );
}
