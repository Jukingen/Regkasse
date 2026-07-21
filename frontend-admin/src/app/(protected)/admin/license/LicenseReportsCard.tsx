'use client';

import { DownloadOutlined, ReloadOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  DatePicker,
  Descriptions,
  Form,
  Space,
  Switch,
  Typography,
} from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
/**
 * License export & summary (<c>/api/admin/licenses/*</c>). German operator copy via <c>license.reports.*</c>.
 */
import React, { useState } from 'react';

import {
  type LicenseExportReportParams,
  downloadLicenseExportFile,
  getLicenseReportSummary,
  licenseQueryKeys,
} from '@/api/manual/adminLicense';
import { FormSkeleton } from '@/components/Skeleton';
import { useAntdApp } from '@/hooks/useAntdApp';
import { formatGermanDateTime, useI18n } from '@/i18n';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';
import {
  LICENSE_DEPLOYMENT_FEATURE,
  deploymentLicenseAllows,
} from '@/shared/licenseDeploymentFeatures';

dayjs.extend(utc);

type FormVals = {
  dateRange?: [Dayjs, Dayjs] | null;
  includeActivationHistory: boolean;
  maskLicenseKeys: boolean;
};

function triggerBrowserDownload(blob: Blob, filename: string) {
  const url = globalThis.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.rel = 'noopener';
  document.body.appendChild(a);
  a.click();
  a.remove();
  globalThis.URL.revokeObjectURL(url);
}

type Props = {
  /** From GET /api/license/status — gates bulk export APIs. */
  enabledLicenseFeatures?: readonly string[] | null;
};

