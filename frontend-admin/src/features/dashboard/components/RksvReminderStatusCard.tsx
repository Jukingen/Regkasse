'use client';

import React, { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { Alert, Button, Card, Descriptions, Select, Space, Tag, Typography } from 'antd';
import { WarningOutlined } from '@ant-design/icons';
import { useI18n } from '@/i18n/I18nProvider';
import {
    isJahresbelegActionRequired,
    isMonatsbelegActionRequired,
    isStartbelegMissing,
    useRksvReminderOverview,
    type RegisterReminderRow,
} from '@/features/dashboard/hooks/useRksvReminderOverview';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { usePermissions } from '@/hooks/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { RKSV_COMPLIANCE_PATH, RKSV_SONDERBELEGE_PATH } from '@/shared/auth/rksvRoutePaths';
import type { RksvReminderStatusDto } from '@/api/generated/model';

function registerLabel(row: RegisterReminderRow): string {
    const nr = row.register.registerNumber?.trim();
    const loc = row.register.location?.trim();
    if (loc && nr) return `${loc} (Nr. ${nr})`;
    if (nr) return `Nr. ${nr}`;
    return row.registerId.slice(0, 8);
}

function monatsbelegStatusTag(status: string | null | undefined, t: (key: string) => string) {
    switch (status) {
        case 'ok':
            return <Tag color="success">{t('dashboard.rksvReminder.monatsbeleg_ok')}</Tag>;
        case 'upcoming':
            return <Tag color="warning">{t('dashboard.rksvReminder.monatsbeleg_upcoming')}</Tag>;
        case 'overdue':
            return <Tag color="error">{t('dashboard.rksvReminder.monatsbeleg_overdue')}</Tag>;
        default:
            return <Tag>{status ?? '—'}</Tag>;
    }
}

function jahresbelegStatusTag(status: string | null | undefined, t: (key: string) => string) {
    switch (status) {
        case 'ok':
            return <Tag color="success">{t('dashboard.rksvReminder.jahresbeleg_ok')}</Tag>;
        case 'upcoming':
            return <Tag color="warning">{t('dashboard.rksvReminder.jahresbeleg_upcoming')}</Tag>;
        case 'overdue':
            return <Tag color="error">{t('dashboard.rksvReminder.jahresbeleg_overdue')}</Tag>;
        default:
            return <Tag>{status ?? '—'}</Tag>;
    }
}

function pickDefaultRegisterId(rows: RegisterReminderRow[]): string | undefined {
    const withIssue = rows.find(
        (r) =>
            !r.statusLoading &&
            !r.statusError &&
            (isStartbelegMissing(r.status) ||
                isJahresbelegActionRequired(r.status) ||
                r.status?.monatsbeleg?.status === 'overdue'),
    );
    return withIssue?.registerId ?? rows[0]?.registerId;
}

function sonderbelegeHref(registerId: string, kind?: 'startbeleg' | 'jahresbeleg' | 'monatsbeleg') {
    const params = new URLSearchParams({ registerId });
    if (kind) params.set('kind', kind);
    return `/rksv/sonderbelege?${params.toString()}`;
}

export type RksvReminderStatusCardProps = {
    enabled?: boolean;
};

export function RksvReminderStatusCard({ enabled = true }: RksvReminderStatusCardProps) {
    const { t } = useI18n();
    const { hasPermission } = usePermissions();
    const canOpenSonderbelege = useCanAccessPath(RKSV_SONDERBELEGE_PATH);
    const canOpenCompliance = useCanAccessPath(RKSV_COMPLIANCE_PATH);
    const canCreateStartbeleg = hasPermission(PERMISSIONS.RKSV_STARTBELEG_CREATE);
    const canCreateMonatsbeleg = hasPermission(PERMISSIONS.RKSV_MONATSBELEG_CREATE);
    const canCreateJahresbeleg = hasPermission(PERMISSIONS.RKSV_JAHRESBELEG_CREATE);
    const { rows, summary, registersLoading, statusPending, hasRegisters, loadError } = useRksvReminderOverview(enabled);
    const [selectedRegisterId, setSelectedRegisterId] = useState<string | undefined>(undefined);

    useEffect(() => {
        if (rows.length === 0) {
            setSelectedRegisterId(undefined);
            return;
        }
        if (selectedRegisterId && rows.some((r) => r.registerId === selectedRegisterId)) return;
        setSelectedRegisterId(pickDefaultRegisterId(rows));
    }, [rows, selectedRegisterId]);

    const selectedRow = useMemo(
        () => rows.find((r) => r.registerId === selectedRegisterId),
        [rows, selectedRegisterId],
    );

    const status: RksvReminderStatusDto | undefined = selectedRow?.status;
    const loading = registersLoading || statusPending;

    const multiRegisterSummary =
        rows.length > 1 &&
        (summary.startbelegMissingCount > 0 ||
            summary.jahresbelegAttentionCount > 0 ||
            summary.monatsbelegAttentionCount > 0);

    return (
        <Card
            title={t('dashboard.rksvReminder.card_title')}
            variant="borderless"
            loading={loading}
            style={{ marginBottom: 24 }}
        >
            <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
                {t('dashboard.rksvReminder.card_intro')}
            </Typography.Paragraph>

            {loadError ? (
                <Alert type="error" showIcon title={t('dashboard.rksvReminder.status_load_failed')} style={{ marginBottom: 12 }} />
            ) : null}

            {!loadError && !hasRegisters && !loading ? (
                <Typography.Text type="secondary">{t('dashboard.rksvReminder.no_registers')}</Typography.Text>
            ) : null}

            {multiRegisterSummary ? (
                <Alert
                    type="warning"
                    showIcon
                    icon={<WarningOutlined />}
                    style={{ marginBottom: 12 }}
                    title={t('dashboard.rksvReminder.multi_register_summary', {
                        startbeleg: summary.startbelegMissingCount,
                        jahresbeleg: summary.jahresbelegAttentionCount,
                        monatsbeleg: summary.monatsbelegAttentionCount,
                    })}
                />
            ) : null}

            {rows.length > 1 ? (
                <Space style={{ marginBottom: 16 }} wrap>
                    <Typography.Text strong>{t('dashboard.rksvReminder.register_label')}</Typography.Text>
                    <Select
                        style={{ minWidth: 280 }}
                        value={selectedRegisterId}
                        onChange={setSelectedRegisterId}
                        options={rows.map((r) => ({
                            value: r.registerId,
                            label: registerLabel(r),
                        }))}
                    />
                </Space>
            ) : null}

            {!loadError && selectedRow?.statusError ? (
                <Alert type="error" showIcon title={t('dashboard.rksvReminder.status_load_failed')} />
            ) : null}

            {selectedRow && !selectedRow.statusError && !loadError && status ? (
                <>
                    {isStartbelegMissing(status) ? (
                        <Alert
                            type="error"
                            showIcon
                            style={{ marginBottom: 12 }}
                            title={t('dashboard.rksvReminder.startbeleg_missing_warning')}
                            description={t('dashboard.rksvReminder.startbeleg_blocks_pos')}
                            action={
                                canOpenSonderbelege && canCreateStartbeleg ? (
                                    <Link href={sonderbelegeHref(selectedRow.registerId, 'startbeleg')}>
                                        <Button type="primary" size="small" danger>
                                            {t('dashboard.rksvReminder.create_now')}
                                        </Button>
                                    </Link>
                                ) : undefined
                            }
                        />
                    ) : null}

                    {isJahresbelegActionRequired(status) ? (
                        <Alert
                            type="warning"
                            showIcon
                            style={{ marginBottom: 12 }}
                            title={t('dashboard.rksvReminder.jahresbeleg_missing_warning')}
                            description={
                                status.jahresbeleg?.daysUntilDeadline != null
                                    ? t('dashboard.rksvReminder.jahresbeleg_days_until', {
                                          days: status.jahresbeleg.daysUntilDeadline,
                                      })
                                    : undefined
                            }
                            action={
                                canOpenSonderbelege && canCreateJahresbeleg ? (
                                    <Link href={sonderbelegeHref(selectedRow.registerId, 'jahresbeleg')}>
                                        <Button type="primary" size="small">
                                            {t('dashboard.rksvReminder.create_now')}
                                        </Button>
                                    </Link>
                                ) : undefined
                            }
                        />
                    ) : null}

                    {isMonatsbelegActionRequired(status) && status.monatsbeleg?.status !== 'ok' ? (
                        <Alert
                            type={status.monatsbeleg?.status === 'overdue' ? 'error' : 'warning'}
                            showIcon
                            style={{ marginBottom: 12 }}
                            title={
                                status.monatsbeleg?.warningMessageDe ??
                                t('dashboard.rksvReminder.monatsbeleg_default_warning')
                            }
                            action={
                                canOpenSonderbelege && canCreateMonatsbeleg ? (
                                    <Link href={sonderbelegeHref(selectedRow.registerId, 'monatsbeleg')}>
                                        <Button type="primary" size="small">
                                            {t('dashboard.rksvReminder.create_now')}
                                        </Button>
                                    </Link>
                                ) : undefined
                            }
                        />
                    ) : null}

                    <Descriptions column={1} size="small" bordered>
                        <Descriptions.Item label={t('dashboard.rksvReminder.row_startbeleg')}>
                            {isStartbelegMissing(status) ? (
                                <Tag color="error">{t('dashboard.rksvReminder.startbeleg_missing')}</Tag>
                            ) : (
                                <Tag color="success">{t('dashboard.rksvReminder.startbeleg_present')}</Tag>
                            )}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('dashboard.rksvReminder.row_monatsbeleg')}>
                            <Space wrap>
                                {monatsbelegStatusTag(status.monatsbeleg?.status, t)}
                                {status.monatsbeleg?.daysUntilDeadline != null ? (
                                    <Typography.Text type="secondary">
                                        {t('dashboard.rksvReminder.days_until_month_end', {
                                            days: status.monatsbeleg.daysUntilDeadline,
                                        })}
                                    </Typography.Text>
                                ) : null}
                            </Space>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('dashboard.rksvReminder.row_jahresbeleg')}>
                            <Space wrap>
                                {jahresbelegStatusTag(status.jahresbeleg?.status, t)}
                                {status.jahresbeleg?.isRequired ? (
                                    <Typography.Text type="secondary">
                                        {t('dashboard.rksvReminder.jahresbeleg_required')}
                                    </Typography.Text>
                                ) : (
                                    <Typography.Text type="secondary">
                                        {t('dashboard.rksvReminder.jahresbeleg_not_required')}
                                    </Typography.Text>
                                )}
                            </Space>
                        </Descriptions.Item>
                    </Descriptions>

                    <div style={{ marginTop: 12 }}>
                        {canOpenSonderbelege ? (
                            <Link href={sonderbelegeHref(selectedRow.registerId)}>
                                <Button type="link" style={{ paddingLeft: 0 }}>
                                    {t('dashboard.rksvReminder.open_sonderbelege')}
                                </Button>
                            </Link>
                        ) : null}
                        {canOpenSonderbelege && canOpenCompliance ? <span> · </span> : null}
                        {canOpenCompliance ? (
                            <Link href={RKSV_COMPLIANCE_PATH}>{t('dashboard.rksvReminder.open_compliance')}</Link>
                        ) : null}
                    </div>
                </>
            ) : null}
        </Card>
    );
}
