'use client';

import {
  AppstoreOutlined,
  CheckCircleOutlined,
  GlobalOutlined,
  MobileOutlined,
} from '@ant-design/icons';
import { useMutation } from '@tanstack/react-query';
import { Alert, Button, Card, Col, Row, Space, Tag, Typography } from 'antd';
import { useState } from 'react';
import {
  generateMobileApp,
  generateWebsite,
} from '@/features/website-generator/api/websiteGeneratorApi';
import { DigitalServicePricingCards } from '@/features/website-generator/components/DigitalServicePricingCards';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

const { Paragraph, Text } = Typography;

type DigitalResult = {
  kind: 'website' | 'app';
  url: string;
  label?: string;
};

const TEMPLATES = [
  { id: 'modern', nameKey: 'modern', descKey: 'modernDesc', icon: <GlobalOutlined /> },
  { id: 'classic', nameKey: 'classic', descKey: 'classicDesc', icon: <AppstoreOutlined /> },
  { id: 'minimal', nameKey: 'minimal', descKey: 'minimalDesc', icon: <CheckCircleOutlined /> },
] as const;

type DigitalServicesPanelProps = {
  tenantId: string;
  /** When false, website generate UI is hidden (service inactive or no permission). */
  websiteEnabled?: boolean;
  /** When false, app generate UI is hidden. */
  appEnabled?: boolean;
};

