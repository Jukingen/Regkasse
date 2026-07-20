'use client';

import {
  Alert,
  Button,
  Card,
  Checkbox,
  Col,
  ColorPicker,
  Form,
  Input,
  Row,
  Select,
  Space,
  Typography,
} from 'antd';
import { useEffect, useRef, useState } from 'react';
import { CardSkeleton } from '@/components/Skeleton';
import { useTenantCustomization } from '@/features/tenant/hooks/useTenantCustomization';
import {
  buildWebsitePreviewBlobUrl,
  previewWebsite,
} from '@/features/website-generator/api/websiteGeneratorApi';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

const { Paragraph, Text } = Typography;

const PAGE_OPTIONS = [
  { value: 'home', labelKey: 'pageHome' },
  { value: 'menu', labelKey: 'pageMenu' },
  { value: 'about', labelKey: 'pageAbout' },
  { value: 'contact', labelKey: 'pageContact' },
  { value: 'gallery', labelKey: 'pageGallery' },
  { value: 'reservation', labelKey: 'pageReservation' },
] as const;

const FEATURE_OPTIONS = [
  { value: 'live-menu', labelKey: 'featureLiveMenu' },
  { value: 'online-order', labelKey: 'featureOnlineOrder' },
  { value: 'reservation', labelKey: 'featureReservation' },
  { value: 'loyalty', labelKey: 'featureLoyalty' },
] as const;

const TEMPLATE_OPTIONS = [
  { value: 'modern', label: 'Modern' },
  { value: 'classic', label: 'Classic' },
  { value: 'minimal', label: 'Minimal' },
] as const;

type FormValues = {
  primaryColor?: string;
  secondaryColor?: string;
  backgroundColor?: string;
  textColor?: string;
  fontFamily?: string;
  logoUrl?: string;
  faviconUrl?: string;
  pages?: string[];
  features?: string[];
  customCss?: string;
  customJs?: string;
};

type TenantCustomizationPanelProps = {
  surface: 'website' | 'app';
  tenantId?: string;
  /** When false, omit outer page intro (parent page already has one). */
  showIntro?: boolean;
};

function colorToHex(value: unknown, fallback: string): string {
  if (typeof value === 'string' && value.length > 0) return value;
  if (value && typeof value === 'object' && 'toHexString' in value) {
    const fn = (value as { toHexString?: () => string }).toHexString;
    if (typeof fn === 'function') return fn.call(value);
  }
  return fallback;
}

function buildAppPreviewHtml(values: FormValues, title: string): string {
  const primary = colorToHex(values.primaryColor, '#0f172a');
  const accent = colorToHex(values.secondaryColor, '#38bdf8');
  const bg = colorToHex(values.backgroundColor, '#ffffff');
  const text = colorToHex(values.textColor, '#0f172a');
  const font = values.fontFamily?.trim() || 'system-ui, sans-serif';
  const logo = values.logoUrl?.trim();
  const logoHtml = logo
    ? `<img src="${logo.replace(/"/g, '&quot;')}" alt="" style="max-height:64px;border-radius:12px;margin-bottom:1rem" />`
    : '';
  return `<!DOCTYPE html><html lang="de"><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/>
<style>
body{margin:0;font-family:${font};background:${bg};color:${text};}
.shell{max-width:22rem;margin:0 auto;padding:1.5rem 1.25rem;}
h1{font-size:1.5rem;margin:0 0 .5rem;color:${primary};}
.accent{color:${accent};font-size:.9rem;}
${values.customCss ?? ''}
</style></head><body><main class="shell">${logoHtml}<h1>${title}</h1>
<p class="accent">App-Vorschau</p>
<ul style="list-style:none;padding:0;margin:1.5rem 0 0">
<li style="padding:.4rem 0;border-bottom:1px solid #e2e8f0">Menüpunkt A</li>
<li style="padding:.4rem 0;border-bottom:1px solid #e2e8f0">Menüpunkt B</li>
</ul></main></body></html>`;
}

