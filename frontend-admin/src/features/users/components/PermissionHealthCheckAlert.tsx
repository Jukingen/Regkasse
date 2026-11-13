'use client';

import { Alert, Button, Space, Typography } from 'antd';
import React, { useMemo } from 'react';

import {
  analyzePermissionHealth,
  type PermissionHealthIssue,
} from '@/features/users/utils/permissionHealthCheck';
import { useI18n } from '@/i18n';

export type PermissionHealthCheckAlertProps = {
  granted: Iterable<string>;
  catalogSize: number;
  catalogKeys?: Set<string> | string[];
  allowPlatformCritical?: boolean;
  onApplySuggestedPreset?: (presetId: string) => void;
};

function issueType(severity: PermissionHealthIssue['severity']): 'error' | 'warning' | 'info' {
  return severity;
}

/**
 * Surfaces missing critical companions, overly broad grants, and preset suggestions.
 */
export function PermissionHealthCheckAlert({
  granted,
  catalogSize,
  catalogKeys,
  allowPlatformCritical = false,
  onApplySuggestedPreset,
}: PermissionHealthCheckAlertProps) {
  const { t } = useI18n();

  const report = useMemo(
    () =>
      analyzePermissionHealth({
        granted,
        catalogSize,
        catalogKeys,
        allowPlatformCritical,
      }),
    [granted, catalogSize, catalogKeys, allowPlatformCritical]
  );

  if (report.issues.length === 0) return null;

  const primary =
    report.issues.find((i) => i.severity === 'error') ??
    report.issues.find((i) => i.severity === 'warning') ??
    report.issues[0]!;

  return (
    <Alert
      type={issueType(primary.severity)}
      showIcon
      style={{ marginBottom: 12 }}
      title={t('users.permissionOnboarding.healthTitle')}
      description={
        <Space orientation="vertical" size={4} style={{ width: '100%' }}>
          {report.issues.map((issue) => (
            <Typography.Text key={issue.id} style={{ fontSize: 12, display: 'block' }}>
              {t(issue.messageKey, issue.messageParams)}
              {issue.suggestedPresetId && onApplySuggestedPreset ? (
                <>
                  {' '}
                  <Button
                    type="link"
                    size="small"
                    style={{ padding: 0, height: 'auto' }}
                    onClick={() => onApplySuggestedPreset(issue.suggestedPresetId!)}
                  >
                    {t('users.permissionOnboarding.healthApplySuggestion')}
                  </Button>
                </>
              ) : null}
            </Typography.Text>
          ))}
        </Space>
      }
    />
  );
}
