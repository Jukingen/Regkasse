'use client';

import {
  CopyOutlined,
  DeleteOutlined,
  DownloadOutlined,
  EyeOutlined,
  HistoryOutlined,
  KeyOutlined,
  PlayCircleOutlined,
} from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Badge,
  Button,
  Card,
  Checkbox,
  DatePicker,
  Descriptions,
  Empty,
  Form,
  Input,
  List,
  Modal,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import Link from 'next/link';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'next/navigation';

import { getAdminCashRegisters } from '@/api/admin-rksv/client';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import { PageSkeleton, TableSkeleton } from '@/components/Skeleton';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { DownloadPreviewModal } from '@/components/ui/DownloadPreviewModal';
import { FilePreviewModal } from '@/components/ui/FilePreviewModal';
import { recordDownloadHistory } from '@/features/download-history/api/downloadHistoryApi';
import { DownloadHistoryPanel } from '@/features/download-history/components/DownloadHistoryPanel';
import { useExportDownloadNotifications } from '@/hooks/useExportDownloadNotifications';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { DepExportValidationCard } from '@/features/rksv/dep-export-compliance/DepExportValidationCard';
import { useCryptoMaterial } from '@/features/rksv/hooks/useCryptoMaterial';
import { useDepExport } from '@/features/rksv/hooks/useDepExport';
import {
  type DepExportHistoryItem,
  type DepExportScheduleItem,
  createDepExportSchedule,
  deactivateDepExportSchedule,
  deleteDepExportHistory,
  depExportHistoryQueryKey,
  depExportSchedulesQueryKey,
  downloadDepExportHistoryFile,
  fetchDepExportHistoryBlob,
  fetchDepExportHistoryDetail,
  isDepExportHistoryCompleted,
  normalizeDepExportHistoryStatus,
  useDepExportHistory,
  useDepExportLastExport,
  useDepExportSchedules,
  useDepExportStatus,
} from '@/features/rksv/hooks/useDepExportHistory';
import { ensureDepExportApiErrorTranslations } from '@/features/rksv/depExportApiErrors';
import { resolveValidationBadgeStatus } from '@/features/rksv/hooks/useDepExportValidation';
import {
  type CryptoMaterial,
  type DepExportLiveMeta,
  type DepExportRequestParams,
  type RksvDepExportRoot,
  computeDepExportStats,
} from '@/features/rksv/types/depExport';
import { buildDepExportFileName } from '@/features/rksv/utils/depExportFileName';
import { useTenant } from '@/features/tenancy/providers/TenantProvider';
import { useAuth } from '@/features/auth/hooks/useAuth';
import {
  EXPORT_TEMPLATE_QUERY_KEY,
  resolveTemplatePeriod,
} from '@/features/exports/applyExportTemplate';
import { getExportTemplateById } from '@/features/exports/exportTemplatesStorage';
import { DateColumn, dateColumnRender } from '@/components/DateColumn';
import { useI18n } from '@/i18n/I18nProvider';
import { formatBytes, formatDate, formatDateTime } from '@/i18n/formatting';
import {
  createJsonExportBlob,
  estimateJsonByteSize,
  saveBlobToFolder,
  triggerBlobDownload,
} from '@/lib/download/exportDownload';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

const { RangePicker } = DatePicker;

type ScheduleFormValues = {
  scheduleType: string;
  recipientEmails?: string;
};

const DEFAULT_SCHEDULE_DAY_OF_MONTH = 1;
const DEFAULT_SCHEDULE_TIME_OF_DAY = '02:00';
const EXPORT_READY_NOTIFY_KEY = 'dep-export-ready';
const RECENT_EXPORTS_LIMIT = 5;

const PRUEFTOOL_COMMAND =
  '.\\scripts\\verify-rksv-dep-export.ps1 -DepExportPath "./dep-export.json" -CryptoMaterialPath "./crypto-material.json"';

ensureDepExportApiErrorTranslations();

type DepDownloadPreviewState = {
  fileName: string;
  sizeBytes: number;
  createdAt: Date | string;
  contentSummary: string;
  registerName?: string;
  isSizeEstimate?: boolean;
  /** Prebuilt blob for live export; history loads on demand. */
  blob?: Blob;
  historyId?: string;
};

function formatRegisterLabel(registerNumber?: string, location?: string, id?: string): string {
  if (registerNumber && location) return `${registerNumber} — ${location}`;
  if (registerNumber) return registerNumber;
  if (location) return location;
  return id ? `${id.slice(0, 8)}…` : '—';
}

type DepExportSchedulesTabProps = {
  tp: (path: string) => string;
  formatLocale: string;
  selectedRegisterId?: string;
  schedules: DepExportScheduleItem[] | undefined;
  schedulesLoading: boolean;
  onRefetchSchedules: () => Promise<unknown>;
};