export function TenantCustomizationPanel({
  surface,
  tenantId,
  showIntro = true,
}: TenantCustomizationPanelProps) {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const [form] = Form.useForm<FormValues>();
  const formValues = Form.useWatch([], form) as FormValues | undefined;
  const [previewTemplateId, setPreviewTemplateId] = useState('modern');
  const [previewUrl, setPreviewUrl] = useState('');
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const previewUrlRef = useRef('');
  const { data, isLoading, update, isUpdating } = useTenantCustomization({
    tenantId,
    surface,
  });

  useEffect(() => {
    if (!data) return;
    form.setFieldsValue({
      primaryColor: data.primaryColor ?? '#0f172a',
      secondaryColor: data.secondaryColor ?? '#38bdf8',
      backgroundColor: data.backgroundColor ?? '#ffffff',
      textColor: data.textColor ?? '#0f172a',
      fontFamily: data.fontFamily ?? undefined,
      logoUrl: data.logoUrl ?? undefined,
      faviconUrl: data.faviconUrl ?? undefined,
      pages: data.pages,
      features: data.features,
      customCss: data.customCss ?? undefined,
      customJs: data.customJs ?? undefined,
    });
  }, [data, form]);

  useEffect(() => {
    if (!formValues || isLoading) return;

    let cancelled = false;
    const timer = window.setTimeout(() => {
      void (async () => {
        setPreviewLoading(true);
        setPreviewError(null);
        try {
          let nextUrl: string;
          if (surface === 'app') {
            const html = buildAppPreviewHtml(
              formValues,
              t('tenants.customization.appTitle'),
            );
            nextUrl = URL.createObjectURL(
              new Blob([html], { type: 'text/html;charset=utf-8' }),
            );
          } else {
            const response = await previewWebsite({
              tenantId,
              templateId: previewTemplateId,
              primaryColor: colorToHex(formValues.primaryColor, '#0f172a'),
              secondaryColor: colorToHex(formValues.secondaryColor, '#38bdf8'),
              backgroundColor: colorToHex(formValues.backgroundColor, '#ffffff'),
              textColor: colorToHex(formValues.textColor, '#0f172a'),
              fontFamily: formValues.fontFamily,
              logoUrl: formValues.logoUrl,
              faviconUrl: formValues.faviconUrl,
              pages: formValues.pages,
              features: formValues.features,
              customCss: formValues.customCss,
              customJs: formValues.customJs,
            });
            if (!response.succeeded || !response.html || !response.css) {
              throw new Error(response.error || response.code || 'PREVIEW_FAILED');
            }
            nextUrl = buildWebsitePreviewBlobUrl(
              response.html,
              response.css,
              response.js ?? '',
            );
          }

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
    }, 500);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [formValues, isLoading, previewTemplateId, surface, t, tenantId]);

  useEffect(() => {
    return () => {
      if (previewUrlRef.current) {
        URL.revokeObjectURL(previewUrlRef.current);
      }
    };
  }, []);

  const handleSave = async (values: FormValues) => {
    try {
      await update({
        primaryColor: colorToHex(values.primaryColor, '#0f172a'),
        secondaryColor: colorToHex(values.secondaryColor, '#38bdf8'),
        backgroundColor: colorToHex(values.backgroundColor, '#ffffff'),
        textColor: colorToHex(values.textColor, '#0f172a'),
        fontFamily: values.fontFamily,
        logoUrl: values.logoUrl,
        faviconUrl: values.faviconUrl,
        pages: values.pages,
        features: values.features,
        customCss: values.customCss,
        customJs: values.customJs,
      });
      message.success(t('tenants.customization.saved'));
    } catch (err) {
      openApiErrorMessage(message.open, t, err, { logContext: 'TenantCustomizationPanel.save' });
    }
  };

  return (
    <Row gutter={[24, 24]}>
      <Col xs={24} xl={14}>
        <Form
          form={form}
          layout="vertical"
          onFinish={(values) => void handleSave(values)}
          disabled={isUpdating || isLoading}
        >
          <Space orientation="vertical" size="large" style={{ width: '100%' }}>
            {showIntro ? (
              <Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t('tenants.customization.intro')}
              </Paragraph>
            ) : null}

            <Card title={t('tenants.customization.sectionGeneral')} loading={isLoading}>
              <Form.Item name="logoUrl" label={t('tenants.customization.logoUrl')}>
                <Input placeholder="/media/..." />
              </Form.Item>
              <Form.Item name="faviconUrl" label={t('tenants.customization.faviconUrl')}>
                <Input placeholder="/media/..." />
              </Form.Item>
              <Form.Item name="fontFamily" label={t('tenants.customization.fontFamily')}>
                <Input placeholder="system-ui, sans-serif" />
              </Form.Item>
            </Card>

            <Card title={t('tenants.customization.sectionColors')} loading={isLoading}>
              <Row gutter={16}>
                <Col xs={24} sm={12} md={6}>
                  <Form.Item name="primaryColor" label={t('tenants.customization.primaryColor')}>
                    <ColorPicker showText format="hex" />
                  </Form.Item>
                </Col>
                <Col xs={24} sm={12} md={6}>
                  <Form.Item
                    name="secondaryColor"
                    label={t('tenants.customization.secondaryColor')}
                  >
                    <ColorPicker showText format="hex" />
                  </Form.Item>
                </Col>
                <Col xs={24} sm={12} md={6}>
                  <Form.Item
                    name="backgroundColor"
                    label={t('tenants.customization.backgroundColor')}
                  >
                    <ColorPicker showText format="hex" />
                  </Form.Item>
                </Col>
                <Col xs={24} sm={12} md={6}>
                  <Form.Item name="textColor" label={t('tenants.customization.textColor')}>
                    <ColorPicker showText format="hex" />
                  </Form.Item>
                </Col>
              </Row>
            </Card>

            <Card title={t('tenants.customization.sectionPages')} loading={isLoading}>
              <Form.Item name="pages" label={t('tenants.customization.pages')}>
                <Checkbox.Group
                  options={PAGE_OPTIONS.map((o) => ({
                    value: o.value,
                    label: t(`tenants.customization.${o.labelKey}`),
                  }))}
                />
              </Form.Item>
            </Card>

            <Card title={t('tenants.customization.sectionFeatures')} loading={isLoading}>
              <Form.Item name="features" label={t('tenants.customization.features')}>
                <Checkbox.Group
                  options={FEATURE_OPTIONS.map((o) => ({
                    value: o.value,
                    label: t(`tenants.customization.${o.labelKey}`),
                  }))}
                />
              </Form.Item>
            </Card>

            <Card title={t('tenants.customization.sectionAdvanced')} loading={isLoading}>
              <Form.Item name="customCss" label={t('tenants.customization.customCss')}>
                <Input.TextArea rows={5} placeholder="/* optional */" />
              </Form.Item>
              <Form.Item name="customJs" label={t('tenants.customization.customJs')}>
                <Input.TextArea rows={5} placeholder="// optional" />
              </Form.Item>
            </Card>

            <Button type="primary" htmlType="submit" loading={isUpdating}>
              {t('tenants.customization.save')}
            </Button>
          </Space>
        </Form>
      </Col>

      <Col xs={24} xl={10}>
        <Card
          title={t('tenants.customization.previewTitle')}
          extra={
            surface === 'website' ? (
              <Select
                size="small"
                value={previewTemplateId}
                onChange={setPreviewTemplateId}
                options={TEMPLATE_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
                style={{ width: 120 }}
                aria-label={t('tenants.customization.previewTemplate')}
              />
            ) : null
          }
          styles={{ body: { padding: 12 } }}
        >
          <Paragraph type="secondary" style={{ marginTop: 0 }}>
            {t('tenants.customization.previewHint')}
          </Paragraph>
          {previewError ? (
            <Alert
              type="warning"
              showIcon
              title={t('tenants.customization.previewError')}
              description={previewError}
              style={{ marginBottom: 12 }}
            />
          ) : null}
          <div style={{ position: 'relative', minHeight: 420 }}>
            {previewLoading ? (
              <CardSkeleton count={1} />
            ) : previewUrl ? (
              <iframe
                title={t('tenants.customization.previewTitle')}
                src={previewUrl}
                sandbox="allow-scripts"
                style={{
                  width: '100%',
                  height: 520,
                  border: '1px solid #e2e8f0',
                  borderRadius: 8,
                  background: '#fff',
                }}
              />
            ) : (
              <Text type="secondary">{t('tenants.customization.previewEmpty')}</Text>
            )}
          </div>
        </Card>
      </Col>
    </Row>
  );
}
