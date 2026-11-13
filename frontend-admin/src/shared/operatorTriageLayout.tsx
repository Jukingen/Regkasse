'use client';

/**
 * Reusable three-layer framing for operator-first triage (summary → business record → technical).
 */
import { Typography } from 'antd';
import React from 'react';

import { OPERATOR_TRIAGE_COPY } from '@/shared/operatorTruthCopy';

export function OperatorSummaryStrip({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        background: '#fafafa',
        border: '1px solid #f0f0f0',
        borderRadius: 8,
        padding: 12,
        marginBottom: 16,
      }}
    >
      <Typography.Text
        type="secondary"
        style={{
          fontSize: 11,
          letterSpacing: 0.4,
          textTransform: 'uppercase',
          display: 'block',
          marginBottom: 8,
        }}
      >
        {OPERATOR_TRIAGE_COPY.summaryStripLabel}
      </Typography.Text>
      {children}
    </div>
  );
}

export function OperatorBusinessSection({
  title = OPERATOR_TRIAGE_COPY.businessDefaultTitle,
  description,
  children,
}: {
  title?: string;
  description?: string;
  children: React.ReactNode;
}) {
  return (
    <div style={{ marginBottom: 20 }}>
      <Typography.Title level={2} style={{ marginBottom: description ? 6 : 12 }}>
        {title}
      </Typography.Title>
      {description ? (
        <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginBottom: 12 }}>
          {description}
        </Typography.Paragraph>
      ) : null}
      {children}
    </div>
  );
}

export function OperatorTechnicalSection({ children }: { children: React.ReactNode }) {
  return (
    <div style={{ marginTop: 4 }}>
      <Typography.Title level={2} style={{ marginBottom: 8 }}>
        {OPERATOR_TRIAGE_COPY.technicalTitle}
      </Typography.Title>
      <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginBottom: 12 }}>
        {OPERATOR_TRIAGE_COPY.technicalIntro}
      </Typography.Paragraph>
      {children}
    </div>
  );
}
