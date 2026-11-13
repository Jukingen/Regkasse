'use client';

import { Alert, Button, Card, Col, Form, InputNumber, Row, Space, Statistic, Switch, Tag, Typography } from 'antd';
import React, { useEffect } from 'react';

import { BackupStorageCostsDashboard } from '@/features/backup/components/BackupStorageCostsDashboard';
import { useBackupPermissions } from '@/features/backup/hooks/useBackupPermissions';
import {
  useBackupRetentionPolicy,
  useSaveBackupRetentionPolicy,
} from '@/features/backup/hooks/useBackupRetentionPolicy';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type FormValues = {
  hotRetentionDays: number;
  warmRetentionDays: number;
  coldRetentionYears: number;
  coldStorageEnabled: boolean;
};

export function BackupRetentionSettings() {
  const { t } = useI18n();
  const notify = useNotify();
  const { isSuperAdmin, canConfigure } = useBackupPermissions();
  const canEdit = isSuperAdmin && canConfigure;
  const { data, isLoading, isError } = useBackupRetentionPolicy();
  const save = useSaveBackupRetentionPolicy();
  const [form] = Form.useForm<FormValues>();

  useEffect(() => {
    if (!data) return;
    form.setFieldsValue({
      hotRetentionDays: data.hotRetentionDays,
      warmRetentionDays: data.warmRetentionDays,
      coldRetentionYears: data.coldRetentionYears,
      coldStorageEnabled: data.coldStorageEnabled,
    });
  }, [data, form]);

  const onSave = async (values: FormValues) => {
    try {
      await save.mutateAsync({
        hotRetentionDays: values.hotRetentionDays,
        warmRetentionDays: values.warmRetentionDays,
        coldRetentionYears: values.coldRetentionYears,
        coldStorageEnabled: values.coldStorageEnabled,
        legalRetentionEnforced: true,
      });
      notify.successKey('backupDr.retention.saveSuccess');
    } catch (err) {
      notify.apiError(err, {
        logContext: 'BackupRetentionSettings.save',
        fallbackKey: 'backupDr.retention.saveFailed',
      });
    }
  };

  if (isError) {
    return <Alert type="error" showIcon title={t('backupDr.retention.loadFailed')} />;
  }

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      <Alert
        type="info"
        showIcon
        title={t('backupDr.retention.legalTitle')}
        description={t('backupDr.retention.legalDescription')}
      />

      <Row gutter={[16, 16]}>
        <Col xs={24} md={8}>
          <Card>
            <Statistic
              title={t('backupDr.retention.hotLabel')}
              value={data?.hotRetentionDays ?? 30}
              suffix={t('backupDr.retention.daysSuffix')}
            />
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card>
            <Statistic
              title={t('backupDr.retention.warmLabel')}
              value={data?.warmRetentionDays ?? 90}
              suffix={t('backupDr.retention.daysSuffix')}
            />
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card>
            <Statistic
              title={t('backupDr.retention.coldLabel')}
              value={data?.coldRetentionYears ?? 7}
              suffix={t('backupDr.retention.yearsSuffix')}
            />
          </Card>
        </Col>
      </Row>

      <Card title={t('backupDr.retention.policyCardTitle')} loading={isLoading}>
        <Space orientation="vertical" size={12} style={{ width: '100%' }}>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {t('backupDr.retention.cloudProvider')}:{' '}
            <Tag>{data?.cloudProvider ?? 'Filesystem'}</Tag>
            {data?.cloudConfigured ? (
              <Tag color="success">{t('backupDr.retention.cloudReady')}</Tag>
            ) : (
              <Tag>{t('backupDr.retention.cloudNotConfigured')}</Tag>
            )}
          </Typography.Paragraph>
          <Form<FormValues>
            form={form}
            layout="vertical"
            disabled={!canEdit}
            onFinish={onSave}
          >
            <Row gutter={16}>
              <Col xs={24} md={8}>
                <Form.Item
                  name="hotRetentionDays"
                  label={t('backupDr.retention.hotDays')}
                  rules={[{ required: true }]}
                >
                  <InputNumber min={7} max={90} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col xs={24} md={8}>
                <Form.Item
                  name="warmRetentionDays"
                  label={t('backupDr.retention.warmDays')}
                  rules={[{ required: true }]}
                >
                  <InputNumber min={30} max={365} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col xs={24} md={8}>
                <Form.Item
                  name="coldRetentionYears"
                  label={t('backupDr.retention.coldYears')}
                  extra={t('backupDr.retention.coldYearsHint')}
                  rules={[{ required: true }]}
                >
                  <InputNumber min={7} max={10} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
            </Row>
            <Form.Item
              name="coldStorageEnabled"
              label={t('backupDr.retention.coldEnabled')}
              valuePropName="checked"
            >
              <Switch />
            </Form.Item>
            {canEdit ? (
              <Button type="primary" htmlType="submit" loading={save.isPending}>
                {t('backupDr.retention.save')}
              </Button>
            ) : (
              <Alert type="warning" showIcon title={t('backupDr.retention.superAdminOnly')} />
            )}
          </Form>
        </Space>
      </Card>

      <BackupStorageCostsDashboard />
    </Space>
  );
}
