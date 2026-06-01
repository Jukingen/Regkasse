'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * On-premise license status, activation, issuance, and issued-license audit list (German operator copy via i18n `license.*`).
 */

import React, { useEffect, useMemo, useState } from 'react';
import axios from 'axios';
import { useSearchParams } from 'next/navigation';
import { Modal, Alert, Button, Card, Col, Collapse, Descriptions, Dropdown, Form, Input, InputNumber, Row, Space, Spin, Table, Tabs, Tag, Typography } from 'antd';
import type { MenuProps } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
    CalendarOutlined,
    CloseOutlined,
    CopyOutlined,
    DeleteOutlined,
    DownOutlined,
    InfoCircleOutlined,
    ReloadOutlined,
    StopOutlined,
    SyncOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { useI18n, formatDate } from '@/i18n';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { tenantStorage } from '@/features/auth/services/tenantStorage';
import { useDebounce } from '@/hooks/useDebounce';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';
import {
    deleteIssuedLicenseSoft,
    deleteRevokeIssuedLicense,
    getDeploymentLicenseStatus,
    getIssuedLicenseDetail,
    getIssuedLicensesList,
    getPublicLicenseStatus,
    licenseQueryKeys,
    postActivateLicense,
    postCancelIssuedLicense,
    postExtendIssuedLicense,
    postRevokeIssuedLicenseById,
    postUnregisterIssuedLicenseMachine,
    type ActivateLicenseRequest,
    type GenerateLicenseResponse,
    type IssuedLicenseActivationDto,
    type IssuedLicenseListItemDto,
} from '@/api/manual/adminLicense';
import {
    getLicenseStatusDayText,
    getLicenseStatusLabel,
    getLicenseStatusMessage,
    getLicenseStatusTagColor,
    resolveDeploymentLicenseStatus,
} from '@/features/license/utils/licenseStatus';
import { readAdminLicensePagePrefill } from '@/features/license/utils/adminLicenseRoute';
import { LicenseGenerationCard } from './LicenseGenerationCard';
import { IssuedLicenseUpgradeModal } from './IssuedLicenseUpgradeModal';
import { LicenseActivationHistoryCard } from './LicenseActivationHistoryCard';
import { LicenseReportsCard } from './LicenseReportsCard';

type LicenseFormValues = {
    licenseKey: string;
};

async function copyTextToClipboard(value: string): Promise<boolean> {
    try {
        await navigator.clipboard.writeText(value);
        return true;
    } catch {
        const ta = document.createElement('textarea');
        ta.value = value;
        ta.style.position = 'fixed';
        ta.style.opacity = '0';
        document.body.appendChild(ta);
        ta.select();
        try {
            document.execCommand('copy');
            return true;
        } catch {
            return false;
        } finally {
            ta.remove();
        }
    }
}