export function LicenseReportsCard({ enabledLicenseFeatures }: Props) {
  const { message } = useAntdApp();

  const { t, formatLocale } = useI18n();
  const [form] = Form.useForm<FormVals>();
  const [applied, setApplied] = useState<LicenseExportReportParams>({
    includeActivationHistory: false,
    maskLicenseKeys: true,
  });
  const [downloading, setDownloading] = useState<'csv' | 'json' | null>(null);

  const summaryQuery = useQuery({
    queryKey: licenseQueryKeys.reportSummary(applied),
    queryFn: () => getLicenseReportSummary(applied),
  });

  const buildParamsFromForm = (values: FormVals): LicenseExportReportParams => {
    const next: LicenseExportReportParams = {
      includeActivationHistory: values.includeActivationHistory,
      maskLicenseKeys: values.maskLicenseKeys,
    };
    const r = values.dateRange;
    if (r?.[0] && r[1]) {
      next.fromUtc = r[0].utc().startOf('day').toISOString();
      next.toUtc = r[1].utc().endOf('day').toISOString();
    }
    return next;
  };

  const onApply = (values: FormVals) => {
    setApplied(buildParamsFromForm(values));
  };

  const onReset = () => {
    form.setFieldsValue({
      dateRange: undefined,
      includeActivationHistory: false,
      maskLicenseKeys: true,
    });
    setApplied({ includeActivationHistory: false, maskLicenseKeys: true });
  };

  const onDownload = async (format: 'csv' | 'json') => {
    const values = form.getFieldsValue() as FormVals;
    const params = buildParamsFromForm(values);
    setDownloading(format);
    try {
      const blob = await downloadLicenseExportFile(format, params);
      const stamp = dayjs.utc().format('YYYYMMDD_HHmmss');
      const ext = format === 'csv' ? 'csv' : 'json';
      triggerBrowserDownload(blob, `licenses_export_${stamp}_UTC.${ext}`);
      message.success(t('license.reports.exportSuccess'));
    } catch {
      message.error(t('license.reports.exportFailed'));
    } finally {
      setDownloading(null);
    }
  };

  const s = summaryQuery.data;

  const canExport =
    deploymentLicenseAllows(
      enabledLicenseFeatures,
      LICENSE_DEPLOYMENT_FEATURE.AdminLicenseManage
    ) && deploymentLicenseAllows(enabledLicenseFeatures, LICENSE_DEPLOYMENT_FEATURE.AdminRksv);

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      <Alert type="info" showIcon title={t('license.reports.scheduledEmailHint')} />

      {!canExport ? (
        <Alert type="warning" showIcon title={t('license.reports.exportLicenseGated')} />
      ) : null}

      <Card title={t('license.reports.filtersTitle')}>
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            includeActivationHistory: false,
            maskLicenseKeys: true,
          }}
          onFinish={onApply}
        >
          <Form.Item name="dateRange" label={t('license.reports.dateRangeIssuedAt')}>
            <DatePicker.RangePicker
              format={DAYJS_DATE_FORMAT}
              allowClear
              style={{ width: '100%', maxWidth: 400 }}
            />
          </Form.Item>
          <Form.Item
            name="includeActivationHistory"
            label={t('license.reports.includeActivationHistory')}
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>
          <Form.Item
            name="maskLicenseKeys"
            label={t('license.reports.maskLicenseKeys')}
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>
          <Form.Item>
            <Space wrap>
              <Button type="primary" htmlType="submit">
                {t('license.reports.applyFilters')}
              </Button>
              <Button onClick={onReset}>{t('license.reports.resetFilters')}</Button>
              <Button
                icon={<DownloadOutlined />}
                loading={downloading === 'csv'}
                disabled={!canExport}
                onClick={() => void onDownload('csv')}
              >
                {t('license.reports.downloadCsv')}
              </Button>
              <Button
                icon={<DownloadOutlined />}
                loading={downloading === 'json'}
                disabled={!canExport}
                onClick={() => void onDownload('json')}
              >
                {t('license.reports.downloadJson')}
              </Button>
              <Button
                icon={<ReloadOutlined />}
                onClick={() => void summaryQuery.refetch()}
                loading={summaryQuery.isFetching}
              >
                {t('license.reports.refreshSummary')}
              </Button>
            </Space>
          </Form.Item>
        </Form>
      </Card>

      <Card title={t('license.reports.summaryTitle')}>
        {summaryQuery.isLoading ? (
          <FormSkeleton fields={6} loading />
        ) : summaryQuery.isError ? (
          <Typography.Text type="danger">{t('license.reports.summaryLoadError')}</Typography.Text>
        ) : s ? (
          <Descriptions bordered column={1} size="small">
            <Descriptions.Item label={t('license.reports.generatedAt')}>
              {formatGermanDateTime(s.generatedAtUtc)}
            </Descriptions.Item>
            <Descriptions.Item label={t('license.reports.issuedTotal')}>
              {s.issuedTotalInDateFilter}
            </Descriptions.Item>
            <Descriptions.Item label={t('license.reports.issuedActive')}>
              {s.issuedActiveEligible}
            </Descriptions.Item>
            <Descriptions.Item label={t('license.reports.issuedRevoked')}>
              {s.issuedRevoked}
            </Descriptions.Item>
            <Descriptions.Item label={t('license.reports.issuedCancelled')}>
              {s.issuedCancelled}
            </Descriptions.Item>
            <Descriptions.Item label={t('license.reports.issuedDeleted')}>
              {s.issuedDeleted}
            </Descriptions.Item>
            <Descriptions.Item label={t('license.reports.expiring30')}>
              {s.expiringWithin30Days}
            </Descriptions.Item>
            <Descriptions.Item label={t('license.reports.expiring15')}>
              {s.expiringWithin15Days}
            </Descriptions.Item>
            <Descriptions.Item label={t('license.reports.expiring7')}>
              {s.expiringWithin7Days}
            </Descriptions.Item>
            <Descriptions.Item label={t('license.reports.uniqueDevices')}>
              {s.uniqueActivatedDevices}
            </Descriptions.Item>
            <Descriptions.Item label={t('license.reports.activationAttempts')}>
              {s.activationAttemptsInDateFilter}
            </Descriptions.Item>
          </Descriptions>
        ) : null}
      </Card>
    </Space>
  );
}
