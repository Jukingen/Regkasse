'use client';

import { Form, Input, InputNumber, Modal, Select, Switch } from 'antd';
import { useEffect } from 'react';

import {
  createExportTemplateId,
} from '@/features/exports/exportTemplatesStorage';
import type {
  ExportTemplate,
  ExportTemplateConfig,
  ExportTemplatePeriod,
} from '@/features/exports/exportTemplateTypes';
import { useI18n } from '@/i18n/I18nProvider';

type FormValues = {
  name: string;
  shared: boolean;
  kind: ExportTemplateConfig['kind'];
  period?: ExportTemplatePeriod;
  registerNumberHint?: string;
  includeSpecialReceipts?: boolean;
  includeDailyClosings?: boolean;
  formats?: Array<'pdf' | 'csv'>;
  strategy?: 'tenant' | 'system';
  retentionDays?: number;
};

export type ExportTemplateEditorModalProps = {
  open: boolean;
  initial?: ExportTemplate | null;
  onCancel: () => void;
  onSave: (template: ExportTemplate) => void;
  currentUserId?: string | null;
  currentUserName?: string | null;
};

/**
 * Create / edit custom export templates (presets are read-only clones → new custom).
 */
export function ExportTemplateEditorModal({
  open,
  initial,
  onCancel,
  onSave,
  currentUserId,
  currentUserName,
}: ExportTemplateEditorModalProps) {
  const { t } = useI18n();
  const [form] = Form.useForm<FormValues>();
  const kind = Form.useWatch('kind', form) ?? 'dep-export';
  const editingPreset = Boolean(initial?.isPreset);

  useEffect(() => {
    if (!open) return;
    if (!initial) {
      form.setFieldsValue({
        name: '',
        shared: false,
        kind: 'dep-export',
        period: 'last24h',
        registerNumberHint: '',
        includeSpecialReceipts: true,
        includeDailyClosings: true,
        formats: ['pdf', 'csv'],
        strategy: 'tenant',
        retentionDays: 30,
      });
      return;
    }

    const c = initial.config;
    form.setFieldsValue({
      name: editingPreset
        ? `${initial.name} (${t('common.exportTemplates.copySuffix')})`
        : initial.name,
      shared: editingPreset ? false : initial.shared,
      kind: c.kind,
      period: c.kind === 'backup' ? undefined : c.period,
      registerNumberHint: c.kind === 'dep-export' ? c.registerNumberHint : undefined,
      includeSpecialReceipts: c.kind === 'dep-export' ? c.includeSpecialReceipts : true,
      includeDailyClosings: c.kind === 'dep-export' ? c.includeDailyClosings : true,
      formats: c.kind === 'tagesbericht' ? c.formats : ['pdf', 'csv'],
      strategy: c.kind === 'backup' ? c.strategy : 'tenant',
      retentionDays: c.kind === 'backup' ? c.retentionDays : 30,
    });
  }, [open, initial, form, editingPreset, t]);

  const handleOk = async () => {
    const values = await form.validateFields();
    let config: ExportTemplateConfig;
    if (values.kind === 'dep-export') {
      config = {
        kind: 'dep-export',
        registerNumberHint: values.registerNumberHint?.trim() || undefined,
        period: values.period ?? 'last24h',
        includeSpecialReceipts: values.includeSpecialReceipts !== false,
        includeDailyClosings: values.includeDailyClosings !== false,
      };
    } else if (values.kind === 'tagesbericht') {
      config = {
        kind: 'tagesbericht',
        period: values.period ?? 'currentMonth',
        formats: values.formats?.length ? values.formats : ['pdf'],
      };
    } else {
      config = {
        kind: 'backup',
        strategy: values.strategy ?? 'tenant',
        retentionDays: Math.min(365, Math.max(7, values.retentionDays ?? 30)),
      };
    }

    const now = new Date().toISOString();
    const template: ExportTemplate = {
      id: editingPreset || !initial ? createExportTemplateId() : initial.id,
      name: values.name.trim(),
      shared: values.shared === true,
      isPreset: false,
      config,
      createdAt: editingPreset || !initial ? now : initial.createdAt,
      updatedAt: now,
      createdByUserId: currentUserId ?? null,
      createdByName: currentUserName ?? null,
    };
    onSave(template);
  };

  return (
    <Modal
      open={open}
      title={
        initial && !editingPreset
          ? t('common.exportTemplates.editTitle')
          : t('common.exportTemplates.createTitle')
      }
      onCancel={onCancel}
      onOk={() => void handleOk()}
      destroyOnHidden
      okText={t('common.exportTemplates.save')}
      cancelText={t('common.exportTemplates.cancel')}
    >
      <Form form={form} layout="vertical">
        <Form.Item
          name="name"
          label={t('common.exportTemplates.fieldName')}
          rules={[{ required: true, message: t('common.exportTemplates.nameRequired') }]}
        >
          <Input maxLength={120} />
        </Form.Item>
        <Form.Item name="kind" label={t('common.exportTemplates.fieldKind')} rules={[{ required: true }]}>
          <Select
            disabled={Boolean(initial) && !editingPreset}
            options={[
              { value: 'dep-export', label: t('common.exportTemplates.kinds.dep') },
              { value: 'tagesbericht', label: t('common.exportTemplates.kinds.tagesbericht') },
              { value: 'backup', label: t('common.exportTemplates.kinds.backup') },
            ]}
          />
        </Form.Item>

        {kind !== 'backup' ? (
          <Form.Item name="period" label={t('common.exportTemplates.fieldPeriod')} rules={[{ required: true }]}>
            <Select
              options={[
                { value: 'last24h', label: t('common.exportTemplates.periods.last24h') },
                { value: 'last7d', label: t('common.exportTemplates.periods.last7d') },
                { value: 'currentMonth', label: t('common.exportTemplates.periods.currentMonth') },
                { value: 'lastMonth', label: t('common.exportTemplates.periods.lastMonth') },
              ]}
            />
          </Form.Item>
        ) : null}

        {kind === 'dep-export' ? (
          <>
            <Form.Item name="registerNumberHint" label={t('common.exportTemplates.fieldRegisterHint')}>
              <Input placeholder="KASSE-001" maxLength={64} />
            </Form.Item>
            <Form.Item
              name="includeSpecialReceipts"
              label={t('common.exportTemplates.fieldSpecialReceipts')}
              valuePropName="checked"
            >
              <Switch />
            </Form.Item>
            <Form.Item
              name="includeDailyClosings"
              label={t('common.exportTemplates.fieldDailyClosings')}
              valuePropName="checked"
            >
              <Switch />
            </Form.Item>
          </>
        ) : null}

        {kind === 'tagesbericht' ? (
          <Form.Item name="formats" label={t('common.exportTemplates.fieldFormats')} rules={[{ required: true }]}>
            <Select
              mode="multiple"
              options={[
                { value: 'pdf', label: 'PDF' },
                { value: 'csv', label: 'CSV' },
              ]}
            />
          </Form.Item>
        ) : null}

        {kind === 'backup' ? (
          <>
            <Form.Item name="strategy" label={t('common.exportTemplates.fieldStrategy')} rules={[{ required: true }]}>
              <Select
                options={[
                  { value: 'tenant', label: t('common.exportTemplates.strategies.tenant') },
                  { value: 'system', label: t('common.exportTemplates.strategies.system') },
                ]}
              />
            </Form.Item>
            <Form.Item
              name="retentionDays"
              label={t('common.exportTemplates.fieldRetention')}
              rules={[{ required: true }]}
            >
              <InputNumber min={7} max={365} style={{ width: '100%' }} />
            </Form.Item>
          </>
        ) : null}

        <Form.Item
          name="shared"
          label={t('common.exportTemplates.fieldShared')}
          valuePropName="checked"
          extra={t('common.exportTemplates.sharedHint')}
        >
          <Switch />
        </Form.Item>
      </Form>
    </Modal>
  );
}
