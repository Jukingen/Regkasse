'use client';

import { useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Form, Input, Space, Typography } from 'antd';
import Link from 'next/link';
import { useEffect, useMemo, useRef, useState } from 'react';

import { getGetApiCompanySettingsQueryKey } from '@/api/generated/company-settings/company-settings';
import { FieldTooltip } from '@/components/FieldTooltip';
import { FormSkeleton } from '@/components/Skeleton';
import { AutoSaveStatusIndicator, FormFieldWithTooltip } from '@/components/form';
import {
  useCompanySettings,
  useUpdateCompanySettings,
} from '@/features/settings/hooks/useCompanySettings';
import {
  type CompanySettingsFormValues,
  mapCompanyFormToUpdateRequest,
  mapCompanySettingsToFormValues,
} from '@/features/settings/types/companySettingsForm';
import { localizeSettingsFieldValidationMessages } from '@/features/settings/utils/localizeSettingsFieldValidationMessages';
import { useAntdApp } from '@/hooks/useAntdApp';
import {
  clearAutoSaveDraft,
  readAutoSaveDraft,
  useAutoSave,
  writeAutoSaveDraft,
} from '@/hooks/useAutoSave';
import { useI18n } from '@/i18n';
import { applyAspNetFieldErrorsToForm } from '@/lib/form/applyAspNetFieldErrorsToForm';
import { createValidationRules } from '@/lib/validation';

const COMPANY_DRAFT_KEY = 'fa:draft:company-settings';

function getLoadErrorDescription(err: unknown, translate: (key: string) => string): string {
  if (err instanceof Error && err.message.trim()) return err.message.trim();
  const normalized = (err as { normalized?: { message?: string } })?.normalized;
  if (normalized?.message?.trim()) return normalized.message.trim();
  const msg = (err as { message?: string })?.message;
  if (typeof msg === 'string' && msg.trim()) return msg.trim();
  return translate('settings.page.loadErrorFallback');
}

