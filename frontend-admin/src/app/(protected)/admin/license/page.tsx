'use client';

/**
 * On-premise license status, activation, issuance, and issued-license audit list (German operator copy via i18n `license.*`).
 */

import React, { useEffect, useMemo, useState } from 'react';
import axios from 'axios';
import {
    Alert,
    Button,
    Card,
    Col,
    Descriptions,
    Form,
    Input,
    Modal,
    Row,
    Space,
    Spin,
    Table,
    Tag,
    Typography,
    message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { CopyOutlined, ReloadOutlined, StopOutlined, SyncOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { useI18n, formatDate } from '@/i18n';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useDebounce } from '@/hooks/useDebounce';
import { PERMISSIONS } from '@/shared/auth/permissions';
import {
    deleteRevokeIssuedLicense,
    getIssuedLicensesList,
    getLicenseStatus,
    getPublicLicenseStatus,
    licenseQueryKeys,
    postActivateLicense,
    type ActivateLicenseRequest,
    type IssuedLicenseListItemDto,
} from '@/api/manual/adminLicense';
import { LicenseGenerationCard } from './LicenseGenerationCard';
import { IssuedLicenseUpgradeModal } from './IssuedLicenseUpgradeModal';

const FloatingHintStorageKey = 'regkasse.license.showFloatingHint';

function getSessionStorageItem(key: string): string | null {
    if (typeof globalThis === 'undefined') {
        return null;
    }
    try {
        return globalThis.sessionStorage?.getItem(key) ?? null;
    } catch {
        return null;
    }
}

function setSessionStorageItem(key: string, value: string): void {
    try {
        globalThis.sessionStorage?.setItem(key, value);
    } catch {
        /* private mode */
    }
}

function removeSessionStorageItem(key: string): void {
    try {
        globalThis.sessionStorage?.removeItem(key);
    } catch {
        /* private mode */
    }
}

type LicenseFormValues = {
    licenseKey: string;
    offlineActivationJwt?: string;
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
    const queryClient = useQueryClient();
    const [searchInput, setSearchInput] = useState('');
    const debouncedSearch = useDebounce(searchInput, 400);
    const [pageNumber, setPageNumber] = useState(1);
    const [pageSize, setPageSize] = useState(50);
    const [revokeTarget, setRevokeTarget] = useState<IssuedLicenseListItemDto | null>(null);
    const [upgradeModalRow, setUpgradeModalRow] = useState<IssuedLicenseListItemDto | null>(null);

    useEffect(() => {
        setPageNumber(1);
    }, [debouncedSearch]);

    function isRenewable(row: IssuedLicenseListItemDto): boolean {
        return !row.isRevoked && Boolean(!row.supersededByLicenseId);
    }

    const listParams = useMemo(
        () => ({
            search: debouncedSearch.trim() || undefined,
            pageNumber,
            pageSize,
        }),
        [debouncedSearch, pageNumber, pageSize],
    );

    const listQuery = useQuery({
        queryKey: licenseQueryKeys.list(listParams),
        queryFn: () => getIssuedLicensesList(listParams),
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
                    if (row.isRevoked) return <Tag color="red">{t('license.issued.columns.statusRevoked')}</Tag>;
                    if (row.supersededByLicenseId)
                        return <Tag color="orange">{t('license.issued.columns.statusSuperseded')}</Tag>;
                    return <Tag color="green">{t('license.issued.columns.statusActive')}</Tag>;
                },
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
                width: 200,
                fixed: 'right',
                render: (_, row) =>
                    isRenewable(row) ? (
                        <Space size="small" wrap>
                            <Button
                                type="primary"
                                size="small"
                                icon={<SyncOutlined />}
                                onClick={() => setUpgradeModalRow(row)}
                            >
                                {t('license.issued.renewUpgrade')}
                            </Button>
                            <Button
                                danger
                                size="small"
                                icon={<StopOutlined />}
                                onClick={() => setRevokeTarget(row)}
                            >
                                {t('license.issued.revoke')}
                            </Button>
                        </Space>
                    ) : (
                        <span style={{ color: 'var(--ant-color-text-secondary)' }}>—</span>
                    ),
            },
        ],
        [formatLocale, t],
    );

    return (
        <Card title={t('license.issued.title')}>
            <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <Input.Search
                    allowClear
                    placeholder={t('license.issued.searchPlaceholder')}
                    value={searchInput}
                    onChange={(e) => setSearchInput(e.target.value)}
                    style={{ maxWidth: 360 }}
                />

                {listQuery.isError ? (
                    <Alert type="error" showIcon message={t('license.issued.loadError')} />
                ) : (
                    <Table<IssuedLicenseListItemDto>
                        rowKey="id"
                        size="small"
                        loading={listQuery.isFetching}
                        columns={columns}
                        dataSource={listQuery.data?.items ?? []}
                        locale={{ emptyText: t('license.issued.empty') }}
                        scroll={{ x: 1040 }}
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

            <IssuedLicenseUpgradeModal
                row={upgradeModalRow}
                onClose={() => setUpgradeModalRow(null)}
            />
        </Card>
    );
}

export default function AdminLicensePage() {
    const { t, formatLocale } = useI18n();
    const queryClient = useQueryClient();
    const { user } = useAuth();
    const [form] = Form.useForm<LicenseFormValues>();
    const [showFloatingHint, setShowFloatingHint] = useState(false);

    const canActivate = Boolean(user?.permissions?.includes(PERMISSIONS.SETTINGS_MANAGE));

    useEffect(() => {
        if (getSessionStorageItem(FloatingHintStorageKey) === '1') {
            setShowFloatingHint(true);
        }
    }, []);

    const statusQuery = useQuery({
        queryKey: licenseQueryKeys.status,
        queryFn: () => getLicenseStatus(),
    });

    const publicStatusQuery = useQuery({
        queryKey: licenseQueryKeys.publicStatus,
        queryFn: () => getPublicLicenseStatus(),
    });

    const activateMutation = useMutation({
        mutationFn: (body: ActivateLicenseRequest) => postActivateLicense(body),
        onSuccess: (res, variables) => {
            if (!res.success) {
                message.error(res.message || t('license.activation.failed'));
                return;
            }
            if (res.validUntil) {
                message.success(
                    t('license.activation.successWithValidUntil', {
                        date: formatDate(res.validUntil, formatLocale, {
                            year: 'numeric',
                            month: '2-digit',
                            day: '2-digit',
                            hour: '2-digit',
                            minute: '2-digit',
                        }),
                    }),
                );
            } else {
                message.success(t('license.activation.success'));
            }
            const jwt = variables.offlineActivationJwt?.trim() ?? '';
            if (!jwt) {
                setSessionStorageItem(FloatingHintStorageKey, '1');
                setShowFloatingHint(true);
            } else {
                removeSessionStorageItem(FloatingHintStorageKey);
                setShowFloatingHint(false);
            }
            form.resetFields(['licenseKey', 'offlineActivationJwt']);
            void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.status });
            void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.publicStatus });
        },
        onError: (err: unknown) => {
            if (axios.isAxiosError(err)) {
                const data = err.response?.data as { message?: string } | undefined;
                message.error(data?.message || t('license.activation.failed'));
                return;
            }
            message.error(t('license.activation.failed'));
        },
    });

    const s = statusQuery.data;

    const statusPresentation = useMemo(() => {
        if (!s) return { tag: null as React.ReactNode };
        if (s.isValid) {
            return { tag: <Tag color="green">{t('license.status.valid')}</Tag> };
        }
        if (s.isTrial) {
            return { tag: <Tag color="orange">{t('license.status.trial')}</Tag> };
        }
        return { tag: <Tag color="red">{t('license.status.expired')}</Tag> };
    }, [s, t]);

    const machineHash = s?.machineHash?.trim() ?? '';

    const dismissFloatingHint = () => {
        removeSessionStorageItem(FloatingHintStorageKey);
        setShowFloatingHint(false);
    };

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
                            void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.status });
                            void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.publicStatus });
                            if (canActivate) {
                                void queryClient.invalidateQueries({ queryKey: licenseQueryKeys.listRoot });
                            }
                        }}
                    >
                        {t('common.buttons.refresh')}
                    </Button>
                }
            />

            {showFloatingHint ? (
                <Alert
                    type="warning"
                    showIcon
                    message={t('license.floatingWarning')}
                    closable
                    onClose={dismissFloatingHint}
                />
            ) : null}

            <Card title={t('license.publicStatus.title')}>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                    {t('license.publicStatus.subtitle')}
                </Typography.Paragraph>
                {publicStatusQuery.isLoading ? (
                    <div style={{ display: 'flex', justifyContent: 'center', padding: 24 }}>
                        <Spin />
                    </div>
                ) : publicStatusQuery.isError ? (
                    <Alert type="warning" showIcon message={t('license.publicStatus.loadError')} />
                ) : publicStatusQuery.data ? (
                    <Descriptions bordered column={1} size="small">
                        <Descriptions.Item label={t('license.publicStatus.licenseType')}>
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
                        <Descriptions.Item label={t('license.publicStatus.mode')}>
                            <Tag>{publicStatusQuery.data.mode ?? '—'}</Tag>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('license.publicStatus.isValid')}>
                            {publicStatusQuery.data.isValid ? (
                                <Tag color="green">{t('common.buttons.yes')}</Tag>
                            ) : (
                                <Tag color="red">{t('common.buttons.no')}</Tag>
                            )}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('license.publicStatus.daysRemaining')}>
                            {publicStatusQuery.data.daysRemaining}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('license.publicStatus.validUntil')}>
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
                        <Descriptions.Item label={t('license.publicStatus.features')}>
                            <Space size={[4, 4]} wrap>
                                {(publicStatusQuery.data.features ?? []).map((f) => (
                                    <Tag key={f}>{f}</Tag>
                                ))}
                            </Space>
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
                    <Alert type="error" showIcon message={t('common.messages.unknownError')} />
                ) : (
                    <Row gutter={[16, 16]}>
                        <Col xs={24} lg={14}>
                            <Descriptions bordered column={1} size="small">
                                <Descriptions.Item label={t('license.status.label')}>
                                    {statusPresentation.tag}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('license.trialDays')}>
                                    {s?.isTrial ? s.daysRemaining : '—'}
                                </Descriptions.Item>
                                <Descriptions.Item label={t('license.machineHash')}>
                                    {machineHash ? (
                                        <Typography.Paragraph
                                            copyable={{ text: machineHash }}
                                            style={{
                                                marginBottom: 0,
                                                wordBreak: 'break-all',
                                                fontFamily: 'ui-monospace, monospace',
                                            }}
                                        >
                                            {machineHash}
                                        </Typography.Paragraph>
                                    ) : (
                                        '—'
                                    )}
                                </Descriptions.Item>
                            </Descriptions>
                        </Col>
                        <Col xs={24} lg={10}>
                            <Card type="inner" title={t('license.activation.title')}>
                                {!canActivate ? (
                                    <Alert type="info" showIcon message={t('license.activation.noPermission')} />
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
                                            activateMutation.mutate({
                                                licenseKey,
                                                offlineActivationJwt: values.offlineActivationJwt?.trim() || null,
                                            });
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
                                        <Form.Item
                                            name="offlineActivationJwt"
                                            label={t('license.activation.offlineJwt')}
                                            extra={t('license.activation.offlineJwtHelp')}
                                        >
                                            <Input.TextArea
                                                rows={4}
                                                placeholder="eyJhbGciOiJSUzI1NiIs..."
                                                autoComplete="off"
                                            />
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
                    </Row>
                )}
            </Card>

            <LicenseGenerationCard canGenerate={canActivate} machineFingerprint={machineHash} />

            {canActivate ? <IssuedLicensesTableCard /> : null}
        </div>
    );
}
