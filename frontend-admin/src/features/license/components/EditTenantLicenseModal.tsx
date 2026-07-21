'use client';

import { CheckCircleOutlined, ClockCircleOutlined, CloseCircleOutlined } from '@ant-design/icons';
import { DatePicker, Descriptions, Form, Input, Modal, Tag } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { type ReactNode, useEffect, useMemo } from 'react';

import type { TenantLicenseOverviewItem } from '@/features/license/api/tenantLicenseOverview';
import { useEditTenantLicense } from '@/features/license/hooks/useEditTenantLicense';
import {
  type MandantLicenseOverviewKind,
  mandantLicenseOverviewKindLabelKey,
  mandantLicenseOverviewTagColor,
  resolveMandantLicenseOverviewStatus,
} from '@/features/license/utils/mandantLicenseOverviewStatus';
import { useI18n } from '@/i18n';

type EditTenantLicenseFormValues = {
  validUntil: Dayjs;
  licenseKey?: string;
};

export type EditTenantLicenseModalProps = {
  open: boolean;
  tenant: TenantLicenseOverviewItem | null;
  onClose: () => void;
  onSuccess?: () => void;
};

function statusIcon(kind: MandantLicenseOverviewKind): ReactNode {
  switch (kind) {
    case 'active':
      return <CheckCircleOutlined />;
    case 'expiring_soon':
    case 'trial':
      return <ClockCircleOutlined />;
    case 'expired':
    case 'none':
    default:
      return <CloseCircleOutlined />;
  }
}

export function EditTenantLicenseModal(props: EditTenantLicenseModalProps) {
  if (!props.open) {
    return null;
  }
  return <EditTenantLicenseModalContent {...props} />;
}

function EditTenantLicenseModalContent({
  open,
  tenant,
  onClose,
  onSuccess,
}: EditTenantLicenseModalProps) {
  const { t } = useI18n();
  const [form] = Form.useForm<EditTenantLicenseFormValues>();

  const saveMutation = useEditTenantLicense({
    onSuccess: () => {
      onSuccess?.();
      onClose();
    },
  });

  const watchedValidUntil = Form.useWatch('validUntil', form);
  const watchedLicenseKey = Form.useWatch('licenseKey', form);

  const previewStatus = useMemo(() => {
    const validUntilIso = watchedValidUntil?.isValid()
      ? watchedValidUntil.endOf('day').toISOString()
      : (tenant?.validUntilUtc ?? null);

    return resolveMandantLicenseOverviewStatus(
      validUntilIso,
      watchedLicenseKey ?? tenant?.licenseKey ?? null
    );
  }, [tenant?.licenseKey, tenant?.validUntilUtc, watchedLicenseKey, watchedValidUntil]);

  useEffect(() => {
    if (!open || !tenant) {
      return;
    }

    form.setFieldsValue({
      validUntil: tenant.validUntilUtc ? dayjs(tenant.validUntilUtc) : dayjs().add(30, 'day'),
      licenseKey: tenant.licenseKey ?? undefined,
    });
  }, [form, open, tenant]);

  const isAdd = tenant?.status === 'none';

  return (
    <Modal
      title={t('license.superAdmin.editModal.title')}
      open={open}
      onCancel={onClose}
      onOk={() => form.submit()}
      okText={t('license.superAdmin.editModal.saveButton')}
      confirmLoading={saveMutation.isPending}
      width={520}
      destroyOnHidden
    >
      {tenant ? (
        <Descriptions
          bordered
          column={1}
          size="small"
          style={{ marginBottom: 16 }}
          items={[
            {
              key: 'tenant',
              label: t('license.superAdmin.editModal.tenantLabel'),
              children: (
                <>
                  {tenant.tenantName}{' '}
                  <span style={{ color: 'var(--ant-color-text-secondary)' }}>
                    ({tenant.tenantSlug})
                  </span>
                </>
              ),
            },
          ]}
        />
      ) : null}

      <Form
        form={form}
        layout="vertical"
        onFinish={(values) => {
          if (!tenant) {
            return;
          }

          saveMutation.mutate({
            tenantId: tenant.tenantId,
            body: {
              validUntilUtc: values.validUntil.endOf('day').toISOString(),
              licenseKey: values.licenseKey?.trim() || null,
            },
          });
        }}
      >
        <Form.Item
          name="licenseKey"
          label={t('license.superAdmin.editModal.licenseKeyLabel')}
          tooltip={t('license.superAdmin.editModal.licenseKeyTooltip')}
        >
          <Input placeholder="REGK-XXXX-XXXX-XXXX" allowClear />
        </Form.Item>

        <Form.Item
          name="validUntil"
          label={t('license.superAdmin.editModal.validUntilLabel')}
          rules={[
            {
              required: true,
              message: t('license.superAdmin.editModal.dateRequired'),
            },
          ]}
        >
          <DatePicker style={{ width: '100%' }} format="DD.MM.YYYY" />
        </Form.Item>

        <Form.Item label={t('license.superAdmin.editModal.statusLabel')}>
          <Tag
            color={mandantLicenseOverviewTagColor(previewStatus.kind)}
            icon={statusIcon(previewStatus.kind)}
          >
            {t(mandantLicenseOverviewKindLabelKey(previewStatus.kind))}
          </Tag>
        </Form.Item>
      </Form>

      {isAdd ? (
        <p style={{ margin: 0, color: 'var(--ant-color-text-secondary)', fontSize: 12 }}>
          {t('license.superAdmin.editModal.addHint')}
        </p>
      ) : null}
    </Modal>
  );
}