export function CompanySettingsForm() {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const [form] = Form.useForm<CompanySettingsFormValues>();
  const queryClient = useQueryClient();
  const settingsQuery = useCompanySettings();
  const { data: settings, isLoading, isError, error, isFetching, isSuccess } = settingsQuery;
  const reloadSettings = () =>
    queryClient.invalidateQueries({ queryKey: getGetApiCompanySettingsQueryKey() });
  const { updateSettings, isLoading: isUpdating } = useUpdateCompanySettings();
  const rules = useMemo(() => createValidationRules(t), [t]);
  const [watchedValues, setWatchedValues] = useState<Partial<CompanySettingsFormValues>>({});

  const {
    saving,
    saved,
    error: autoSaveError,
  } = useAutoSave(
    watchedValues,
    async (data) => {
      writeAutoSaveDraft(COMPANY_DRAFT_KEY, data);
    },
    900,
    { enabled: Boolean(settings) && !isLoading, skipInitial: true }
  );

  const hydratedRef = useRef(false);

  useEffect(() => {
    if (!settings || hydratedRef.current) return;
    hydratedRef.current = true;
    const mapped = mapCompanySettingsToFormValues(settings);
    const draft = readAutoSaveDraft<Partial<CompanySettingsFormValues>>(COMPANY_DRAFT_KEY);
    const hasDraft = draft && JSON.stringify(draft) !== JSON.stringify(mapped);
    if (hasDraft) {
      form.setFieldsValue(draft);
      setWatchedValues(draft);
      message.info(t('common.autoSave.draftRestored'));
    } else {
      form.setFieldsValue(mapped);
      setWatchedValues(mapped);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- hydrate once per mount when settings arrive
  }, [settings, form]);

  const onFinish = async (values: CompanySettingsFormValues) => {
    try {
      const payload = mapCompanyFormToUpdateRequest(values, settings);
      await updateSettings(payload);
      clearAutoSaveDraft(COMPANY_DRAFT_KEY);
      message.success(t('settings.companyPage.saveSuccess'));
      await reloadSettings();
    } catch (err) {
      const fieldNames = applyAspNetFieldErrorsToForm(form, err, {
        localizeMessage: (formField, messages) =>
          localizeSettingsFieldValidationMessages(t, formField, messages),
      });
      if (fieldNames[0]) {
        form.scrollToField(fieldNames[0], { block: 'center' });
      }
      message.error(t('settings.page.saveFailed'));
    }
  };

  if (isLoading) {
    return (
      <>
        <Form form={form} style={{ display: 'none' }} preserve />
        <FormSkeleton fields={6} loading />
      </>
    );
  }

  if (isError) {
    return (
      <>
        <Form form={form} style={{ display: 'none' }} preserve />
        <Alert
          type="error"
          title={t('settings.page.loadErrorTitle')}
          description={getLoadErrorDescription(error, t)}
          showIcon
          action={
            <Button
              size="small"
              type="primary"
              onClick={() => void reloadSettings()}
              loading={isFetching}
            >
              {t('common.buttons.retry')}
            </Button>
          }
        />
      </>
    );
  }

  if (isSuccess && settings == null) {
    return (
      <>
        <Form form={form} style={{ display: 'none' }} preserve />
        <Card>
          <Typography.Paragraph type="secondary">{t('settings.page.empty')}</Typography.Paragraph>
          <Button type="primary" onClick={() => void reloadSettings()} loading={isFetching}>
            {t('common.buttons.retry')}
          </Button>
        </Card>
      </>
    );
  }

  const g = (key: string) => t(`settings.form.general.${key}`);
  const c = (key: string) => t(`settings.companyPage.${key}`);

  return (
    <Card
      title={
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 12 }}>
          {c('cardTitle')}
          <AutoSaveStatusIndicator saving={saving} saved={saved} error={autoSaveError} />
        </span>
      }
    >
      <Alert
        type="info"
        showIcon
        title={c('rksvInfoTitle')}
        description={c('rksvInfoDescription')}
        style={{ marginBottom: 24 }}
      />

      <Form
        form={form}
        layout="vertical"
        onFinish={onFinish}
        onValuesChange={(_, all) => setWatchedValues(all)}
        scrollToFirstError
      >
        <Alert
          type="warning"
          showIcon
          title={c('requiredFieldsTitle')}
          description={c('requiredFieldsDescription')}
          style={{ marginBottom: 16 }}
        />

        <Form.Item
          name="companyName"
          label={g('companyName')}
          rules={[rules.required(g('companyName'))]}
        >
          <Input placeholder={c('placeholderCompanyName')} />
        </Form.Item>

        <Form.Item
          name="companyAddress"
          label={g('companyAddress')}
          rules={[rules.required(g('companyAddress'))]}
        >
          <Input.TextArea rows={3} placeholder={c('placeholderAddress')} />
        </Form.Item>

        <FormFieldWithTooltip
          name="companyTaxNumber"
          label={
            <FieldTooltip title={g('companyTaxNumberTooltip')}>
              {g('companyTaxNumber')}
            </FieldTooltip>
          }
          rules={rules.atuTaxNumber(true)}
        >
          <Input placeholder={g('placeholderAtu')} />
        </FormFieldWithTooltip>

        <Typography.Title level={5} style={{ marginTop: 8, marginBottom: 16 }}>
          {c('optionalSectionTitle')}
        </Typography.Title>

        <Form.Item name="companyPhone" label={c('phone')}>
          <Input placeholder={c('placeholderPhone')} />
        </Form.Item>

        <Form.Item name="companyEmail" label={c('email')} rules={[rules.email]}>
          <Input type="email" placeholder={c('placeholderEmail')} />
        </Form.Item>

        <Form.Item name="companyWebsite" label={g('companyWebsite')}>
          <Input placeholder={c('placeholderWebsite')} />
        </Form.Item>

        <Form.Item name="companyDescription" label={c('receiptFooter')}>
          <Input.TextArea rows={2} placeholder={c('placeholderReceiptFooter')} />
        </Form.Item>

        <Form.Item>
          <Space wrap>
            <Button type="primary" htmlType="submit" loading={isUpdating}>
              {t('settings.page.saveChanges')}
            </Button>
            <Button
              onClick={() => {
                const mapped = mapCompanySettingsToFormValues(settings);
                form.setFieldsValue(mapped);
                setWatchedValues(mapped);
                clearAutoSaveDraft(COMPANY_DRAFT_KEY);
              }}
            >
              {c('reset')}
            </Button>
          </Space>
        </Form.Item>
      </Form>

      <Typography.Paragraph type="secondary" style={{ marginBottom: 0, marginTop: 8 }}>
        {c('advancedHint')} <Link href="/settings">{c('advancedLink')}</Link>
      </Typography.Paragraph>
    </Card>
  );
}
