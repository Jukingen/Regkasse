'use client';

import { Alert, Descriptions, Typography } from 'antd';
import React from 'react';

import { buildTenantPortalUrl } from '@/features/super-admin/api/adminTenants';
import type { CreateTenantWizardData } from '@/features/super-admin/components/CreateTenantWizard/types';
import { useI18n } from '@/i18n';

export type Step4SummaryProps = {
  data: CreateTenantWizardData;
};

export function Step4Summary({ data }: Step4SummaryProps) {
  const { t } = useI18n();
  const portalUrl = data.slug ? buildTenantPortalUrl(data.slug) : '—';
  const passwordDisplay =
    data.passwordMode === 'auto' && !data.adminPassword.trim()
      ? t('tenants.create.wizard.summary.passwordServerGenerated')
      : data.adminPassword || '—';

  return (
    <div>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        {t('tenants.create.wizard.summary.intro')}
      </Typography.Paragraph>

      <Descriptions
        title={t('tenants.create.wizard.summary.title')}
        bordered
        size="small"
        column={1}
      >
        <Descriptions.Item label={t('tenants.create.fields.name.label')}>
          {data.name || '—'}
        </Descriptions.Item>
        <Descriptions.Item label={t('tenants.create.fields.slug.label')}>
          {data.slug || '—'}
        </Descriptions.Item>
        <Descriptions.Item label={t('tenants.create.fields.contactEmail.label')}>
          {data.email || '—'}
        </Descriptions.Item>
        {data.phone?.trim() ? (
          <Descriptions.Item label={t('tenants.create.fields.phone.label')}>
            {data.phone.trim()}
          </Descriptions.Item>
        ) : null}
        {data.address?.trim() ? (
          <Descriptions.Item label={t('tenants.create.fields.address.label')}>
            {data.address.trim()}
          </Descriptions.Item>
        ) : null}
        <Descriptions.Item label={t('tenants.create.wizard.fields.adminEmail')}>
          {data.adminEmail || '—'}
        </Descriptions.Item>
        <Descriptions.Item label={t('tenants.create.wizard.fields.role')}>
          {t('tenants.create.wizard.fields.roleValue')}
        </Descriptions.Item>
        <Descriptions.Item label={t('tenants.create.wizard.fields.adminPassword')}>
          {passwordDisplay}
        </Descriptions.Item>
        <Descriptions.Item label={t('tenants.create.wizard.fields.registerName')}>
          {data.registerNumber || '—'}
        </Descriptions.Item>
        <Descriptions.Item label={t('tenants.create.wizard.fields.licenseType')}>
          {t('tenants.create.wizard.fields.licenseDaysOption', { days: data.licenseDays })}
        </Descriptions.Item>
        <Descriptions.Item label={t('tenants.create.wizard.fields.licenseStartDate')}>
          {data.licenseStartDate || '—'}
        </Descriptions.Item>
        <Descriptions.Item label={t('tenants.create.wizard.fields.createDemoProductsLabel')}>
          {data.importDemoProducts
            ? t('tenants.create.wizard.summary.yes')
            : t('tenants.create.wizard.summary.no')}
        </Descriptions.Item>
        <Descriptions.Item label={t('tenants.create.wizard.summary.loginUrl')}>
          {portalUrl}
        </Descriptions.Item>
      </Descriptions>

      <Alert
        type="warning"
        showIcon
        style={{ marginTop: 16 }}
        title={t('tenants.create.wizard.summary.irreversibleTitle')}
        description={t('tenants.create.wizard.summary.irreversibleBody')}
      />
    </div>
  );
}
