'use client';

/**
 * Kanıt özeti (L0–L6) + uygulama/dış bağımlılık özet satırları (model.labelKey); anlam drProofLevelPresentation’dan gelir.
 */
import { Space, Tag, Typography } from 'antd';
import React from 'react';

import type {
  DrProofLayerRow,
  DrProofPresentationModel,
} from '@/features/backup-dr/logic/drProofLevelPresentation';

export interface BackupDrProofSummaryLayersProps {
  model: DrProofPresentationModel;
  t: (key: string, options?: Record<string, string | number>) => string;
}

function layerStateColor(state: DrProofLayerRow['state']): string {
  switch (state) {
    case 'proven':
      return 'green';
    case 'partial':
      return 'orange';
    case 'gap':
      return 'red';
    case 'stub_only':
      return 'default';
    case 'not_applicable':
      return 'blue';
    default:
      return 'default';
  }
}

function layerStateLabelKey(state: DrProofLayerRow['state']): string {
  const map: Record<DrProofLayerRow['state'], string> = {
    proven: 'backupDr.confidenceDashboard.layerState.proven',
    partial: 'backupDr.confidenceDashboard.layerState.partial',
    gap: 'backupDr.confidenceDashboard.layerState.gap',
    stub_only: 'backupDr.confidenceDashboard.layerState.stub_only',
    not_applicable: 'backupDr.confidenceDashboard.layerState.not_applicable',
    unknown: 'backupDr.confidenceDashboard.layerState.unknown',
  };
  return map[state];
}

export function BackupDrProofSummaryLayers({ model, t }: BackupDrProofSummaryLayersProps) {
  const l = model.layers;

  return (
    <Space orientation="vertical" size={10} style={{ width: '100%' }}>
      {l.map((row) => (
        <div
          key={row.id}
          style={{
            display: 'flex',
            gap: 8,
            alignItems: 'flex-start',
            flexWrap: 'wrap',
          }}
        >
          <Tag color={layerStateColor(row.state)}>{t(layerStateLabelKey(row.state))}</Tag>
          <div>
            <Typography.Text strong>{t(row.titleKey)}</Typography.Text>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0, marginTop: 4 }}>
              {t(row.detailKey)}
            </Typography.Paragraph>
          </div>
        </div>
      ))}
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
        {t(model.appRecoverySummary.labelKey)}
      </Typography.Paragraph>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
        {t(model.externalDepsSummary.labelKey)}
      </Typography.Paragraph>
    </Space>
  );
}
