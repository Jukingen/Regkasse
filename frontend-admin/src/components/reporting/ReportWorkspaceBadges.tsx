'use client';

import React from 'react';
import { Tag, Space } from 'antd';
import {
  documentStatusVisual,
  submissionLifecycleVisual,
  type AntTagColor,
} from '@/components/reporting/reportWorkspaceLabels';

type TFn = (key: string) => string;

function tag(color: AntTagColor, text: string) {
  return (
    <Tag color={color} style={{ marginInlineEnd: 0 }}>
      {text}
    </Tag>
  );
}

/** Dokumentstatus (Entwurf / fertig / ersetzt). */
export function ReportDocumentBadge({ status, t }: { status: string | undefined; t: TFn }) {
  const v = documentStatusVisual(status);
  return tag(v.color, t(`adminShell.reporting.reportCenter.${v.labelKey}`));
}

/** FinanzOnline-Meldung — verständliche Kurzlabels. */
export function ReportSubmissionBadge({ lifecycle, t }: { lifecycle: string | undefined; t: TFn }) {
  const v = submissionLifecycleVisual(lifecycle);
  return tag(v.color, t(`adminShell.reporting.reportCenter.${v.labelKey}`));
}

export function ReportDualBadges({
  reportStatus,
  lifecycle,
  t,
}: {
  reportStatus: string | undefined;
  lifecycle: string | undefined;
  t: TFn;
}) {
  return (
    <Space size={4} wrap>
      <ReportDocumentBadge status={reportStatus} t={t} />
      <ReportSubmissionBadge lifecycle={lifecycle} t={t} />
    </Space>
  );
}