function IssuedLicensesTableCard() {
    const { t, formatLocale } = useI18n();
    const { user } = useAuth();
    const queryClient = useQueryClient();
    const [extendForm] = Form.useForm<{ addDays?: number; addMonths?: number }>();
    const [searchInput, setSearchInput] = useState('');
    const debouncedSearch = useDebounce(searchInput, 400);
    const [fingerprintSearchInput, setFingerprintSearchInput] = useState('');
    const debouncedFingerprintSearch = useDebounce(fingerprintSearchInput, 400);
    const [pageNumber, setPageNumber] = useState(1);
    const [pageSize, setPageSize] = useState(50);
    const [revokeTarget, setRevokeTarget] = useState<IssuedLicenseListItemDto | null>(null);
    const [upgradeModalRow, setUpgradeModalRow] = useState<IssuedLicenseListItemDto | null>(null);
    const [extendRow, setExtendRow] = useState<IssuedLicenseListItemDto | null>(null);
    const [extendResult, setExtendResult] = useState<GenerateLicenseResponse | null>(null);
    const [superRevokeTarget, setSuperRevokeTarget] = useState<IssuedLicenseListItemDto | null>(null);
    const [cancelTarget, setCancelTarget] = useState<IssuedLicenseListItemDto | null>(null);
    const [cancelReason, setCancelReason] = useState('');
    const [deleteTarget, setDeleteTarget] = useState<IssuedLicenseListItemDto | null>(null);
    const [unregisterTarget, setUnregisterTarget] = useState<IssuedLicenseListItemDto | null>(null);
    const [unregisterResult, setUnregisterResult] = useState<GenerateLicenseResponse | null>(null);
    const [detailsLicenseId, setDetailsLicenseId] = useState<string | null>(null);

    const canManage = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
    const canSuper = hasPermission(user, PERMISSIONS.LICENSE_LIFECYCLE_SUPER);

    useEffect(() => {
        setPageNumber(1);
    }, [debouncedSearch, debouncedFingerprintSearch]);

    useEffect(() => {
        if (extendRow) {
            extendForm.setFieldsValue({ addDays: 30, addMonths: 0 });
        }
    }, [extendRow, extendForm]);

    function isRenewable(row: IssuedLicenseListItemDto): boolean {
        return (
            !row.isRevoked &&
            !row.supersededByLicenseId &&
            !row.transferredToLicenseId &&
            !row.isCancelled &&
            !row.isDeleted
        );
    }

    function canSuperExtendRow(row: IssuedLicenseListItemDto): boolean {
        return (
            !row.isCancelled &&
            !row.isRevoked &&
            !row.supersededByLicenseId &&
            !row.transferredToLicenseId
        );
    }

    function canSuperUnregisterRow(row: IssuedLicenseListItemDto): boolean {
        return Boolean(row.requireFingerprint && canSuperExtendRow(row));
    }

    const listParams = useMemo(
        () => ({
            search: debouncedSearch.trim() || undefined,
            machineFingerprint: debouncedFingerprintSearch.trim() || undefined,
            pageNumber,
            pageSize,
        }),
        [debouncedSearch, debouncedFingerprintSearch, pageNumber, pageSize],
    );

    const listQuery = useQuery({
        queryKey: licenseQueryKeys.list(listParams),
        queryFn: () => getIssuedLicensesList(listParams),
    });

    const detailsQuery = useQuery({
        queryKey: ['admin', 'license', 'detail', detailsLicenseId] as const,
        queryFn: () => getIssuedLicenseDetail(detailsLicenseId!),
        enabled: Boolean(detailsLicenseId),
    });

    const extendMutation = useMutation({
        mutationFn: (args: { id: string; addDays?: number; addMonths?: number }) =>
            postExtendIssuedLicense(args.id, { addDays: args.addDays, addMonths: args.addMonths }),
        onSuccess: async (data) => {
            if (!data.success) {
                message.error(data.message || t('license.issued.super.extendFailed'));
                return;
            }
            message.success(t('license.issued.super.extendSuccess'));
            setExtendRow(null);
            extendForm.resetFields();
            setExtendResult(data);
            await queryClient.invalidateQueries({ queryKey: licenseQueryKeys.listRoot });
        },
        onError: (err: unknown) => {
            if (axios.isAxiosError(err)) {
                const data = err.response?.data as { message?: string } | undefined;
                message.error(data?.message || t('license.issued.super.extendFailed'));
                return;
            }
            message.error(t('license.issued.super.extendFailed'));
        },
    });

    const superRevokeMutation = useMutation({
        mutationFn: (id: string) => postRevokeIssuedLicenseById(id),
        onSuccess: async () => {
            message.success(t('license.issued.super.superRevokeSuccess'));
            setSuperRevokeTarget(null);
            await queryClient.invalidateQueries({ queryKey: licenseQueryKeys.listRoot });
        },
        onError: (err: unknown) => {
            if (axios.isAxiosError(err)) {
                const data = err.response?.data as { message?: string } | undefined;
                message.error(data?.message || t('license.issued.super.superRevokeFailed'));
                return;
            }
            message.error(t('license.issued.super.superRevokeFailed'));
        },
    });

    const cancelMutation = useMutation({
        mutationFn: (args: { id: string; reason?: string | null }) =>
            postCancelIssuedLicense(args.id, args.reason ? { reason: args.reason } : {}),
        onSuccess: async () => {
            message.success(t('license.issued.super.cancelSuccess'));
            setCancelTarget(null);
            setCancelReason('');
            await queryClient.invalidateQueries({ queryKey: licenseQueryKeys.listRoot });
        },
        onError: (err: unknown) => {
            if (axios.isAxiosError(err)) {
                const data = err.response?.data as { message?: string } | undefined;
                message.error(data?.message || t('license.issued.super.cancelFailed'));
                return;
            }
            message.error(t('license.issued.super.cancelFailed'));
        },
    });

    const deleteMutation = useMutation({
        mutationFn: (id: string) => deleteIssuedLicenseSoft(id),
        onSuccess: async () => {
            message.success(t('license.issued.super.deleteSuccess'));
            setDeleteTarget(null);
            await queryClient.invalidateQueries({ queryKey: licenseQueryKeys.listRoot });
        },
        onError: (err: unknown) => {
            if (axios.isAxiosError(err)) {
                const data = err.response?.data as { message?: string } | undefined;
                message.error(data?.message || t('license.issued.super.deleteFailed'));
                return;
            }
            message.error(t('license.issued.super.deleteFailed'));
        },
    });

    const unregisterMutation = useMutation({
        mutationFn: (id: string) => postUnregisterIssuedLicenseMachine(id),
        onSuccess: async (data) => {
            if (!data.success) {
                message.error(data.message || t('license.issued.super.unregisterFailed'));
                return;
            }
            message.success(t('license.issued.super.unregisterSuccess'));
            setUnregisterTarget(null);
            setUnregisterResult(data);
            await queryClient.invalidateQueries({ queryKey: licenseQueryKeys.listRoot });
        },
        onError: (err: unknown) => {
            if (axios.isAxiosError(err)) {
                const data = err.response?.data as { message?: string } | undefined;
                message.error(data?.message || t('license.issued.super.unregisterFailed'));
                return;
            }
            message.error(t('license.issued.super.unregisterFailed'));
        },
    });

    const revokeMutation = useMutation({
        mutationFn: (id: string) => deleteRevokeIssuedLicense(id),
        onSuccess: async () => {
            message.success(t('license.issued.revokeSuccess'));
            setRevokeTarget(null);
            await queryClient.invalidateQueries({ queryKey: licenseQueryKeys.listRoot });
        },
        onError: (err: unknown) => {
            if (axios.isAxiosError(err)) {
                const data = err.response?.data as { message?: string } | undefined;
                message.error(data?.message || t('license.issued.revokeFailed'));
                return;
            }
            message.error(t('license.issued.revokeFailed'));
        },
    });

    const activationColumns: ColumnsType<IssuedLicenseActivationDto> = useMemo(
        () => [
            {
                title: t('license.issued.super.colFp'),
                dataIndex: 'machineFingerprint',
                key: 'machineFingerprint',
                ellipsis: true,
                render: (fp: string) => (
                    <Typography.Text code style={{ fontSize: 11, wordBreak: 'break-all' }}>
                        {fp}
                    </Typography.Text>
                ),
            },
            {
                title: t('license.issued.super.colActivated'),
                dataIndex: 'activatedAtUtc',
                key: 'activatedAtUtc',
                width: 140,
                render: (iso: string) =>
                    formatDate(iso, formatLocale, {
                        year: 'numeric',
                        month: '2-digit',
                        day: '2-digit',
                        hour: '2-digit',
                        minute: '2-digit',
                    }),
            },
            {
                title: t('license.issued.super.colLastSeen'),
                dataIndex: 'lastSeenAtUtc',
                key: 'lastSeenAtUtc',
                width: 140,
                render: (iso: string) =>
                    formatDate(iso, formatLocale, {
                        year: 'numeric',
                        month: '2-digit',
                        day: '2-digit',
                        hour: '2-digit',
                        minute: '2-digit',
                    }),
            },
            {
                title: t('license.issued.super.colValidUntil'),
                dataIndex: 'validUntilUtc',
                key: 'validUntilUtc',
                width: 140,
                render: (iso: string) =>
                    formatDate(iso, formatLocale, {
                        year: 'numeric',
                        month: '2-digit',
                        day: '2-digit',
                        hour: '2-digit',
                        minute: '2-digit',
                    }),
            },
            {
                title: t('license.issued.super.colCust'),
                dataIndex: 'customerName',
                key: 'customerName',
                ellipsis: true,
            },
        ],
        [formatLocale, t],
    );

    const columns: ColumnsType<IssuedLicenseListItemDto> = useMemo(
        () => [
            {
                title: t('license.issued.columns.customerName'),
                dataIndex: 'customerName',
                key: 'customerName',
                ellipsis: true,
            },
            {
                title: t('license.issued.columns.licenseKey'),
                dataIndex: 'licenseKey',
                key: 'licenseKey',
                render: (key: string) => (
                    <Space.Compact style={{ maxWidth: '100%' }}>
                        <Typography.Text
                            code
                            style={{
                                flex: 1,
                                minWidth: 0,
                                fontSize: 12,
                                wordBreak: 'break-all',
                            }}
                        >
                            {key}
                        </Typography.Text>
                        <Button
                            type="default"
                            icon={<CopyOutlined />}
                            aria-label={t('license.issued.copy')}
                            onClick={async () => {
                                const ok = await copyTextToClipboard(key);
                                message[ok ? 'success' : 'error'](
                                    ok ? t('license.issued.copied') : t('license.issued.copyFailed'),
                                );
                            }}
                        >
                            {t('license.issued.copy')}
                        </Button>
                    </Space.Compact>
                ),
            },
            {
                title: t('license.issued.columns.machineFingerprintShort'),
                dataIndex: 'recentMachineFingerprintShort',
                key: 'recentMachineFingerprintShort',
                width: 160,
                ellipsis: true,
                render: (v: string | null | undefined) =>
                    v ? (
                        <Typography.Text code style={{ fontSize: 12 }}>
                            {v}
                        </Typography.Text>
                    ) : (
                        '—'
                    ),
            },
            {
                title: t('license.issued.columns.expiryDate'),
                dataIndex: 'expiryAtUtc',
                key: 'expiryAtUtc',
                width: 140,
                render: (iso: string) =>
                    formatDate(iso, formatLocale, {
                        year: 'numeric',
                        month: '2-digit',
                        day: '2-digit',
                    }),
            },
            {
                title: t('license.issued.columns.type'),
                key: 'type',
                width: 120,
                render: (_, row) =>
                    row.requireFingerprint ? (
                        <Tag color="blue">{t('license.issued.columns.typeBound')}</Tag>
                    ) : (
                        <Tag>{t('license.issued.columns.typeFloating')}</Tag>
                    ),
            },
            {
                title: t('license.issued.columns.status'),
                key: 'status',
                width: 120,
                render: (_, row) => {
                    if (row.isCancelled)
                        return <Tag color="magenta">{t('license.issued.columns.statusCancelled')}</Tag>;
                    if (row.isRevoked) return <Tag color="red">{t('license.issued.columns.statusRevoked')}</Tag>;
                    if (row.supersededByLicenseId)
                        return <Tag color="orange">{t('license.issued.columns.statusSuperseded')}</Tag>;
                    return <Tag color="green">{t('license.issued.columns.statusActive')}</Tag>;
                },
            },
            {
                title: t('license.issued.columns.lastActivation'),
                dataIndex: 'lastActivationAtUtc',
                key: 'lastActivationAtUtc',
                width: 150,
                render: (iso: string | null | undefined) =>
                    iso
                        ? formatDate(iso, formatLocale, {
                              year: 'numeric',
                              month: '2-digit',
                              day: '2-digit',
                              hour: '2-digit',
                              minute: '2-digit',
                          })
                        : '—',
            },
            {
                title: t('license.issued.columns.activatedDevices'),
                dataIndex: 'activatedDeviceCount',
                key: 'activatedDeviceCount',
                width: 120,
                align: 'right',
                render: (n: number | null | undefined) => (typeof n === 'number' ? n : 0),
            },
            {
                title: t('license.issued.columns.issuedDate'),
                dataIndex: 'issuedAtUtc',
                key: 'issuedAtUtc',
                width: 140,
                render: (iso: string) =>
                    formatDate(iso, formatLocale, {
                        year: 'numeric',
                        month: '2-digit',
                        day: '2-digit',
                    }),
            },
            {
                title: t('license.issued.columns.actions'),
                key: 'actions',
                width: 320,
                fixed: 'right',
                render: (_, row) => {
                    const renewable = isRenewable(row) && canManage;
                    const legacyRevoke = renewable && !canSuper;
                    const hasRowActions = renewable || legacyRevoke || canSuper;
                    if (!hasRowActions) {
                        return <span style={{ color: 'var(--ant-color-text-secondary)' }}>—</span>;
                    }

                    const superMenu = (): MenuProps => ({
                        items: [
                            {
                                key: 'extend',
                                icon: <CalendarOutlined />,
                                label: t('license.issued.super.extend'),
                                disabled: !canSuperExtendRow(row),
                            },
                            {
                                key: 'revoke',
                                icon: <StopOutlined />,
                                label: t('license.issued.super.revoke'),
                                disabled: row.isRevoked || Boolean(row.isCancelled),
                            },
                            {
                                key: 'cancel',
                                icon: <CloseOutlined />,
                                label: t('license.issued.super.cancel'),
                                disabled: Boolean(row.isCancelled),
                            },
                            { type: 'divider' },
                            {
                                key: 'unregister',
                                label: t('license.issued.super.unregister'),
                                disabled: !canSuperUnregisterRow(row),
                            },
                            {
                                key: 'delete',
                                icon: <DeleteOutlined />,
                                label: t('license.issued.super.delete'),
                                danger: true,
                            },
                            {
                                key: 'details',
                                icon: <InfoCircleOutlined />,
                                label: t('license.issued.super.details'),
                            },
                        ],
                        onClick: ({ key, domEvent }) => {
                            domEvent?.stopPropagation();
                            if (key === 'extend') setExtendRow(row);
                            else if (key === 'revoke') setSuperRevokeTarget(row);
                            else if (key === 'cancel') {
                                setCancelTarget(row);
                                setCancelReason('');
                            } else if (key === 'unregister') setUnregisterTarget(row);
                            else if (key === 'delete') setDeleteTarget(row);
                            else if (key === 'details') setDetailsLicenseId(row.id);
                        },
                    });

                    return (
                        <Space size="small" wrap>
                            {renewable ? (
                                <Button
                                    type="primary"
                                    size="small"
                                    icon={<SyncOutlined />}
                                    onClick={() => setUpgradeModalRow(row)}
                                >
                                    {t('license.issued.renewUpgrade')}
                                </Button>
                            ) : null}
                            {legacyRevoke ? (
                                <Button
                                    danger
                                    size="small"
                                    icon={<StopOutlined />}
                                    onClick={() => setRevokeTarget(row)}
                                >
                                    {t('license.issued.revoke')}
                                </Button>
                            ) : null}
                            {canSuper ? (
                                <Dropdown menu={superMenu()} trigger={['click']}>
                                    <Button size="small" icon={<DownOutlined />}>
                                        {t('license.issued.super.menu')}
                                    </Button>
                                </Dropdown>
                            ) : null}
                        </Space>
                    );
                },
            },
        ],
        [canManage, canSuper, formatLocale, t],
    );

    return (
        <Card title={t('license.issued.title')}>
            <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                <Input.Search
                    allowClear
                    placeholder={t('license.issued.searchPlaceholder')}
                    value={searchInput}
                    onChange={(e) => setSearchInput(e.target.value)}
                    style={{ maxWidth: 360 }}
                />
                <Input.Search
                    allowClear
                    placeholder={t('license.issued.fingerprintSearchPlaceholder')}
                    value={fingerprintSearchInput}
                    onChange={(e) => setFingerprintSearchInput(e.target.value)}
                    style={{ maxWidth: 360 }}
                />

                {listQuery.isError ? (
                    <Alert type="error" showIcon title={t('license.issued.loadError')} />
                ) : (
                    <Table<IssuedLicenseListItemDto>
                        rowKey="id"
                        size="small"
                        loading={listQuery.isFetching}
                        columns={columns}
                        dataSource={listQuery.data?.items ?? []}
                        locale={{ emptyText: t('license.issued.empty') }}
                        scroll={{ x: 1540 }}
                        pagination={{
                            current: pageNumber,
                            pageSize,
                            total: listQuery.data?.total ?? 0,
                            showSizeChanger: true,
                            pageSizeOptions: [25, 50, 100, 200],
                            showTotal: (total) => `${total}`,
                            onChange: (p, ps) => {
                                setPageNumber(p);
                                setPageSize(ps);
                            },
                        }}
                    />
                )}
            </Space>

            <Modal
                title={t('license.issued.revokeConfirmTitle')}
                open={revokeTarget !== null}
                okText={t('license.issued.revokeConfirmOk')}
                okType="danger"
                confirmLoading={revokeMutation.isPending}
                onCancel={() => {
                    if (!revokeMutation.isPending) {
                        setRevokeTarget(null);
                    }
                }}
                onOk={() => {
                    if (revokeTarget) {
                        revokeMutation.mutate(revokeTarget.id);
                    }
                }}
            >
                <Typography.Paragraph>{t('license.issued.revokeConfirmDescription')}</Typography.Paragraph>
                {revokeTarget ? (
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                        {t('license.issued.revokeConfirmCustomerHint', { customer: revokeTarget.customerName })}
                    </Typography.Paragraph>
                ) : null}
            </Modal>

            <Modal
                title={t('license.issued.super.extendModalTitle')}
                open={extendRow !== null}
                okText={t('license.issued.super.extendSubmit')}
                confirmLoading={extendMutation.isPending}
                onCancel={() => {
                    if (!extendMutation.isPending) {
                        setExtendRow(null);
                        extendForm.resetFields();
                    }
                }}
                onOk={async () => {
                    const v = extendForm.getFieldsValue();
                    const d = Number(v.addDays) || 0;
                    const m = Number(v.addMonths) || 0;
                    if (d <= 0 && m <= 0) {
                        message.warning(t('license.issued.super.extendNeedPositive'));
                        return;
                    }
                    if (extendRow) {
                        extendMutation.mutate({
                            id: extendRow.id,
                            addDays: d > 0 ? d : undefined,
                            addMonths: m > 0 ? m : undefined,
                        });
                    }
                }}
            >
                {extendRow ? (
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                        {extendRow.customerName}
                    </Typography.Paragraph>
                ) : null}
                <Form form={extendForm} layout="vertical">
                    <Form.Item name="addDays" label={t('license.issued.super.addDays')}>
                        <InputNumber min={0} max={3650} style={{ width: '100%' }} />
                    </Form.Item>
                    <Form.Item name="addMonths" label={t('license.issued.super.addMonths')}>
                        <InputNumber min={0} max={120} style={{ width: '100%' }} />
                    </Form.Item>
                </Form>
            </Modal>

            <Modal
                title={t('license.issued.super.extendResultTitle')}
                open={extendResult !== null && extendResult.success}
                footer={
                    <Button type="primary" onClick={() => setExtendResult(null)}>
                        {t('common.buttons.close')}
                    </Button>
                }
                onCancel={() => setExtendResult(null)}
                width={640}
            >
                <Typography.Paragraph type="secondary">{t('license.issued.super.extendResultHelp')}</Typography.Paragraph>
                {extendResult?.licenseKey ? (
                    <Descriptions bordered column={1} size="small" style={{ marginTop: 12 }}>
                        <Descriptions.Item label={t('license.generation.result.licenseKey')}>
                            <Typography.Paragraph copyable={{ text: extendResult.licenseKey }} style={{ marginBottom: 0 }}>
                                {extendResult.licenseKey}
                            </Typography.Paragraph>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('license.generation.result.expiry')}>
                            {extendResult.expiryAtUtc
                                ? formatDate(extendResult.expiryAtUtc, formatLocale, {
                                      year: 'numeric',
                                      month: '2-digit',
                                      day: '2-digit',
                                      hour: '2-digit',
                                      minute: '2-digit',
                                  })
                                : '—'}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('license.generation.result.signedJwt')}>
                            {(extendResult.signedJwt || extendResult.licenseJwt) ? (
                                <Typography.Paragraph
                                    copyable={{ text: extendResult.signedJwt || extendResult.licenseJwt || '' }}
                                    style={{ marginBottom: 0, wordBreak: 'break-all', fontSize: 12 }}
                                >
                                    {extendResult.signedJwt || extendResult.licenseJwt}
                                </Typography.Paragraph>
                            ) : (
                                '—'
                            )}
                        </Descriptions.Item>
                    </Descriptions>
                ) : null}
            </Modal>

            <Modal
                title={t('license.issued.super.superRevokeTitle')}
                open={superRevokeTarget !== null}
                okText={t('license.issued.super.superRevokeOk')}
                okType="danger"
                confirmLoading={superRevokeMutation.isPending}
                onCancel={() => {
                    if (!superRevokeMutation.isPending) {
                        setSuperRevokeTarget(null);
                    }
                }}
                onOk={() => {
                    if (superRevokeTarget) {
                        superRevokeMutation.mutate(superRevokeTarget.id);
                    }
                }}
            >
                <Typography.Paragraph>{t('license.issued.super.superRevokeBody')}</Typography.Paragraph>
                {superRevokeTarget ? (
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                        {superRevokeTarget.customerName}
                    </Typography.Paragraph>
                ) : null}
            </Modal>

            <Modal
                title={t('license.issued.super.cancelTitle')}
                open={cancelTarget !== null}
                okText={t('license.issued.super.cancelOk')}
                okType="danger"
                confirmLoading={cancelMutation.isPending}
                onCancel={() => {
                    if (!cancelMutation.isPending) {
                        setCancelTarget(null);
                        setCancelReason('');
                    }
                }}
                onOk={() => {
                    if (cancelTarget) {
                        const r = cancelReason.trim();
                        cancelMutation.mutate({ id: cancelTarget.id, reason: r.length > 0 ? r : undefined });
                    }
                }}
            >
                <Typography.Paragraph>{t('license.issued.super.cancelBody')}</Typography.Paragraph>
                <Input.TextArea
                    rows={3}
                    value={cancelReason}
                    onChange={(e) => setCancelReason(e.target.value)}
                    placeholder={t('license.issued.super.cancelReasonPlaceholder')}
                />
            </Modal>

            <Modal
                title={t('license.issued.super.deleteTitle')}
                open={deleteTarget !== null}
                okText={t('license.issued.super.deleteOk')}
                okType="danger"
                confirmLoading={deleteMutation.isPending}
                onCancel={() => {
                    if (!deleteMutation.isPending) {
                        setDeleteTarget(null);
                    }
                }}
                onOk={() => {
                    if (deleteTarget) {
                        deleteMutation.mutate(deleteTarget.id);
                    }
                }}
            >
                <Typography.Paragraph>{t('license.issued.super.deleteBody')}</Typography.Paragraph>
                {deleteTarget ? (
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                        {deleteTarget.customerName}
                    </Typography.Paragraph>
                ) : null}
            </Modal>

            <Modal
                title={t('license.issued.super.unregisterTitle')}
                open={unregisterTarget !== null}
                okText={t('license.issued.super.unregister')}
                confirmLoading={unregisterMutation.isPending}
                onCancel={() => {
                    if (!unregisterMutation.isPending) {
                        setUnregisterTarget(null);
                    }
                }}
                onOk={() => {
                    if (unregisterTarget) {
                        unregisterMutation.mutate(unregisterTarget.id);
                    }
                }}
            >
                <Typography.Paragraph>{t('license.issued.super.unregisterBody')}</Typography.Paragraph>
                {unregisterTarget ? (
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                        {unregisterTarget.customerName}
                    </Typography.Paragraph>
                ) : null}
            </Modal>

            <Modal
                title={t('license.issued.super.unregisterResultTitle')}
                open={unregisterResult !== null && unregisterResult.success}
                footer={
                    <Button type="primary" onClick={() => setUnregisterResult(null)}>
                        {t('common.buttons.close')}
                    </Button>
                }
                onCancel={() => setUnregisterResult(null)}
                width={640}
            >
                {unregisterResult?.signedJwt || unregisterResult?.licenseJwt ? (
                    <Typography.Paragraph
                        copyable={{ text: unregisterResult.signedJwt || unregisterResult.licenseJwt || '' }}
                        style={{ wordBreak: 'break-all', fontSize: 12 }}
                    >
                        {unregisterResult.signedJwt || unregisterResult.licenseJwt}
                    </Typography.Paragraph>
                ) : null}
            </Modal>

            <Modal
                title={t('license.issued.super.detailsTitle')}
                open={detailsLicenseId !== null}
                width={920}
                footer={null}
                destroyOnHidden
                onCancel={() => setDetailsLicenseId(null)}
            >
                {detailsQuery.isFetching ? (
                    <div style={{ display: 'flex', justifyContent: 'center', padding: 24 }}>
                        <Spin />
                    </div>
                ) : detailsQuery.isError ? (
                    <Alert type="error" showIcon title={t('license.issued.super.detailsLoadError')} />
                ) : detailsQuery.data ? (
                    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                            {t('license.issued.super.fullKeyHint')}
                        </Typography.Paragraph>
                        <Descriptions bordered column={1} size="small">
                            <Descriptions.Item label={t('license.issued.columns.customerName')}>
                                {detailsQuery.data.customerName}
                            </Descriptions.Item>
                            <Descriptions.Item label={t('license.issued.columns.licenseKey')}>
                                <Typography.Paragraph
                                    copyable={{ text: detailsQuery.data.licenseKey }}
                                    style={{ marginBottom: 0, wordBreak: 'break-all', fontFamily: 'ui-monospace, monospace' }}
                                >
                                    {detailsQuery.data.licenseKey}
                                </Typography.Paragraph>
                            </Descriptions.Item>
                            <Descriptions.Item label={t('license.issued.columns.expiryDate')}>
                                {formatDate(detailsQuery.data.expiryAtUtc, formatLocale, {
                                    year: 'numeric',
                                    month: '2-digit',
                                    day: '2-digit',
                                    hour: '2-digit',
                                    minute: '2-digit',
                                })}
                            </Descriptions.Item>
                            <Descriptions.Item label={t('license.issued.columns.type')}>
                                {detailsQuery.data.requireFingerprint
                                    ? t('license.issued.columns.typeBound')
                                    : t('license.issued.columns.typeFloating')}
                            </Descriptions.Item>
                            <Descriptions.Item label={t('license.issued.super.signedJwtLabel')}>
                                <Typography.Paragraph
                                    copyable={{ text: detailsQuery.data.signedJwt }}
                                    style={{ marginBottom: 0, wordBreak: 'break-all', fontSize: 11 }}
                                >
                                    {detailsQuery.data.signedJwt}
                                </Typography.Paragraph>
                            </Descriptions.Item>
                        </Descriptions>
                        <Typography.Title level={5} style={{ marginTop: 8, marginBottom: 0 }}>
                            {t('license.issued.super.activationHistory')}
                        </Typography.Title>
                        <Table<IssuedLicenseActivationDto>
                            rowKey={(a) => `${a.machineFingerprint}-${a.activatedAtUtc}`}
                            size="small"
                            pagination={false}
                            columns={activationColumns}
                            dataSource={detailsQuery.data.activations ?? []}
                            locale={{ emptyText: '—' }}
                        />
                    </Space>
                ) : null}
            </Modal>

            <IssuedLicenseUpgradeModal
                row={upgradeModalRow}
                onClose={() => setUpgradeModalRow(null)}
            />
        </Card>
    );
}

