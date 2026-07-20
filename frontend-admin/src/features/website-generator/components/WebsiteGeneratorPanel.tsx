'use client';

import { useMutation, useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Radio, Space, Typography } from 'antd';
import Link from 'next/link';
import { useState } from 'react';
import {
  fetchWebsiteTemplates,
  generateMobileApp,
  generateWebsite,
} from '@/features/website-generator/api/websiteGeneratorApi';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

const { Paragraph, Text } = Typography;

export function WebsiteGeneratorPanel({
  websiteEnabled = true,
  appEnabled = true,
}: {
  websiteEnabled?: boolean;
  appEnabled?: boolean;
} = {}) {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const [templateId, setTemplateId] = useState<string>('modern');
  const [appType, setAppType] = useState<'Pwa' | 'Native'>('Pwa');
  const [websiteUrl, setWebsiteUrl] = useState<string | null>(null);
  const [mobileUrl, setMobileUrl] = useState<string | null>(null);

  const templatesQuery = useQuery({
    queryKey: ['admin', 'website', 'templates'],
    queryFn: fetchWebsiteTemplates,
    enabled: websiteEnabled,
  });

  const websiteMutation = useMutation({
    mutationFn: () => generateWebsite(templateId),
    onSuccess: (result) => {
      if (!result.succeeded || !result.url) {
        message.error(result.error ?? t('common.errors.http500'));
        return;
      }
      setWebsiteUrl(result.url);
      message.success(t('settings.websiteGenerator.websiteSuccess'));
    },
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'WebsiteGeneratorPanel' }),
  });

  const mobileMutation = useMutation({
    mutationFn: () => generateMobileApp(appType),
    onSuccess: (result) => {
      if (!result.succeeded || !result.url) {
        message.error(result.error ?? t('common.errors.http500'));
        return;
      }
      setMobileUrl(result.url);
      message.success(t('settings.websiteGenerator.mobileSuccess'));
    },
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'WebsiteGeneratorPanel' }),
  });

  const templates = templatesQuery.data ?? [];

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Paragraph>{t('settings.websiteGenerator.intro')}</Paragraph>
      <Alert
        type="info"
        showIcon
        message={
          <span>
            {t('settings.websiteGenerator.companyHint')}{' '}
            <Link href="/settings/company">{t('settings.websiteGenerator.companyLink')}</Link>
          </span>
        }
      />

      {websiteEnabled ? (
        <Card loading={templatesQuery.isLoading}>
          <Text strong>{t('settings.websiteGenerator.templateLabel')}</Text>
          <div style={{ marginTop: 12 }}>
            <Radio.Group
              value={templateId}
              onChange={(e) => setTemplateId(e.target.value)}
              optionType="button"
              buttonStyle="solid"
              options={templates.map((tpl) => ({
                label: `${tpl.name} — ${tpl.description}`,
                value: tpl.id,
              }))}
            />
          </div>
        </Card>
      ) : null}

      {appEnabled ? (
        <Card>
          <Text strong>{t('settings.websiteGenerator.appTypeLabel')}</Text>
          <div style={{ marginTop: 12 }}>
            <Radio.Group
              value={appType}
              onChange={(e) => setAppType(e.target.value)}
              optionType="button"
              buttonStyle="solid"
              options={[
                { label: t('settings.websiteGenerator.appTypePwa'), value: 'Pwa' },
                { label: t('settings.websiteGenerator.appTypeNative'), value: 'Native' },
              ]}
            />
          </div>
        </Card>
      ) : null}

      <Space wrap>
        {websiteEnabled ? (
          <Button
            type="primary"
            loading={websiteMutation.isPending}
            onClick={() => websiteMutation.mutate()}
            disabled={!templateId}
          >
            {t('settings.websiteGenerator.generateWebsite')}
          </Button>
        ) : null}
        {appEnabled ? (
          <Button
            loading={mobileMutation.isPending}
            onClick={() => mobileMutation.mutate()}
          >
            {t('settings.websiteGenerator.generateMobile')}
          </Button>
        ) : null}
      </Space>

      {websiteUrl ? (
        <Alert
          type="success"
          showIcon
          message={t('settings.websiteGenerator.websiteSuccess')}
          description={
            <a href={websiteUrl} target="_blank" rel="noreferrer">
              {t('settings.websiteGenerator.openUrl')}: {websiteUrl}
            </a>
          }
        />
      ) : null}

      {mobileUrl ? (
        <Alert
          type="success"
          showIcon
          message={t('settings.websiteGenerator.mobileSuccess')}
          description={
            <a href={mobileUrl} target="_blank" rel="noreferrer">
              {t('settings.websiteGenerator.openUrl')}: {mobileUrl}
            </a>
          }
        />
      ) : null}
    </Space>
  );
}
