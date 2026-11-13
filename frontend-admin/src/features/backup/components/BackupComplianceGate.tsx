'use client';

import { Card, Col, Row, Tag, Tooltip, Typography } from 'antd';
import React from 'react';

import type { GateColor } from '@/features/backup/logic/backupDrInsights';
import { useI18n } from '@/i18n';

const TAG_COLOR: Record<GateColor, string> = {
  green: 'success',
  yellow: 'warning',
  red: 'error',
};

export type ComplianceGateItem = {
  id: string;
  label: string;
  detail: string;
  color: GateColor;
  /** Accepted open gap: the tag stays amber and does not become green. */
  accepted?: boolean;
  /** Shown as the tag tooltip and as a short caption. */
  hint?: string;
};

export type KnownGapCardRow = {
  id: string;
  label: string;
  status: 'amber' | 'red' | 'green';
  note?: string;
};

export function BackupComplianceGate({
  gates,
  knownGaps = [],
  verdict = null,
}: {
  gates: ComplianceGateItem[];
  knownGaps?: KnownGapCardRow[];
  verdict?: 'pass' | 'fail' | null;
}) {
  const { t } = useI18n();
  const colorLabel = (color: GateColor) => {
    switch (color) {
      case 'green':
        return t('backupDr.compliance.gate.green');
      case 'yellow':
        return t('backupDr.compliance.gate.yellow');
      case 'red':
        return t('backupDr.compliance.gate.red');
      default:
        return color;
    }
  };
  const rowLabel = (status: KnownGapCardRow['status']) => {
    if (status === 'green') return t('backupDr.compliance.gate.green');
    if (status === 'amber') return t('backupDr.compliance.gate.yellow');
    return t('backupDr.compliance.gate.red');
  };

  return (
    <Card size="small" title={t('backupDr.compliance.gate.title')} style={{ marginTop: 16 }}>
      {verdict ? (
        <div data-testid="backup-known-gap-verdict" data-verdict={verdict} style={{ marginBottom: 12 }}>
          <Tag color={verdict === 'pass' ? 'success' : 'error'}>
            {verdict === 'pass'
              ? t('backupDr.knownGapAcceptance.verdictPass')
              : t('backupDr.knownGapAcceptance.verdictFail')}
          </Tag>
        </div>
      ) : null}
      <Row gutter={[16, 16]}>
        {gates.map((gate) => {
          const status = gate.accepted && gate.color === 'yellow' ? 'amber' : gate.color;
          return (
            <Col xs={24} md={8} key={gate.id}>
              <div data-testid={`backup-gate-${gate.id}`} data-status={status}>
                <Typography.Text type="secondary">{gate.label}</Typography.Text>
                <div style={{ marginTop: 8 }}>
                  <Tooltip title={gate.hint}>
                    <Tag color={status === 'amber' ? 'warning' : TAG_COLOR[gate.color]}>
                      {status === 'amber' ? t('backupDr.compliance.gate.yellow') : colorLabel(gate.color)}
                    </Tag>
                  </Tooltip>
                </div>
                <div>{gate.detail}</div>
                {gate.hint ? (
                  <Typography.Paragraph
                    type="secondary"
                    style={{ marginBottom: 0, marginTop: 4, fontSize: 12 }}
                  >
                    {gate.hint}
                  </Typography.Paragraph>
                ) : null}
              </div>
            </Col>
          );
        })}
      </Row>
      {knownGaps.length > 0 ? (
        <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
          {knownGaps.map((gap) => (
            <Col xs={24} md={12} key={gap.id}>
              <div data-testid={`backup-known-gap-${gap.id}`} data-status={gap.status}>
                <Typography.Text>{gap.label}</Typography.Text>
                <div style={{ marginTop: 8 }}>
                  <Tag color={gap.status === 'amber' ? 'warning' : gap.status === 'green' ? 'success' : 'error'}>
                    {rowLabel(gap.status)}
                  </Tag>
                </div>
                {gap.note ? <div>{gap.note}</div> : null}
              </div>
            </Col>
          ))}
        </Row>
      ) : null}
    </Card>
  );
}