export default function AdminLicensePage() {
  const { message } = useAntdApp();

    const { t, formatLocale } = useI18n();
    const queryClient = useQueryClient();
    const { user } = useAuth();
    const [form] = Form.useForm<LicenseFormValues>();
    const searchParams = useSearchParams();

    const canActivate = Boolean(user?.permissions?.includes(PERMISSIONS.SETTINGS_MANAGE));

    const statusQuery = useQuery({
        queryKey: licenseQueryKeys.deploymentStatus,
        queryFn: () => getDeploymentLicenseStatus(),
    });

    const publicStatusQuery = useQuery({
        queryKey: licenseQueryKeys.publicStatus,
        queryFn: () => getPublicLicenseStatus(),
    });

    const activateMutation = useMutation({
        mutationFn: (body: ActivateLicenseRequest) => postActivateLicense(body),
        onSuccess: (res) => {
            if (!res.success) {
                message.error(res.message || t('license.activation.failureSimple'));
                return;
            }
            tenantStorage.persistBootstrap({
                tenantId: res.tenantId,
                tenantSlug: res.tenantSlug,
                apiBaseUrl: res.apiBaseUrl,
            });
            message.success(t('license.activation.successSimple'));
            form.resetFields(['licenseKey']);
            void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.deploymentStatus });
            void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.publicStatus });
            void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.activationAttemptsRoot });
            void queryClient.invalidateQueries({ queryKey: ['admin', 'licenses'] });
        },
        onError: (err: unknown) => {
            if (axios.isAxiosError(err)) {
                const data = err.response?.data as { message?: string } | undefined;
                message.error(data?.message || t('license.activation.failureSimple'));
                return;
            }
            message.error(t('license.activation.failureSimple'));
        },
    });

    const s = statusQuery.data;

    const enabledPublicLicenseFeatures = publicStatusQuery.data?.features ?? null;

    const resolvedStatus = useMemo(() => (s ? resolveDeploymentLicenseStatus(s) : null), [s]);
    const licensePagePrefill = useMemo(() => readAdminLicensePagePrefill(searchParams), [searchParams]);

    const machineHash = s?.machineHash?.trim() ?? '';
    const showDeploymentActivation = false;
    const showIssuedLicenses = false;

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <AdminPageHeader
                title={t('license.page.title')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t(ADMIN_NAV_LABEL_KEYS.settingsHub), href: '/settings' },
                    { title: t('license.page.title'), href: '/admin/license' },
                ]}
                actions={
                    <Button
                        icon={<ReloadOutlined />}
                        onClick={() => {
                            void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.deploymentStatus });
                            void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.publicStatus });
                            if (canActivate) {
                                void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.listRoot });
                                void queryClient.invalidateQueries({
                                    queryKey: licenseQueryKeys.activationAttemptsRoot,
                                });
                                void queryClient.invalidateQueries({ queryKey: ['admin', 'licenses'] });
                            }
                        }}
                    >
                        {t('common.buttons.refresh')}
                    </Button>
                }
            />

            <Card title={t('license.simpleUi.titlePublic')}>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                    {t('license.simpleUi.subtitlePublic')}
                </Typography.Paragraph>
                {publicStatusQuery.isLoading ? (
                    <div style={{ display: 'flex', justifyContent: 'center', padding: 24 }}>
                        <Spin />
                    </div>
                ) : publicStatusQuery.isError ? (
                    <Alert type="warning" showIcon title={t('license.publicStatus.loadError')} />
                ) : publicStatusQuery.data ? (
                    <Descriptions bordered column={1} size="small">
                        <Descriptions.Item label={t('license.simpleUi.status')}>
                            <Tag
                                color={
                                    publicStatusQuery.data.licenseType === 'Licensed' ||
                                    publicStatusQuery.data.licenseType === 'Paid'
                                        ? 'green'
                                        : publicStatusQuery.data.licenseType === 'Trial' ||
                                            publicStatusQuery.data.licenseType === 'Demo'
                                          ? 'blue'
                                          : 'red'
                                }
                            >
                                {publicStatusQuery.data.licenseType}
                            </Tag>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('license.simpleUi.validUntil')}>
                            {publicStatusQuery.data.validUntil
                                ? formatDate(publicStatusQuery.data.validUntil, formatLocale, {
                                      year: 'numeric',
                                      month: '2-digit',
                                      day: '2-digit',
                                      hour: '2-digit',
                                      minute: '2-digit',
                                  })
                                : '—'}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('license.simpleUi.daysRemaining')}>
                            {publicStatusQuery.data.daysRemaining}
                        </Descriptions.Item>
                    </Descriptions>
                ) : null}
            </Card>

            <Card>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
                    {t('license.supportNotice')}
                </Typography.Paragraph>

                {statusQuery.isLoading ? (
                    <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
                        <Spin />
                    </div>
                ) : statusQuery.isError ? (
                    <Alert type="error" showIcon title={t('common.messages.unknownError')} />
                ) : (
                    <Row gutter={[16, 16]}>
                        <Col xs={24} lg={14}>
                            <Typography.Title level={5} style={{ marginTop: 0, marginBottom: 12 }}>
                                {t('license.simpleUi.titleServer')}
                            </Typography.Title>
                            <Descriptions bordered column={1} size="small">
                                <Descriptions.Item label={t('license.simpleUi.status')}>
                                    <Tag color={getLicenseStatusTagColor(resolvedStatus?.kind ?? 'no_license')}>
                                        {getLicenseStatusLabel(resolvedStatus?.kind ?? 'no_license', t)}
                                    </Tag>
                                </Descriptions.Item>
                                <Descriptions.Item label={t('license.simpleUi.validUntil')}>
                                    {s?.expiryDate
                                        ? formatDate(s.expiryDate, formatLocale, {
                                              year: 'numeric',
                                              month: '2-digit',
                                              day: '2-digit',
                                              hour: '2-digit',
                                              minute: '2-digit',
                                          })
                                        : '—'}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('license.simpleUi.daysRemaining')}>
                                    {resolvedStatus ? getLicenseStatusDayText(resolvedStatus, t) ?? '—' : '—'}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('license.phase.capabilities.write')}>
                                    <Tag color={resolvedStatus?.canWrite ? 'green' : 'red'}>
                                        {t(resolvedStatus?.canWrite ? 'common.buttons.yes' : 'common.buttons.no')}
                                    </Tag>
                                </Descriptions.Item>
                                <Descriptions.Item label={t('license.phase.capabilities.access')}>
                                    <Tag color={resolvedStatus?.canAccess ? 'green' : 'red'}>
                                        {t(resolvedStatus?.canAccess ? 'common.buttons.yes' : 'common.buttons.no')}
                                    </Tag>
                                </Descriptions.Item>
                            </Descriptions>
                            {resolvedStatus ? (
                                <Alert
                                    style={{ marginTop: 12 }}
                                    type={
                                        resolvedStatus.kind === 'active'
                                            ? 'success'
                                            : resolvedStatus.kind === 'grace_write'
                                              ? 'warning'
                                              : 'error'
                                    }
                                    showIcon
                                    title={getLicenseStatusMessage(resolvedStatus, 'deployment', t)}
                                />
                            ) : null}
                            {machineHash ? (
                                <Collapse
                                    ghost
                                    style={{ marginTop: 12 }}
                                    items={[
                                        {
                                            key: 'tech',
                                            label: t('license.simpleUi.technicalPanel'),
                                            children: (
                                                <Typography.Paragraph
                                                    copyable={{ text: machineHash }}
                                                    style={{
                                                        marginBottom: 0,
                                                        wordBreak: 'break-all',
                                                        fontFamily: 'ui-monospace, monospace',
                                                    }}
                                                >
                                                    <Typography.Text type="secondary">
                                                        {t('license.simpleUi.machineFingerprint')}
                                                    </Typography.Text>
                                                    <br />
                                                    {machineHash}
                                                </Typography.Paragraph>
                                            ),
                                        },
                                    ]}
                                />
                            ) : null}
                        </Col>
                        {showDeploymentActivation ? (
                            <Col xs={24} lg={10}>
                                <Card type="inner" title={t('license.activation.title')}>
                                    {!canActivate ? (
                                        <Alert type="info" showIcon title={t('license.activation.noPermission')} />
                                    ) : (
                                        <Form
                                            form={form}
                                            layout="vertical"
                                            onFinish={(values) => {
                                                const licenseKey = values.licenseKey?.trim() ?? '';
                                                if (!licenseKey) {
                                                    message.warning(t('license.activation.licenseKey'));
                                                    return;
                                                }
                                                activateMutation.mutate({ licenseKey });
                                            }}
                                        >
                                            <Form.Item
                                                name="licenseKey"
                                                label={t('license.activation.licenseKey')}
                                                rules={[
                                                    { required: true, message: t('common.validation.fieldRequired') },
                                                ]}
                                            >
                                                <Input placeholder="REGK-XXXXX-XXXXX-XXXXX" autoComplete="off" />
                                            </Form.Item>
                                            <Form.Item>
                                                <Button
                                                    type="primary"
                                                    htmlType="submit"
                                                    loading={activateMutation.isPending}
                                                >
                                                    {t('license.activation.activate')}
                                                </Button>
                                            </Form.Item>
                                        </Form>
                                    )}
                                </Card>
                            </Col>
                        ) : null}
                    </Row>
                )}
            </Card>

            {canActivate ? (
                <Tabs
                    defaultActiveKey="issuance"
                    items={[
                        {
                            key: 'issuance',
                            label: t('license.tabs.issuance'),
                            children: (
                                <>
                                    <LicenseGenerationCard
                                        canGenerate={canActivate}
                                        machineFingerprint={machineHash}
                                        enabledLicenseFeatures={enabledPublicLicenseFeatures}
                                        prefill={licensePagePrefill}
                                    />
                                    {showIssuedLicenses ? <IssuedLicensesTableCard /> : null}
                                </>
                            ),
                        },
                        {
                            key: 'history',
                            label: t('license.tabs.activationHistory'),
                            children: <LicenseActivationHistoryCard />,
                        },
                        {
                            key: 'reports',
                            label: t('license.tabs.reports'),
                            children: <LicenseReportsCard enabledLicenseFeatures={enabledPublicLicenseFeatures} />,
                        },
                    ]}
                />
            ) : (
                <LicenseGenerationCard
                    canGenerate={canActivate}
                    machineFingerprint={machineHash}
                    enabledLicenseFeatures={enabledPublicLicenseFeatures}
                    prefill={licensePagePrefill}
                />
            )}
        </div>
    );
}