function DepExportSchedulesTab({
  tp,
  formatLocale,
  selectedRegisterId,
  schedules,
  schedulesLoading,
  onRefetchSchedules,
}: DepExportSchedulesTabProps) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [scheduleForm] = Form.useForm<ScheduleFormValues>();
  const [scheduleSaving, setScheduleSaving] = useState(false);

  const handleScheduleExport = async (values: ScheduleFormValues) => {
    if (!selectedRegisterId) {
      notify.warning('rksvHub.depExportPage.selectRegisterWarning');
      return;
    }

    setScheduleSaving(true);
    try {
      await createDepExportSchedule({
        cashRegisterId: selectedRegisterId,
        scheduleType: values.scheduleType,
        dayOfMonth: DEFAULT_SCHEDULE_DAY_OF_MONTH,
        timeOfDay: DEFAULT_SCHEDULE_TIME_OF_DAY,
        recipientEmails: values.recipientEmails?.trim() || null,
      });
      scheduleForm.resetFields();
      await onRefetchSchedules();
      void queryClient.invalidateQueries({ queryKey: depExportSchedulesQueryKey });
      notify.successKey('rksvHub.depExportPage.scheduleCreated');
    } catch (err) {
      notify.apiError(err, {
        logContext: 'RKSV.depExport.scheduleCreate',
        fallbackKey: 'rksvHub.depExportPage.scheduleCreateFailed',
      });
    } finally {
      setScheduleSaving(false);
    }
  };

  const handleDeactivateSchedule = async (scheduleId: string) => {
    try {
      await deactivateDepExportSchedule(scheduleId);
      await onRefetchSchedules();
      notify.successKey('rksvHub.depExportPage.scheduleDeactivated');
    } catch (err) {
      notify.apiError(err, {
        logContext: 'RKSV.depExport.scheduleDeactivate',
        fallbackKey: 'rksvHub.depExportPage.scheduleDeactivateFailed',
      });
    }
  };

  const scheduleColumns: ColumnsType<DepExportScheduleItem> = [
    { title: tp('scheduleColumnType'), dataIndex: 'scheduleType', key: 'scheduleType' },
    {
      title: tp('scheduleColumnNextRun'),
      dataIndex: 'nextRunAt',
      key: 'nextRunAt',
      render: dateColumnRender('datetime'),
    },
    {
      title: tp('scheduleRecipientsPlaceholder'),
      dataIndex: 'recipientEmails',
      key: 'recipientEmails',
      render: (v: string | null | undefined) => v ?? '—',
    },
    {
      title: tp('historyColumnActions'),
      key: 'actions',
      render: (_, row) => (
        <Button size="small" danger onClick={() => void handleDeactivateSchedule(row.id)}>
          {tp('scheduleDeactivateButton')}
        </Button>
      ),
    },
  ];

  return (
    <Card size="small">
      <Space orientation="vertical" size="large" style={{ width: '100%' }}>
        <Alert
          type="info"
          showIcon
          title={tp('scheduleTitle')}
          description={tp('scheduleDescription')}
        />
        <Form
          form={scheduleForm}
          layout="vertical"
          onFinish={(values) => void handleScheduleExport(values)}
          initialValues={{ scheduleType: 'Monthly' }}
        >
          <Form.Item name="scheduleType" label={tp('schedulePeriodPlaceholder')}>
            <Select
              placeholder={tp('schedulePeriodPlaceholder')}
              options={[
                { value: 'Daily', label: tp('scheduleTypeDaily') },
                { value: 'Weekly', label: tp('scheduleTypeWeekly') },
                { value: 'Monthly', label: tp('scheduleTypeMonthly') },
                { value: 'Yearly', label: tp('scheduleTypeYearly') },
              ]}
            />
          </Form.Item>
          <Form.Item name="recipientEmails" label={tp('scheduleRecipientsPlaceholder')}>
            <Input placeholder={tp('scheduleRecipientsPlaceholder')} />
          </Form.Item>
          <Form.Item>
            <Button
              type="primary"
              htmlType="submit"
              loading={scheduleSaving}
              disabled={!selectedRegisterId}
            >
              {tp('scheduleSubmitButton')}
            </Button>
          </Form.Item>
        </Form>
        {schedulesLoading ? (
          <TableSkeleton rows={4} cols={4} />
        ) : schedules?.length ? (
          <Table
            rowKey="id"
            size="small"
            pagination={false}
            dataSource={schedules}
            columns={scheduleColumns}
          />
        ) : (
          <Empty description={tp('scheduleEmpty')} />
        )}
      </Space>
    </Card>
  );
}

