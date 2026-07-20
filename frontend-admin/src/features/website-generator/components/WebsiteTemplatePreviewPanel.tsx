'use client';

import { useEffect, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Space, Tabs, Tag, Typography } from 'antd';
import { CardSkeleton, PageSkeleton } from '@/components/Skeleton';
import {
  findPendingRequest,
  useRequestDigitalService,
  useTenantDigitalServiceRequests,
} from '@/features/digital-services/hooks/useDigitalServiceRequests';
import { useTenantDigitalService } from '@/features/digital-services/hooks/useTenantDigitalServices';
import {
  canPreviewDigitalWeb,
  canRequestDigitalWeb,
} from '@/features/digital/digitalServicePermissions';
import {
  buildWebsitePreviewBlobUrl,
  fetchWebsiteTemplates,
  previewWebsite,
  type WebsiteTemplate,
} from '@/features/website-generator/api/websiteGeneratorApi';
import { useAntdApp } from '@/hooks/useAntdApp';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { OptimizedImage } from '@/components/OptimizedImage';

const { Paragraph, Text } = Typography;

const FALLBACK_TEMPLATES: WebsiteTemplate[] = [
  {
    id: 'modern',
    name: 'Modern',
    description: 'Clean, modern design',
    previewImage: '/templates/modern-preview.png',
  },
  {
    id: 'classic',
    name: 'Classic',
    description: 'Traditional restaurant style',
    previewImage: '/templates/classic-preview.png',
  },
  {
    id: 'minimal',
    name: 'Minimal',
    description: 'Simple and elegant',
    previewImage: '/templates/minimal-preview.png',
  },
];

export function buildTemplateChangeRequestNote(templateId: string): string {
  return `template-change:${templateId.trim()}`;
}

type WebsiteTemplatePreviewPanelProps = {
  tenantId: string;
};

/**
 * Mandanten-Admin: published site link + read-only template previews + template-change request.
 * Does not generate or publish — Super Admin applies the change after approval.
 */
