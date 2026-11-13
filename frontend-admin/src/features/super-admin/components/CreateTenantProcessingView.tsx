'use client';

/**
 * Phase 2: animated provisioning checklist while tenant create API runs.
 */
import { CheckCircleFilled, CloseCircleFilled, LoadingOutlined } from '@ant-design/icons';
import { Alert, Typography } from 'antd';
import React from 'react';

import type {
  TenantOnboardingStepDefinition,
  TenantOnboardingStepStatus,
} from '@/features/super-admin/lib/tenantOnboardingSteps';
import { useI18n } from '@/i18n';
import styles from '@/styles/tenant-form.module.css';

export type CreateTenantProcessingViewProps = {
  definitions: TenantOnboardingStepDefinition[];
  statuses: Record<string, TenantOnboardingStepStatus>;
  companyName: string;
  slug: string;
  baseDomain: string;
  errorMessage?: string;
  phase: 'running' | 'success' | 'error';
};

function StepIcon({ status }: { status: TenantOnboardingStepStatus }) {
  if (status === 'done') {
    return <CheckCircleFilled className={styles.processingIconDone} aria-hidden />;
  }
  if (status === 'error') {
    return <CloseCircleFilled className={styles.processingIconError} aria-hidden />;
  }
  if (status === 'active') {
    return <LoadingOutlined spin className={styles.processingIconActive} aria-hidden />;
  }
  return <span className={styles.processingIconPending} aria-hidden />;
}

export function CreateTenantProcessingView({
  definitions,
  statuses,
  companyName,
  slug,
  baseDomain,
  errorMessage,
  phase,
}: CreateTenantProcessingViewProps) {
  const { t } = useI18n();

  return (
    <div className={styles.processingRoot}>
      <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
        {phase === 'error'
          ? t('tenants.create.processing.subtitleError')
          : t('tenants.create.processing.subtitle')}
      </Typography.Paragraph>

      <ul className={styles.processingList} role="list" aria-live="polite">
        {definitions.map((def) => {
          const status = statuses[def.id] ?? 'pending';
          const lineKey =
            status === 'done' ? def.doneKey : status === 'error' ? def.labelKey : def.labelKey;

          const values: Record<string, string | number> | undefined =
            def.id === 'company'
              ? { name: companyName }
              : def.id === 'subdomain'
                ? { slug, domain: baseDomain }
                : undefined;

          return (
            <li key={def.id} className={styles.processingItem}>
              <StepIcon status={status} />
              <span
                className={
                  status === 'done'
                    ? styles.processingTextDone
                    : status === 'active'
                      ? styles.processingTextActive
                      : status === 'error'
                        ? styles.processingTextError
                        : styles.processingTextPending
                }
              >
                {t(lineKey, values)}
              </span>
            </li>
          );
        })}
      </ul>

      {phase === 'running' ? (
        <Alert
          type="info"
          showIcon
          style={{ marginTop: 16 }}
          title={t('tenants.create.processing.rollbackHint')}
        />
      ) : null}

      {phase === 'error' && errorMessage ? (
        <Alert
          type="error"
          showIcon
          style={{ marginTop: 16 }}
          title={t('tenants.create.processing.failedTitle')}
          description={errorMessage}
        />
      ) : null}
    </div>
  );
}