export function DigitalServicesPanel({
  tenantId,
  websiteEnabled = true,
  appEnabled = true,
}: DigitalServicesPanelProps) {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const [selectedTemplateId, setSelectedTemplateId] = useState('modern');
  const [selectedAppType, setSelectedAppType] = useState<'Pwa' | 'Native'>('Pwa');
  const [result, setResult] = useState<DigitalResult | null>(null);

  const websiteMutation = useMutation({
    mutationFn: (templateId: string) => generateWebsite(templateId, tenantId),
    onSuccess: (response, templateId) => {
      if (!response.succeeded || !response.url) {
        message.error(response.error ?? t('common.errors.http500'));
        return;
      }
      setResult({
        kind: 'website',
        url: response.url,
        label: response.templateName ?? templateId,
      });
      message.success(t('tenants.digitalServices.websiteSuccess'));
    },
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'DigitalServicesPanel' }),
  });

  const appMutation = useMutation({
    mutationFn: (appType: 'Pwa' | 'Native') => generateMobileApp(appType, tenantId),
    onSuccess: (response, appType) => {
      if (!response.succeeded || !response.url) {
        message.error(response.error ?? t('common.errors.http500'));
        return;
      }
      setResult({
        kind: 'app',
        url: response.url,
        label: appType,
      });
      message.success(t('tenants.digitalServices.appSuccess'));
    },
    onError: (err) =>
      openApiErrorMessage(message.open, t, err, { logContext: 'DigitalServicesPanel' }),
  });

  const loading = websiteMutation.isPending || appMutation.isPending;
  const showGenerators = websiteEnabled || appEnabled;

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <div>
        <Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tenants.digitalServices.subtitle')}
        </Paragraph>
      </div>

      <DigitalServicePricingCards />

      {showGenerators ? (
        <Row gutter={[16, 16]}>
          {websiteEnabled ? (
            <Col xs={24} lg={appEnabled ? 12 : 24} id="digital-generate-web">
              <Card title={t('tenants.digitalServices.websiteCardTitle')} loading={loading}>
                <Row gutter={[8, 8]}>
                  {TEMPLATES.map((template) => (
                    <Col span={8} key={template.id}>
                      <Card
                        hoverable
                        size="small"
                        style={{
                          textAlign: 'center',
                          borderColor:
                            selectedTemplateId === template.id
                              ? 'var(--ant-color-primary)'
                              : undefined,
                        }}
                        onClick={() => setSelectedTemplateId(template.id)}
                      >
                        <div style={{ fontSize: 28, marginBottom: 8 }}>{template.icon}</div>
                        <Text strong>
                          {t(`tenants.digitalServices.templates.${template.nameKey}`)}
                        </Text>
                        <div>
                          <Text type="secondary" style={{ fontSize: 12 }}>
                            {t(`tenants.digitalServices.templates.${template.descKey}`)}
                          </Text>
                        </div>
                      </Card>
                    </Col>
                  ))}
                </Row>
                <Button
                  type="primary"
                  block
                  style={{ marginTop: 16 }}
                  icon={<GlobalOutlined />}
                  loading={websiteMutation.isPending}
                  onClick={() => websiteMutation.mutate(selectedTemplateId)}
                >
                  {t('tenants.digitalServices.generateWebsite')}
                </Button>
              </Card>
            </Col>
          ) : null}

          {appEnabled ? (
            <Col xs={24} lg={websiteEnabled ? 12 : 24} id="digital-generate-app">
              <Card title={t('tenants.digitalServices.appCardTitle')} loading={loading}>
                <Row gutter={[8, 8]}>
                  <Col span={12}>
                    <Card
                      hoverable
                      size="small"
                      style={{
                        textAlign: 'center',
                        borderColor:
                          selectedAppType === 'Pwa' ? 'var(--ant-color-primary)' : undefined,
                      }}
                      onClick={() => setSelectedAppType('Pwa')}
                    >
                      <div style={{ fontSize: 28, marginBottom: 8 }}>
                        <MobileOutlined />
                      </div>
                      <Text strong>{t('tenants.digitalServices.appTypePwa')}</Text>
                      <div>
                        <Text type="secondary" style={{ fontSize: 12 }}>
                          {t('tenants.digitalServices.appTypePwaDesc')}
                        </Text>
                      </div>
                    </Card>
                  </Col>
                  <Col span={12}>
                    <Card
                      hoverable
                      size="small"
                      style={{
                        textAlign: 'center',
                        borderColor:
                          selectedAppType === 'Native' ? 'var(--ant-color-primary)' : undefined,
                      }}
                      onClick={() => setSelectedAppType('Native')}
                    >
                      <div style={{ fontSize: 28, marginBottom: 8 }}>
                        <AppstoreOutlined />
                      </div>
                      <Text strong>{t('tenants.digitalServices.appTypeNative')}</Text>
                      <div>
                        <Text type="secondary" style={{ fontSize: 12 }}>
                          {t('tenants.digitalServices.appTypeNativeDesc')}
                        </Text>
                      </div>
                    </Card>
                  </Col>
                </Row>
                <Button
                  type="primary"
                  block
                  style={{ marginTop: 16 }}
                  icon={<MobileOutlined />}
                  loading={appMutation.isPending}
                  onClick={() => appMutation.mutate(selectedAppType)}
                >
                  {t('tenants.digitalServices.generateApp')}
                </Button>
              </Card>
            </Col>
          ) : null}
        </Row>
      ) : null}

      {result ? (
        <Card title={t('tenants.digitalServices.resultTitle')}>
          <Space orientation="vertical" size="middle">
            <Tag color="success" icon={<CheckCircleOutlined />}>
              {t('tenants.digitalServices.resultSuccess')}
            </Tag>
            {result.label ? (
              <Text>
                {t('tenants.digitalServices.resultLabel')}: {result.label}
              </Text>
            ) : null}
            <Text>
              {t('tenants.digitalServices.resultUrl')}:{' '}
              <a href={result.url} target="_blank" rel="noreferrer">
                {result.url}
              </a>
            </Text>
            {result.kind === 'app' && result.url.includes('app-source.zip') ? (
              <Alert
                type="info"
                showIcon
                message={t('tenants.digitalServices.nativeDownloadHint')}
              />
            ) : null}
          </Space>
        </Card>
      ) : null}
    </Space>
  );
}