export function DepExportTestPage() {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const { notification, modal } = useAntdApp();
  const exportNotify = useExportDownloadNotifications();
  const { tenant } = useTenant();
  const { user } = useAuth();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const tp = useCallback((path: string) => t(`rksvHub.depExportPage.${path}`), [t]);

  const [selectedRegisterId, setSelectedRegisterId] = useState<string | undefined>();
  const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null]>([
    dayjs().subtract(7, 'day').startOf('day'),
    dayjs().endOf('day'),
  ]);
  const [includeSpecialReceipts, setIncludeSpecialReceipts] = useState(true);
  const [includeDailyClosings, setIncludeDailyClosings] = useState(true);
  const [templateAppliedHint, setTemplateAppliedHint] = useState<string | null>(null);
  const [exportResult, setExportResult] = useState<RksvDepExportRoot | null>(null);
  const [exportMeta, setExportMeta] = useState<DepExportLiveMeta | null>(null);
  const [cryptoMaterial, setCryptoMaterial] = useState<CryptoMaterial | null>(null);
  const [activeTab, setActiveTab] = useState('export');
  const [historyPage, setHistoryPage] = useState(1);
  const [viewingHistory, setViewingHistory] = useState<DepExportHistoryItem | null>(null);
  const [viewingHistoryId, setViewingHistoryId] = useState<string | null>(null);
  const [downloadPreview, setDownloadPreview] = useState<DepDownloadPreviewState | null>(null);
  const [downloadBusy, setDownloadBusy] = useState(false);
  const [filePreviewOpen, setFilePreviewOpen] = useState(false);

  const { data: cashRegisters, isLoading: registersLoading } = useQuery({
    queryKey: rksvAdminQueryKeys.cashRegisters,
    queryFn: getAdminCashRegisters,
    staleTime: 60_000,
  });

  const { mutate: fetchDepExport, isPending } = useDepExport();
  const { mutate: fetchCryptoMaterial, isPending: cryptoLoading } = useCryptoMaterial();
  const { data: historyData, isLoading: historyLoading } = useDepExportHistory(
    selectedRegisterId,
    historyPage
  );
  const { data: recentHistoryData, isLoading: recentHistoryLoading } = useDepExportHistory(
    selectedRegisterId,
    1
  );
  const { data: depExportStatus } = useDepExportStatus(selectedRegisterId);
  const { data: lastExport } = useDepExportLastExport(selectedRegisterId);
  const {
    data: schedules,
    isLoading: schedulesLoading,
    refetch: refetchSchedules,
  } = useDepExportSchedules();

  const registerOptions = useMemo(
    () =>
      (cashRegisters ?? [])
        .filter((register) => typeof register.id === 'string' && register.id.length > 0)
        .map((register) => ({
          value: register.id as string,
          label: formatRegisterLabel(register.registerNumber, register.location, register.id),
          registerNumber: register.registerNumber ?? '',
        })),
    [cashRegisters]
  );

  // Apply export template from ?exportTemplateId=
  useEffect(() => {
    const templateId = searchParams.get(EXPORT_TEMPLATE_QUERY_KEY)?.trim();
    if (!templateId) return;
    const tmpl = getExportTemplateById(templateId, {
      userId: user?.id ?? 'anon',
      tenantId: tenant?.id ?? 'default',
    });
    if (!tmpl || tmpl.config.kind !== 'dep-export') return;

    const cfg = tmpl.config;
    const range = resolveTemplatePeriod(cfg.period, cfg.customFromUtc, cfg.customToUtc);
    setDateRange([range.from, range.to]);
    setIncludeSpecialReceipts(cfg.includeSpecialReceipts);
    setIncludeDailyClosings(cfg.includeDailyClosings);

    if (cfg.cashRegisterId) {
      setSelectedRegisterId(cfg.cashRegisterId);
    } else if (cfg.registerNumberHint && cashRegisters?.length) {
      const hint = cfg.registerNumberHint.trim().toLowerCase();
      const match = cashRegisters.find(
        (r) => (r.registerNumber ?? '').trim().toLowerCase() === hint
      );
      if (match?.id) setSelectedRegisterId(match.id);
    }

    setTemplateAppliedHint(tmpl.name);
  }, [searchParams, user?.id, tenant?.id, cashRegisters]);

  const stats = useMemo(() => computeDepExportStats(exportResult), [exportResult]);
  const previewJson = exportResult ? JSON.stringify(exportResult, null, 2) : '';
  const recentExports = useMemo(
    () =>
      (recentHistoryData?.items ?? [])
        .filter((row) => row.canDelete !== false)
        .slice(0, RECENT_EXPORTS_LIMIT),
    [recentHistoryData?.items]
  );

  const downloadStoredExport = useCallback(
    async (exportId: string, fileName?: string | null) => {
      const name =
        fileName?.trim() ||
        `dep-export-${new Date().toISOString().replaceAll(':', '-').replaceAll('.', '-')}.json`;
      try {
        await downloadDepExportHistoryFile(exportId, name);
        notification.destroy(EXPORT_READY_NOTIFY_KEY);
        notify.successKey('rksvHub.depExportPage.downloadSuccess');
      } catch (err) {
        const apiCode =
          err &&
          typeof err === 'object' &&
          'response' in err &&
          err.response &&
          typeof err.response === 'object' &&
          'data' in err.response &&
          err.response.data &&
          typeof err.response.data === 'object' &&
          'code' in err.response.data
            ? String((err.response.data as { code?: string }).code ?? '')
            : '';
        notify.apiError(err, {
          logContext: 'RKSV.depExport.download',
          fallbackKey:
            apiCode === 'RKSV_DEP_EXPORT_EXPIRED'
              ? 'rksvHub.depExportPage.exportExpired'
              : 'rksvHub.depExportPage.downloadFailed',
        });
      }
    },
    [notification, notify]
  );

  const deleteRecentExport = useCallback(
    (row: DepExportHistoryItem) => {
      if (!row.canDelete) return;
      modal.confirm({
        title: tp('deleteConfirmTitle'),
        content: tp('deleteConfirmBody'),
        okText: tp('deleteButton'),
        okButtonProps: { danger: true },
        onOk: async () => {
          try {
            await deleteDepExportHistory(row.id);
            void queryClient.invalidateQueries({ queryKey: ['rksv', 'dep-export', 'history'] });
            notify.successKey('rksvHub.depExportPage.deleteSuccess');
          } catch (err) {
            notify.apiError(err, {
              logContext: 'RKSV.depExport.delete',
              fallbackKey: 'rksvHub.depExportPage.deleteFailed',
            });
          }
        },
      });
    },
    [modal, notify, queryClient, tp]
  );

  const buildRequestParams = (): DepExportRequestParams | null => {
    const fromUtc = dateRange?.[0]?.toISOString();
    const toUtc = dateRange?.[1]?.toISOString();
    if (!selectedRegisterId || !fromUtc || !toUtc) return null;
    return {
      cashRegisterId: selectedRegisterId,
      fromUtc,
      toUtc,
      includeSpecialReceipts,
      includeDailyClosings,
    };
  };

  const handleExport = () => {
    const params = buildRequestParams();
    if (!params) {
      if (!selectedRegisterId) {
        notify.warning('rksvHub.depExportPage.selectRegisterWarning');
      } else {
        notify.warning('rksvHub.depExportPage.selectDateRangeWarning');
      }
      return;
    }

    fetchDepExport(params, {
      onSuccess: (data) => {
        setExportResult(data.dep);
        setExportMeta(data.meta);
        void queryClient.invalidateQueries({
          queryKey: ['rksv', 'dep-export', 'history'],
        });
        void queryClient.invalidateQueries({
          queryKey: ['rksv', 'dep-export', 'status'],
        });
        void queryClient.invalidateQueries({
          queryKey: ['rksv', 'dep-export', 'last-export'],
        });

        const exportId = data.meta.exportId ?? data.meta.historyId ?? null;
        if (exportId) {
          const readyDescription = data.meta.expiresAt
            ? `${t('rksvHub.depExportPage.exportReady')} ${t('rksvHub.depExportPage.expiresLabel')}: ${formatDateTime(data.meta.expiresAt, formatLocale)}`
            : t('rksvHub.depExportPage.exportReady');

          notify.successKey('rksvHub.depExportPage.exportSuccess', undefined, {
            mode: 'notification',
            description: readyDescription,
            duration: 30,
            key: EXPORT_READY_NOTIFY_KEY,
            btn: (
              <Button
                type="primary"
                icon={<DownloadOutlined />}
                onClick={() => void downloadStoredExport(exportId, data.meta.fileName)}
              >
                {t('rksvHub.depExportPage.downloadNow')}
              </Button>
            ),
          });
        } else {
          notify.successKey('rksvHub.depExportPage.exportSuccess');
        }
      },
      onError: (error) => {
        notify.apiError(error, {
          logContext: 'RKSV.depExport.export',
          fallbackKey: 'rksvHub.depExportPage.exportFailed',
        });
      },
    });
  };

  const handleDownload = () => {
    if (!exportResult || !selectedRegisterId) return;
    const register = (cashRegisters ?? []).find((r) => r.id === selectedRegisterId);
    const createdAt = new Date();
    const fileName = buildDepExportFileName(tenant?.slug, register?.registerNumber, createdAt);
    const estimatedBytes = estimateJsonByteSize(exportResult);
    const blob = createJsonExportBlob(exportResult);
    setDownloadPreview({
      fileName,
      sizeBytes: blob.size || estimatedBytes,
      createdAt,
      contentSummary: t('common.exportDownload.contentDep', {
        signatures: stats?.totalSignatures ?? 0,
        groups: stats?.groupCount ?? 0,
      }),
      registerName: formatRegisterLabel(register?.registerNumber, register?.location, register?.id),
      blob,
    });
  };

  const downloadCryptoMaterial = () => {
    if (!cryptoMaterial || !selectedRegisterId) return;
    const createdAt = new Date();
    const fileName = `crypto-material-${selectedRegisterId}.json`;
    const estimatedBytes = estimateJsonByteSize(cryptoMaterial);
    const blob = createJsonExportBlob(cryptoMaterial);
    setDownloadPreview({
      fileName,
      sizeBytes: blob.size || estimatedBytes,
      createdAt,
      contentSummary: t('common.exportDownload.contentGeneric'),
      registerName: formatRegisterLabel(
        (cashRegisters ?? []).find((r) => r.id === selectedRegisterId)?.registerNumber,
        (cashRegisters ?? []).find((r) => r.id === selectedRegisterId)?.location,
        selectedRegisterId
      ),
      blob,
    });
  };

  const handleGenerateCryptoMaterial = () => {
    if (!selectedRegisterId) {
      notify.warning('rksvHub.depExportPage.selectRegisterWarning');
      return;
    }

    fetchCryptoMaterial(selectedRegisterId, {
      onSuccess: (data) => {
        setCryptoMaterial(data);
        notify.successKey('rksvHub.depExportPage.cryptoMaterialSuccess');
      },
      onError: (error) => {
        notify.apiError(error, {
          logContext: 'RKSV.depExport.cryptoMaterial',
          fallbackKey: 'rksvHub.depExportPage.cryptoMaterialFailed',
        });
      },
    });
  };

  const handleCopy = async () => {
    if (!exportResult) return;
    try {
      await navigator.clipboard.writeText(JSON.stringify(exportResult, null, 2));
      notify.successKey('rksvHub.depExportPage.copySuccess');
    } catch {
      notify.errorKey('rksvHub.depExportPage.copyFailed');
    }
  };

  const handleCopyPrueftoolCommand = async () => {
    try {
      await navigator.clipboard.writeText(PRUEFTOOL_COMMAND);
      notify.successKey('rksvHub.depExportPage.copyCommandSuccess');
    } catch {
      notify.errorKey('rksvHub.depExportPage.copyFailed');
    }
  };

  const applyHistoryEntry = (entry: DepExportHistoryItem) => {
    setSelectedRegisterId(entry.cashRegisterId);
    setDateRange([dayjs(entry.fromUtc), dayjs(entry.toUtc)]);
    setIncludeSpecialReceipts(entry.includeSpecialReceipts);
    setIncludeDailyClosings(entry.includeDailyClosings);
    setActiveTab('export');
    notify.info('rksvHub.depExportPage.historyParamsLoaded');
  };

  const resolvePreviewBlob = async (preview: DepDownloadPreviewState): Promise<Blob> => {
    if (preview.blob) return preview.blob;
    if (preview.historyId) {
      const stored = await fetchDepExportHistoryBlob(preview.historyId);
      return stored.blob;
    }
    throw new Error('No export payload available');
  };

  const confirmPreviewDownload = async () => {
    if (!downloadPreview) return;
    setDownloadBusy(true);
    const fileName = downloadPreview.fileName;
    exportNotify.notifyPreparing({ fileName });
    try {
      const blob = await resolvePreviewBlob(downloadPreview);
      triggerBlobDownload(blob, fileName);
      try {
        await recordDownloadHistory({
          fileName,
          fileType: 'json',
          fileSize: blob.size,
          downloadUrl: downloadPreview.historyId
            ? `/api/admin/rksv/dep-export/download/${downloadPreview.historyId}`
            : undefined,
          sourceKind: downloadPreview.historyId ? 'dep-export' : 'dep-export-live',
          sourceId: downloadPreview.historyId ?? null,
        });
      } catch {
        // History write is best-effort; download already started.
      }
      exportNotify.notifyCompleted({
        fileName,
        onRetry: () => void confirmPreviewDownload(),
        onOpenFolder: () => void confirmPreviewSaveToFolder(),
      });
      setDownloadPreview(null);
    } catch {
      exportNotify.notifyFailed({
        fileName,
        onRetry: () => void confirmPreviewDownload(),
      });
    } finally {
      setDownloadBusy(false);
    }
  };

  const confirmPreviewSaveToFolder = async () => {
    if (!downloadPreview) return;
    setDownloadBusy(true);
    const fileName = downloadPreview.fileName;
    exportNotify.notifyPreparing({ fileName });
    try {
      const blob = await resolvePreviewBlob(downloadPreview);
      const result = await saveBlobToFolder(blob, fileName);
      if (result === 'cancelled') {
        exportNotify.closePanel();
        return;
      }
      try {
        await recordDownloadHistory({
          fileName,
          fileType: 'json',
          fileSize: blob.size,
          downloadUrl: downloadPreview.historyId
            ? `/api/admin/rksv/dep-export/download/${downloadPreview.historyId}`
            : undefined,
          sourceKind: downloadPreview.historyId ? 'dep-export' : 'dep-export-live',
          sourceId: downloadPreview.historyId ?? null,
        });
      } catch {
        // best-effort
      }
      exportNotify.notifyCompleted({
        fileName,
        onRetry: () => void confirmPreviewSaveToFolder(),
        onOpenFolder: () => void confirmPreviewSaveToFolder(),
      });
      setDownloadPreview(null);
    } catch {
      exportNotify.notifyFailed({
        fileName,
        onRetry: () => void confirmPreviewSaveToFolder(),
      });
    } finally {
      setDownloadBusy(false);
    }
  };

  const viewExport = async (historyId: string) => {
    setViewingHistoryId(historyId);
    try {
      const detail = await fetchDepExportHistoryDetail(historyId);
      setViewingHistory(detail);
    } catch (err) {
      notify.apiError(err, {
        logContext: 'RKSV.depExport.historyView',
        fallbackKey: 'rksvHub.depExportPage.historyViewLoadFailed',
      });
      setViewingHistory(null);
    } finally {
      setViewingHistoryId(null);
    }
  };

  const renderHistoryStatusTag = (status: DepExportHistoryItem['status'] | number | string) => {
    const normalized = normalizeDepExportHistoryStatus(status);
    const color =
      normalized === 'Completed'
        ? 'green'
        : normalized === 'Failed'
          ? 'red'
          : normalized === 'Processing'
            ? 'blue'
            : 'default';
    return <Tag color={color}>{normalized}</Tag>;
  };

  const historyColumns: ColumnsType<DepExportHistoryItem> = [
    {
      title: tp('historyColumnDate'),
      dataIndex: 'exportedAt',
      key: 'exportedAt',
      render: dateColumnRender('datetime'),
    },
    {
      title: tp('historyColumnPeriod'),
      key: 'period',
      render: (_, row) => (
        <>
          <DateColumn date={row.fromUtc} format="short" /> —{' '}
          <DateColumn date={row.toUtc} format="short" />
        </>
      ),
    },
    {
      title: tp('historyColumnSignatures'),
      dataIndex: 'signatureCount',
      key: 'signatureCount',
    },
    {
      title: tp('historyColumnPrueftool'),
      key: 'prueftoolCompatible',
      render: (_, row) => {
        const compatible = row.prueftoolCompatible ?? (row.legacyJwsCount ?? 0) === 0;
        return (
          <Tag color={compatible ? 'green' : 'orange'}>
            {compatible ? tp('historyPrueftoolYes') : tp('historyPrueftoolNo')}
          </Tag>
        );
      },
    },
    {
      title: tp('historyColumnSize'),
      dataIndex: 'fileSizeBytes',
      key: 'fileSizeBytes',
      render: (value: number) => formatBytes(value, formatLocale),
    },
    {
      title: tp('historyColumnStatus'),
      dataIndex: 'status',
      key: 'status',
      render: (status: DepExportHistoryItem['status'], row) => (
        <Space size={4} wrap>
          {renderHistoryStatusTag(status)}
          {row.isSimulated ? <Tag color="orange">{tp('simulatedBadge')}</Tag> : null}
        </Space>
      ),
    },
    {
      title: tp('historyColumnValidation'),
      dataIndex: 'validationStatus',
      key: 'validationStatus',
      render: (status: string | null | undefined) => (
        <Space size={4}>
          <Badge status={resolveValidationBadgeStatus(status)} />
          <span>{status ?? tp('historyValidationPending')}</span>
        </Space>
      ),
    },
    {
      title: tp('historyColumnActions'),
      key: 'actions',
      render: (_, row) => (
        <Space wrap>
          <Button
            type="primary"
            size="small"
            icon={<DownloadOutlined />}
            disabled={!row.hasStoredFile || !isDepExportHistoryCompleted(row.status)}
            onClick={() => void downloadStoredExport(row.id, row.fileName)}
          >
            {tp('downloadButton')}
          </Button>
          <Button
            size="small"
            icon={<EyeOutlined />}
            aria-label={tp('historyViewTitle')}
            loading={viewingHistoryId === row.id}
            onClick={() => void viewExport(row.id)}
          />
          <Button size="small" onClick={() => applyHistoryEntry(row)}>
            {tp('historyLoadParams')}
          </Button>
          <Button
            size="small"
            danger
            icon={<DeleteOutlined />}
            disabled={row.canDelete === false}
            onClick={() => deleteRecentExport(row)}
          >
            {tp('deleteButton')}
          </Button>
        </Space>
      ),
    },
  ];

  const exportTab = (
    <Card size="small">
      <Space orientation="vertical" size="large" style={{ width: '100%' }}>
        <div>
          <Typography.Text type="secondary">{tp('lastExportLabel')}: </Typography.Text>
          {lastExport?.hasExport ? (
            <Space size={8} wrap>
              <Typography.Text strong>
                {lastExport.formatted ??
                  (lastExport.lastExportAt
                    ? formatDateTime(lastExport.lastExportAt, formatLocale)
                    : '—')}
              </Typography.Text>
              {lastExport.isSimulated ? <Tag color="orange">{tp('simulatedBadge')}</Tag> : null}
              {typeof lastExport.downloadCount === 'number' ? (
                <Typography.Text type="secondary">
                  {t('rksvHub.depExportPage.downloadCountLabel', {
                    count: String(lastExport.downloadCount),
                  })}
                </Typography.Text>
              ) : null}
            </Space>
          ) : (
            <Typography.Text type="secondary">{tp('noExportYet')}</Typography.Text>
          )}
        </div>

        <div>
          <Typography.Text strong>{tp('cashRegisterLabel')}</Typography.Text>
          <Select
            placeholder={tp('cashRegisterPlaceholder')}
            style={{ width: '100%', marginTop: 8 }}
            loading={registersLoading}
            allowClear
            value={selectedRegisterId}
            onChange={setSelectedRegisterId}
            options={registerOptions}
          />
        </div>

        <div>
          <Typography.Text strong>{tp('dateRangeLabel')}</Typography.Text>
          <RangePicker
            showTime
            format="DD.MM.YYYY HH:mm:ss"
            style={{ width: '100%', marginTop: 8 }}
            value={[dateRange[0], dateRange[1]]}
            onChange={(dates) =>
              setDateRange((dates as [Dayjs | null, Dayjs | null] | null) ?? [null, null])
            }
            placeholder={[tp('dateRangeStart'), tp('dateRangeEnd')]}
          />
        </div>

        <div>
          <Typography.Text strong>{tp('optionsLabel')}</Typography.Text>
          <Space wrap style={{ marginTop: 8 }}>
            <Checkbox
              checked={includeSpecialReceipts}
              onChange={(event) => setIncludeSpecialReceipts(event.target.checked)}
            >
              {tp('includeSpecialReceipts')}
            </Checkbox>
            <Checkbox
              checked={includeDailyClosings}
              onChange={(event) => setIncludeDailyClosings(event.target.checked)}
            >
              {tp('includeDailyClosings')}
            </Checkbox>
          </Space>
        </div>

        <Space wrap>
          <Button
            type="primary"
            icon={<PlayCircleOutlined />}
            onClick={handleExport}
            loading={isPending}
            disabled={!selectedRegisterId || !dateRange[0] || !dateRange[1]}
          >
            {tp('exportButton')}
          </Button>
          {exportMeta?.exportId || exportMeta?.historyId ? (
            <Button
              type="primary"
              icon={<DownloadOutlined />}
              onClick={() =>
                void downloadStoredExport(
                  exportMeta.exportId ?? exportMeta.historyId!,
                  exportMeta.fileName
                )
              }
            >
              {tp('downloadStoredButton')}
            </Button>
          ) : null}
          {exportResult ? (
            <>
              <Button icon={<EyeOutlined />} onClick={() => setFilePreviewOpen(true)}>
                {t('common.filePreview.open')}
              </Button>
              <Button icon={<DownloadOutlined />} onClick={handleDownload}>
                {tp('downloadLiveButton')}
              </Button>
              <Button icon={<CopyOutlined />} onClick={() => void handleCopy()}>
                {tp('copyButton')}
              </Button>
            </>
          ) : null}
          <Button
            icon={<KeyOutlined />}
            onClick={handleGenerateCryptoMaterial}
            loading={cryptoLoading}
            disabled={!selectedRegisterId}
          >
            {tp('cryptoMaterialGenerateButton')}
          </Button>
          {cryptoMaterial ? (
            <Button icon={<DownloadOutlined />} onClick={downloadCryptoMaterial}>
              {tp('cryptoMaterialDownloadButton')}
            </Button>
          ) : null}
        </Space>

        <Card size="small" title={tp('historyTitle')}>
          {recentHistoryLoading ? (
            <TableSkeleton rows={3} cols={3} />
          ) : recentExports.length > 0 ? (
            <List
              size="small"
              dataSource={recentExports}
              renderItem={(row) => (
                <List.Item
                  actions={[
                    <Button
                      key="download"
                      type="link"
                      icon={<DownloadOutlined />}
                      disabled={!row.hasStoredFile || !isDepExportHistoryCompleted(row.status)}
                      onClick={() => void downloadStoredExport(row.id, row.fileName)}
                    >
                      {tp('downloadButton')}
                    </Button>,
                    <Button
                      key="delete"
                      type="link"
                      danger
                      icon={<DeleteOutlined />}
                      disabled={row.canDelete === false}
                      onClick={() => deleteRecentExport(row)}
                    >
                      {tp('deleteButton')}
                    </Button>,
                  ]}
                >
                  <List.Item.Meta
                    title={
                      <Space wrap size={8}>
                        <Typography.Text>{row.fileName}</Typography.Text>
                        {renderHistoryStatusTag(row.status)}
                        {row.isSimulated ? <Tag color="orange">{tp('simulatedBadge')}</Tag> : null}
                        {!row.hasStoredFile ? (
                          <Tag>{tp('historyDownloadUnavailable')}</Tag>
                        ) : null}
                      </Space>
                    }
                    description={
                      <Space wrap size={8} split={<Typography.Text type="secondary">·</Typography.Text>}>
                        <Typography.Text type="secondary">
                          {formatDateTime(row.exportedAt, formatLocale)}
                        </Typography.Text>
                        <Typography.Text type="secondary">
                          {row.registerNumber ?? row.cashRegisterId}
                        </Typography.Text>
                        <Typography.Text type="secondary">
                          {formatBytes(row.fileSizeBytes, formatLocale)}
                        </Typography.Text>
                        {row.expiresAt ? (
                          <Typography.Text type="secondary">
                            {tp('expiresLabel')}: {formatDateTime(row.expiresAt, formatLocale)}
                          </Typography.Text>
                        ) : null}
                        {typeof row.downloadCount === 'number' && row.downloadCount > 0 ? (
                          <Typography.Text type="secondary">
                            {t('rksvHub.depExportPage.downloadCount', {
                              count: String(row.downloadCount),
                            })}
                          </Typography.Text>
                        ) : null}
                      </Space>
                    }
                  />
                </List.Item>
              )}
            />
          ) : (
            <Empty description={tp('noExports')} />
          )}
          <Button type="link" style={{ paddingInline: 0, marginTop: 8 }} onClick={() => setActiveTab('history')}>
            {tp('recentExportsViewAll')}
          </Button>
        </Card>

        {exportMeta && exportMeta.legacyJwsCount > 0 ? (
          <Alert
            type="warning"
            showIcon
            title={tp('legacyJwsAlertTitle')}
            description={
              exportMeta.legacyJwsWarning?.trim()
                ? exportMeta.legacyJwsWarning
                : t('rksvHub.depExportPage.legacyJwsAlertBody', {
                    count: String(exportMeta.legacyJwsCount),
                  })
            }
          />
        ) : null}

        {stats ? (
          <Alert
            type="info"
            showIcon
            title={tp('statsTitle')}
            description={
              <Descriptions size="small" column={2} style={{ marginTop: 8 }}>
                <Descriptions.Item label={tp('statsGroups')}>{stats.groupCount}</Descriptions.Item>
                <Descriptions.Item label={tp('statsSignatures')}>
                  {stats.totalSignatures}
                </Descriptions.Item>
              </Descriptions>
            }
          />
        ) : null}

        {exportResult ? (
          <Alert
            type="success"
            showIcon
            title={tp('jsonPreviewTitle')}
            description={t('common.filePreview.openHint')}
            action={
              <Button size="small" icon={<EyeOutlined />} onClick={() => setFilePreviewOpen(true)}>
                {t('common.filePreview.open')}
              </Button>
            }
          />
        ) : (
          <Alert
            type="info"
            showIcon
            title={tp('emptyHintTitle')}
            description={tp('emptyHintBody')}
          />
        )}
      </Space>
    </Card>
  );

  const verifyTab = (
    <Card size="small">
      <Alert type="info" showIcon title={tp('verifyTitle')} description={tp('verifyDescription')} />
      <Space orientation="vertical" size="middle" style={{ width: '100%', marginTop: 16 }}>
        <Space wrap>
          <Button icon={<DownloadOutlined />} onClick={handleDownload} disabled={!exportResult}>
            {tp('verifyDownloadButton')}
          </Button>
          <Button
            icon={<KeyOutlined />}
            onClick={handleGenerateCryptoMaterial}
            loading={cryptoLoading}
            disabled={!selectedRegisterId}
          >
            {tp('cryptoMaterialGenerateButton')}
          </Button>
          {cryptoMaterial ? (
            <Button icon={<DownloadOutlined />} onClick={downloadCryptoMaterial}>
              {tp('cryptoMaterialDownloadButton')}
            </Button>
          ) : null}
        </Space>
        <div style={{ padding: 12, background: '#f5f5f5', borderRadius: 6 }}>
          <Typography.Text code style={{ whiteSpace: 'pre-wrap', fontSize: 12 }}>
            {PRUEFTOOL_COMMAND}
          </Typography.Text>
        </div>
        <Space wrap>
          <Button icon={<CopyOutlined />} onClick={() => void handleCopyPrueftoolCommand()}>
            {tp('verifyCopyCommand')}
          </Button>
        </Space>
        <Alert
          type="warning"
          showIcon
          title={tp('verifyPrerequisiteTitle')}
          description={tp('verifyPrerequisiteBody')}
        />
      </Space>
    </Card>
  );

  const certificateTab = (
    <Card size="small">
      <Alert
        type="info"
        showIcon
        title={tp('certificateTitle')}
        description={tp('certificateDescription')}
      />
      <Space orientation="vertical" size="middle" style={{ width: '100%', marginTop: 16 }}>
        {stats?.certificateThumbprints.length ? (
          <Descriptions bordered size="small" column={1} title={tp('certificateGroupsTitle')}>
            {stats.certificateThumbprints.map((thumbprint, index) => (
              <Descriptions.Item
                key={`${thumbprint}-${index}`}
                label={`${tp('certificateGroupLabel')} ${index + 1}`}
              >
                <Typography.Text code>{thumbprint}…</Typography.Text>
              </Descriptions.Item>
            ))}
          </Descriptions>
        ) : (
          <Empty description={tp('certificateEmpty')} />
        )}
        <Button href="/rksv/cmc-certificate">{tp('certificateOpenCmc')}</Button>
      </Space>
    </Card>
  );

  const historyTab = (
    <Card size="small">
      {historyLoading ? (
        <TableSkeleton rows={6} cols={5} />
      ) : historyData?.items.length ? (
        <Table
          rowKey="id"
          size="small"
          pagination={{
            current: historyPage,
            pageSize: 20,
            total: historyData.totalCount,
            onChange: setHistoryPage,
          }}
          columns={historyColumns}
          dataSource={historyData.items}
        />
      ) : (
        <Empty
          image={<HistoryOutlined style={{ fontSize: 48, color: '#bfbfbf' }} />}
          description={tp('historyEmpty')}
        />
      )}
      <Typography.Paragraph
        type="secondary"
        style={{ marginTop: 16, marginBottom: 0, fontSize: 12 }}
      >
        {tp('historyFootnoteServer')}
      </Typography.Paragraph>
    </Card>
  );

  const tabItems = [
    { key: 'export', label: tp('tabExport'), children: exportTab },
    { key: 'verify', label: tp('tabVerify'), children: verifyTab },
    { key: 'certificate', label: tp('tabCertificate'), children: certificateTab },
    { key: 'history', label: tp('tabHistory'), children: historyTab },
    { key: 'downloads', label: tp('tabDownloads'), children: <DownloadHistoryPanel /> },
    {
      key: 'schedule',
      label: tp('tabSchedules'),
      children: (
        <DepExportSchedulesTab
          tp={tp}
          formatLocale={formatLocale}
          selectedRegisterId={selectedRegisterId}
          schedules={schedules}
          schedulesLoading={schedulesLoading}
          onRefetchSchedules={refetchSchedules}
        />
      ),
    },
  ];

  return (
    <>
      <AdminPageHeader
        title={tp('title')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('adminShell.group.rksv'), href: '/rksv' },
          { title: tp('breadcrumb') },
        ]}
      >
        <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
          {tp('subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {templateAppliedHint ? (
        <Alert
          type="success"
          showIcon
          closable
          onClose={() => setTemplateAppliedHint(null)}
          style={{ marginBottom: 16 }}
          title={t('common.exportTemplates.appliedOnPage', { name: templateAppliedHint })}
        />
      ) : null}

      {(depExportStatus?.isSimulated || exportMeta?.isSimulated) && (
        <Alert
          type="warning"
          showIcon
          closable
          style={{ marginBottom: 16 }}
          title={tp('simulationWarning')}
          description={tp('simulationDescription')}
        />
      )}

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        title={tp('scopeTitle')}
        description={
          <span>
            {tp('scopeBody')}{' '}
            <Link href="/rksv/fiscal-export-diagnostics">{tp('scopeFiscalExportLink')}</Link>.
          </span>
        }
      />

      {registersLoading ? (
        <PageSkeleton widgets={3} />
      ) : (
        <>
          <Tabs activeKey={activeTab} onChange={setActiveTab} items={tabItems} />
          <Modal
            title={tp('historyViewTitle')}
            open={viewingHistory != null}
            onCancel={() => setViewingHistory(null)}
            footer={
              <Space>
                {viewingHistory?.hasStoredFile ? (
                  <Button
                    type="primary"
                    icon={<DownloadOutlined />}
                    onClick={() =>
                      viewingHistory &&
                      void downloadStoredExport(viewingHistory.id, viewingHistory.fileName)
                    }
                  >
                    {tp('downloadButton')}
                  </Button>
                ) : null}
                <Button
                  onClick={() => {
                    if (viewingHistory) applyHistoryEntry(viewingHistory);
                    setViewingHistory(null);
                  }}
                >
                  {tp('historyLoadParams')}
                </Button>
                <Button type="primary" onClick={() => setViewingHistory(null)}>
                  {t('common.close')}
                </Button>
              </Space>
            }
            width={720}
          >
            {viewingHistory ? (
              <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                <Descriptions bordered size="small" column={1}>
                  <Descriptions.Item label={tp('historyColumnDate')}>
                    {formatDateTime(viewingHistory.exportedAt, formatLocale)}
                  </Descriptions.Item>
                  <Descriptions.Item label={tp('historyColumnPeriod')}>
                    {formatDate(viewingHistory.fromUtc, formatLocale)} —{' '}
                    {formatDate(viewingHistory.toUtc, formatLocale)}
                  </Descriptions.Item>
                  <Descriptions.Item label={tp('historyColumnRegister')}>
                    {viewingHistory.registerNumber ?? viewingHistory.cashRegisterId}
                  </Descriptions.Item>
                  <Descriptions.Item label={tp('historyColumnSignatures')}>
                    {viewingHistory.signatureCount}
                  </Descriptions.Item>
                  <Descriptions.Item label={tp('statsGroups')}>
                    {viewingHistory.groupCount}
                  </Descriptions.Item>
                  <Descriptions.Item label={tp('historyColumnSize')}>
                    {formatBytes(viewingHistory.fileSizeBytes, formatLocale)}
                  </Descriptions.Item>
                  <Descriptions.Item label={tp('historyColumnStatus')}>
                    <Space size={4} wrap>
                      {renderHistoryStatusTag(viewingHistory.status)}
                      {viewingHistory.isSimulated ? (
                        <Tag color="orange">{tp('simulatedBadge')}</Tag>
                      ) : null}
                    </Space>
                  </Descriptions.Item>
                  {viewingHistory.errorMessage ? (
                    <Descriptions.Item label={tp('historyColumnError')}>
                      {viewingHistory.errorMessage}
                    </Descriptions.Item>
                  ) : null}
                </Descriptions>
                <DepExportValidationCard exportId={viewingHistory.id} showExportPicker={false} />
              </Space>
            ) : null}
          </Modal>
          <DownloadPreviewModal
            open={downloadPreview != null}
            fileName={downloadPreview?.fileName ?? ''}
            fileSize={formatBytes(downloadPreview?.sizeBytes ?? 0, formatLocale)}
            fileType="JSON"
            createdAt={formatDateTime(downloadPreview?.createdAt ?? new Date(), formatLocale, {
              second: '2-digit',
            })}
            contentSummary={downloadPreview?.contentSummary ?? ''}
            tenantName={tenant?.name ?? tenant?.slug}
            registerName={downloadPreview?.registerName}
            hint={t('common.exportDownload.hintDep')}
            sizeBytes={downloadPreview?.sizeBytes}
            isSizeEstimate={downloadPreview?.isSizeEstimate}
            contentPreview={downloadPreview?.blob ? { blob: downloadPreview.blob } : null}
            resolveContentPreview={
              downloadPreview
                ? async () => ({ blob: await resolvePreviewBlob(downloadPreview) })
                : undefined
            }
            confirmLoading={downloadBusy}
            onCancel={() => {
              if (!downloadBusy) setDownloadPreview(null);
            }}
            onConfirm={() => void confirmPreviewDownload()}
            onSaveToFolder={() => void confirmPreviewSaveToFolder()}
            enableSendEmail
            defaultEmailTo={user?.email ?? ''}
            emailSubject={
              downloadPreview
                ? t('common.exportEmail.defaultSubjectDep', {
                    date: formatDateTime(downloadPreview.createdAt, formatLocale, {
                      day: '2-digit',
                      month: '2-digit',
                      year: 'numeric',
                    }),
                  })
                : undefined
            }
            emailSourceKind={downloadPreview?.historyId ? 'dep-export' : undefined}
            emailSourceId={downloadPreview?.historyId ?? null}
            resolveEmailContent={
              downloadPreview
                ? async () => resolvePreviewBlob(downloadPreview)
                : undefined
            }
          />
          <FilePreviewModal
            open={filePreviewOpen && exportResult != null}
            fileName={
              selectedRegisterId
                ? buildDepExportFileName(
                    tenant?.slug,
                    (cashRegisters ?? []).find((r) => r.id === selectedRegisterId)?.registerNumber,
                    new Date()
                  )
                : 'dep-export.json'
            }
            fileType="json"
            source={{ text: previewJson }}
            onClose={() => setFilePreviewOpen(false)}
            onDownload={() => {
              setFilePreviewOpen(false);
              handleDownload();
            }}
          />
        </>
      )}
    </>
  );
}