export function WebsiteTemplatePreviewPanel({ tenantId }: WebsiteTemplatePreviewPanelProps) {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const { user, isSuperAdmin } = usePermissions();
  const userPerms = user ? { permissions: user.permissions } : null;

  const { data: status, isLoading: statusLoading, isError: statusError } =
    useTenantDigitalService(tenantId);
  const { data: requests } = useTenantDigitalServiceRequests(tenantId);
  const requestMutation = useRequestDigitalService();

  const canPreview = canPreviewDigitalWeb(userPerms, isSuperAdmin);
  const canRequest = canRequestDigitalWeb(userPerms, isSuperAdmin);

  const templatesQuery = useQuery({
    queryKey: ['admin', 'website', 'templates'],
    queryFn: fetchWebsiteTemplates,
    enabled: canPreview,
  });

  const website = status?.website;
  const currentTemplateId = website?.templateId?.trim() || 'modern';
  const [selectedTemplate, setSelectedTemplate] = useState(currentTemplateId);
  const [previewUrl, setPreviewUrl] = useState('');
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const previewUrlRef = useRef('');

  useEffect(() => {
    if (website?.templateId) {
      setSelectedTemplate(website.templateId);
    }
  }, [website?.templateId]);

  const templates =
    templatesQuery.data && templatesQuery.data.length > 0
      ? templatesQuery.data
      : FALLBACK_TEMPLATES;

  useEffect(() => {
    if (!canPreview || !selectedTemplate) return;

    let cancelled = false;
    const timer = window.setTimeout(() => {
      void (async () => {
        setPreviewLoading(true);
        setPreviewError(null);
        try {
          const response = await previewWebsite({
            tenantId,
            templateId: selectedTemplate,
          });
          if (!response.succeeded || !response.html || !response.css) {
            throw new Error(response.error || response.code || 'PREVIEW_FAILED');
          }
          const nextUrl = buildWebsitePreviewBlobUrl(
            response.html,
            response.css,
            response.js ?? '',
          );
          if (cancelled) {
            URL.revokeObjectURL(nextUrl);
            return;
          }
          if (previewUrlRef.current) {
            URL.revokeObjectURL(previewUrlRef.current);
          }
          previewUrlRef.current = nextUrl;
          setPreviewUrl(nextUrl);
        } catch (err) {
          if (!cancelled) {
            setPreviewError(err instanceof Error ? err.message : String(err));
          }
        } finally {
          if (!cancelled) setPreviewLoading(false);
        }
      })();
    }, 300);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [canPreview, selectedTemplate, tenantId]);

  useEffect(() => {
    return () => {
      if (previewUrlRef.current) {
        URL.revokeObjectURL(previewUrlRef.current);
      }
    };
  }, []);

  const pendingWeb = findPendingRequest(requests, 'website');
  const isPublished =
    website?.status === 'published' && Boolean(website.url?.trim());
  const templateChanged = selectedTemplate !== currentTemplateId;

  const handleRequestTemplateChange = async () => {
    try {
      await requestMutation.mutateAsync({
        tenantId,
        serviceType: 'website',
        note: buildTemplateChangeRequestNote(selectedTemplate),
      });
      message.success(t('tenants.websitePreview.requestSuccess'));
    } catch (err) {
      message.error(
        err instanceof Error ? err.message : t('tenants.websitePreview.requestFailed'),
      );
    }
  };

  if (statusLoading) {
    return <PageSkeleton widgets={3} />;
  }

  if (statusError || !website) {
    return <Alert type="error" showIcon message={t('tenants.digitalServices.statusLoadFailed')} />;
  }

  if (!canPreview) {
    return (
      <Alert
        type="warning"
        showIcon
        message={t('tenants.digitalServices.webNoPermissionBody')}
      />
    );
  }

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      {isPublished ? (
        <Alert
          type="success"
          showIcon
          message={t('tenants.websitePreview.publishedTitle')}
          description={
            <a href={website.url!} target="_blank" rel="noreferrer">
              {website.url}
            </a>
          }
        />
      ) : (
        <Alert
          type="info"
          showIcon
          message={t('tenants.websitePreview.notPublishedTitle')}
          description={t('tenants.websitePreview.notPublishedBody')}
        />
      )}

      {pendingWeb ? (
        <Alert
          type="info"
          showIcon
          message={t('tenants.websitePreview.pendingTitle')}
          description={
            pendingWeb.note
              ? t('tenants.websitePreview.pendingBodyWithNote', { note: pendingWeb.note })
              : t('tenants.websitePreview.pendingBody')
          }
        />
      ) : null}

      <Card
        title={t('tenants.websitePreview.templatesTitle')}
        extra={
          website.templateId ? (
            <Tag>
              {t('tenants.websitePreview.currentTemplate', {
                name: website.templateId,
              })}
            </Tag>
          ) : null
        }
        loading={templatesQuery.isLoading}
      >
        <Paragraph type="secondary">{t('tenants.websitePreview.templatesIntro')}</Paragraph>
        <Tabs
          activeKey={selectedTemplate}
          onChange={setSelectedTemplate}
          items={templates.map((tpl) => ({
            key: tpl.id,
            label: tpl.name,
            children: (
              <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                <div>
                  <Text strong>{tpl.name}</Text>
                  {tpl.description ? (
                    <Paragraph type="secondary" style={{ marginBottom: 0 }}>
                      {tpl.description}
                    </Paragraph>
                  ) : null}
                </div>

                {tpl.previewImage ? (
                  <OptimizedImage
                    src={tpl.previewImage}
                    alt={tpl.name}
                    width={480}
                    height={270}
                    style={{ width: '100%', height: 'auto', borderRadius: 8 }}
                    sizes="(max-width: 768px) 100vw, 480px"
                  />
                ) : null}

                <div style={{ position: 'relative', minHeight: 320 }}>
                  {previewLoading ? (
                    <div style={{ minHeight: 320 }}>
                      <CardSkeleton count={1} loading />
                    </div>
                  ) : null}
                  {previewError ? (
                    <Alert type="warning" showIcon message={previewError} />
                  ) : null}
                  {!previewLoading && previewUrl && selectedTemplate === tpl.id ? (
                    <iframe
                      title={t('tenants.websitePreview.iframeTitle', { name: tpl.name })}
                      src={previewUrl}
                      style={{
                        width: '100%',
                        height: 420,
                        border: '1px solid var(--ant-color-border)',
                        borderRadius: 8,
                        background: '#fff',
                      }}
                      sandbox="allow-scripts allow-same-origin"
                    />
                  ) : null}
                </div>

                <Space wrap>
                  <Button
                    type={selectedTemplate === tpl.id ? 'primary' : 'default'}
                    onClick={() => setSelectedTemplate(tpl.id)}
                  >
                    {selectedTemplate === tpl.id
                      ? t('tenants.websitePreview.selected')
                      : t('tenants.websitePreview.select')}
                  </Button>
                  {canRequest &&
                  selectedTemplate === tpl.id &&
                  templateChanged &&
                  !pendingWeb ? (
                    <Button
                      type="primary"
                      loading={requestMutation.isPending}
                      onClick={() => void handleRequestTemplateChange()}
                    >
                      {t('tenants.websitePreview.requestChange')}
                    </Button>
                  ) : null}
                </Space>
              </Space>
            ),
          }))}
        />
      </Card>
    </Space>
  );
}
