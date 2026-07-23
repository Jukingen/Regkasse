'use client';

import { Alert, Button, Modal, Space, Typography } from 'antd';
import React, { useMemo, useState } from 'react';

import {
  analyzeMenuPermissionConsistency,
  buildConsistencyFixSuggestions,
  formatConsistencySummaryLines,
  type ConsistencyReport,
} from '@/features/users/utils/menuPermissionConsistency';
import { useI18n } from '@/i18n';

export type MenuPermissionConsistencyButtonProps = {
  /** Full permission catalog keys from API when available. */
  catalogKeys?: readonly string[];
};

/**
 * Toolbar control: run menu↔permission consistency analysis and show results + fix suggestions.
 */
export function MenuPermissionConsistencyButton({
  catalogKeys,
}: MenuPermissionConsistencyButtonProps) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const [report, setReport] = useState<ConsistencyReport | null>(null);

  const fixes = useMemo(
    () => (report ? buildConsistencyFixSuggestions(report) : []),
    [report]
  );

  const runCheck = () => {
    const next = analyzeMenuPermissionConsistency(catalogKeys);
    setReport(next);
    setOpen(true);
  };

  return (
    <>
      <Button size="small" onClick={runCheck}>
        {t('users.roleDrawer.consistencyCheckButton')}
      </Button>
      <Modal
        title={t('users.roleDrawer.consistencyCheckTitle')}
        open={open}
        onCancel={() => setOpen(false)}
        footer={
          <Button type="primary" onClick={() => setOpen(false)}>
            {t('common.buttons.close')}
          </Button>
        }
        width={640}
        destroyOnHidden
      >
        {report ? (
          <Space direction="vertical" size={12} style={{ width: '100%' }}>
            <Alert
              type={report.summary.error > 0 ? 'error' : report.summary.warning > 0 ? 'warning' : 'success'}
              showIcon
              message={t('users.roleDrawer.consistencyOkMenus', { count: report.okMenus })}
              description={
                report.summary.warning + report.summary.error > 0
                  ? t('users.roleDrawer.consistencyIssueCounts', {
                      warning: report.summary.warning,
                      error: report.summary.error,
                    })
                  : undefined
              }
            />
            <pre
              style={{
                margin: 0,
                padding: 10,
                background: 'rgba(0,0,0,0.04)',
                borderRadius: 6,
                fontSize: 12,
                whiteSpace: 'pre-wrap',
              }}
            >
              {formatConsistencySummaryLines(report).join('\n')}
            </pre>
            {report.issues.length === 0 ? (
              <Typography.Text type="secondary">
                {t('users.roleDrawer.consistencyAllGood')}
              </Typography.Text>
            ) : (
              <ul style={{ margin: 0, paddingLeft: 18, maxHeight: 280, overflow: 'auto' }}>
                {report.issues.map((issue) => (
                  <li key={issue.id} style={{ marginBottom: 6, fontSize: 13 }}>
                    {issue.severity === 'error' ? '❌' : '⚠️'}{' '}
                    {t(`users.roleDrawer.consistencyKind.${issue.kind}`, {
                      subject: issue.subject,
                      detail: issue.detail ?? '',
                    })}
                    {issue.suggestedKeys?.length ? (
                      <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
                        → {issue.suggestedKeys.join(', ')}
                      </Typography.Text>
                    ) : null}
                  </li>
                ))}
              </ul>
            )}
            {fixes.length > 0 ? (
              <div>
                <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
                  {t('users.roleDrawer.consistencyFixTitle')}
                </Typography.Text>
                <Typography.Paragraph type="secondary" style={{ fontSize: 12 }}>
                  {t('users.roleDrawer.consistencyFixHint')}
                </Typography.Paragraph>
                {fixes.map((fix) => (
                  <div key={fix.issueId} style={{ marginBottom: 10 }}>
                    <Typography.Text style={{ fontSize: 12 }}>{fix.title}</Typography.Text>
                    <pre
                      style={{
                        margin: '4px 0 0',
                        padding: 8,
                        background: 'rgba(0,0,0,0.04)',
                        borderRadius: 6,
                        fontSize: 11,
                        overflow: 'auto',
                      }}
                    >
                      {fix.mappingSnippet}
                    </pre>
                    <Button
                      size="small"
                      type="link"
                      onClick={() => {
                        void navigator.clipboard?.writeText(fix.mappingSnippet);
                      }}
                    >
                      {t('users.roleDrawer.consistencyCopyFix')}
                    </Button>
                  </div>
                ))}
              </div>
            ) : null}
          </Space>
        ) : null}
      </Modal>
    </>
  );
}
