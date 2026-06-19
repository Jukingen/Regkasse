'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Upgrade / supersede flow: POST /api/admin/license/upgrade with row id + new expiry date, then surface key + JWT.
 */

import React, { useEffect, useMemo, useState } from 'react';
import axios from 'axios';
import { Modal, Alert, Button, DatePicker, Descriptions, Input, Space, Typography } from 'antd';
import { CopyOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import utc from 'dayjs/plugin/utc';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useI18n, formatDate } from '@/i18n';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';
import {
    licenseQueryKeys,
    postUpgradeIssuedLicense,
    type GenerateLicenseResponse,
    type IssuedLicenseListItemDto,
} from '@/api/manual/adminLicense';

dayjs.extend(utc);

/** Minimum YYYY-MM-DD (UTC calendar) selectable: strictly after the stored expiry UTC day, and not before today UTC day. */
function minSelectableUpgradeYmd(expiryAtUtcIso: string): string {
    const afterExpiryUtc = dayjs.utc(expiryAtUtcIso).startOf('day').add(1, 'day');
    const todayUtc = dayjs.utc().startOf('day');
    const minUtc = afterExpiryUtc.isAfter(todayUtc, 'day') ? afterExpiryUtc : todayUtc;
    return minUtc.format('YYYY-MM-DD');
}

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

export type IssuedLicenseUpgradeModalProps = {
    row: IssuedLicenseListItemDto | null;
    onClose: () => void;
};

export function IssuedLicenseUpgradeModal({ row, onClose }: IssuedLicenseUpgradeModalProps) {
  const { message } = useAntdApp();

    const open = row !== null;
    const { t, formatLocale } = useI18n();
    const queryClient = useQueryClient();

    const [expiryPick, setExpiryPick] = useState<Dayjs | null>(null);
    const [reason, setReason] = useState('');
    const [outcome, setOutcome] = useState<GenerateLicenseResponse | null>(null);

    const minYmd = useMemo(() => (row ? minSelectableUpgradeYmd(row.expiryAtUtc) : ''), [row]);

    const minSelectableStart = useMemo(
        () => (minYmd ? dayjs(minYmd, 'YYYY-MM-DD').startOf('day') : null),
        [minYmd],
    );

    useEffect(() => {
        if (!row) {
            setExpiryPick(null);
            setReason('');
            return;
        }
        setReason('');
        setExpiryPick(dayjs(minSelectableUpgradeYmd(row.expiryAtUtc), 'YYYY-MM-DD'));
    }, [row]);

    const mutation = useMutation({
        mutationFn: (p: { issuedLicenseId: string; newExpiryDate: string; reason?: string | null }) =>
            postUpgradeIssuedLicense({
                issuedLicenseId: p.issuedLicenseId,
                newExpiryDate: p.newExpiryDate,
                reason: p.reason?.trim() || null,
            }),
        onSuccess: async (res) => {
            const jwt = res.licenseJwt ?? res.signedJwt;
            if (!res.success || !res.licenseKey || !jwt) {
                message.error(res.message || t('license.issued.upgrade.failed'));
                return;
            }
            message.success(t('license.issued.upgrade.success'));
            setOutcome(res);
            onClose();
            await queryClient.invalidateQueries({ queryKey: licenseQueryKeys.listRoot });
            await queryClient.invalidateQueries({ queryKey: licenseQueryKeys.status });
            await queryClient.invalidateQueries({ queryKey: licenseQueryKeys.publicStatus });
        },
        onError: (err: unknown) => {
            if (axios.isAxiosError(err)) {
                const status = err.response?.status;
                const data = err.response?.data as { message?: string } | GenerateLicenseResponse | undefined;
                const msg =
                    typeof data?.message === 'string'
                        ? data.message
                        : (data as GenerateLicenseResponse | undefined)?.message;
                if (status === 503) {
                    message.error(msg || t('license.generation.unavailable'));
                    return;
                }
                message.error(msg || t('license.issued.upgrade.failed'));
                return;
            }
            message.error(t('license.issued.upgrade.failed'));
        },
    });

    const disabledDate = (current: Dayjs) => {
        if (!minSelectableStart) return true;
        return current.clone().startOf('day').isBefore(minSelectableStart, 'day');
    };

    const handleFinish = () => {
        if (!row || !expiryPick) {
            message.warning(t('common.validation.fieldRequired'));
            return;
        }
        const ymd = expiryPick.format('YYYY-MM-DD');
        if (ymd < minYmd) {
            message.warning(t('license.issued.upgrade.pickAfterExpiry'));
            return;
        }
        mutation.mutate({
            issuedLicenseId: row.id,
            newExpiryDate: ymd,
            reason: reason || null,
        });
    };

    return (
        <>
            <Modal
                title={t('license.issued.upgrade.modalTitle')}
                open={open}
                okText={t('license.issued.upgrade.submit')}
                confirmLoading={mutation.isPending}
                onCancel={() => {
                    if (!mutation.isPending) {
                        onClose();
                    }
                }}
                destroyOnHidden
                onOk={handleFinish}
            >
                {row ? (
                    <>
                        <Typography.Paragraph type="secondary">{t('license.issued.upgrade.customerHint', { customer: row.customerName })}</Typography.Paragraph>
                        <Descriptions bordered column={1} size="small" style={{ marginBottom: 12 }}>
                            <Descriptions.Item label={t('license.issued.upgrade.currentExpiry')}>
                                {formatDate(row.expiryAtUtc, formatLocale, {
                                    year: 'numeric',
                                    month: '2-digit',
                                    day: '2-digit',
                                })}
                            </Descriptions.Item>
                        </Descriptions>
                        <Typography.Text>{t('license.issued.upgrade.newExpiryLabel')}</Typography.Text>
                        <DatePicker
                            style={{ width: '100%', marginTop: 6 }}
                            format={DAYJS_DATE_FORMAT}
                            value={expiryPick}
                            onChange={(d) => setExpiryPick(d)}
                            disabledDate={disabledDate}
                        />
                        <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 8 }}>
                            {t('license.issued.upgrade.newExpiryHelp')}
                        </Typography.Paragraph>
                        <Typography.Text>{t('license.issued.upgrade.reasonLabel')}</Typography.Text>
                        <Input.TextArea
                            style={{ marginTop: 6 }}
                            rows={2}
                            value={reason}
                            onChange={(e) => setReason(e.target.value)}
                            placeholder={t('license.issued.upgrade.reasonPlaceholder')}
                            maxLength={512}
                            autoComplete="off"
                        />
                    </>
                ) : null}
            </Modal>

            <Modal
                title={t('license.issued.upgrade.resultTitle')}
                open={outcome !== null && outcome.success}
                footer={[
                    <Button key="close" type="primary" onClick={() => setOutcome(null)}>
                        {t('common.buttons.close')}
                    </Button>,
                ]}
                onCancel={() => setOutcome(null)}
                width={680}
                destroyOnHidden
            >
                {outcome?.licenseKey && outcome.signedJwt ? (
                    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                        <Alert type="info" showIcon title={t('license.issued.upgrade.reActivateNotice')} />
                        <Alert type="warning" showIcon title={t('license.issued.upgrade.resultWarning')} />
                        <Descriptions bordered column={1} size="small">
                            <Descriptions.Item label={t('license.generation.result.licenseKey')}>
                                <Space.Compact style={{ width: '100%' }}>
                                    <Input
                                        readOnly
                                        value={outcome.licenseKey}
                                        style={{ fontFamily: 'ui-monospace, monospace' }}
                                    />
                                    <Button
                                        icon={<CopyOutlined />}
                                        onClick={async () => {
                                            const ok = await copyTextToClipboard(outcome.licenseKey!);
                                            message[ok ? 'success' : 'error'](
                                                ok ? t('license.generation.result.copied') : t('license.issued.copyFailed'),
                                            );
                                        }}
                                    >
                                        {t('license.issued.copy')}
                                    </Button>
                                </Space.Compact>
                            </Descriptions.Item>
                            <Descriptions.Item label={t('license.generation.result.signedJwt')}>
                                <Space.Compact style={{ width: '100%' }}>
                                    <Input.TextArea
                                        readOnly
                                        autoSize={{ minRows: 2, maxRows: 6 }}
                                        value={outcome.signedJwt}
                                        style={{
                                            fontFamily: 'ui-monospace, monospace',
                                            wordBreak: 'break-all',
                                        }}
                                    />
                                    <Button
                                        icon={<CopyOutlined />}
                                        onClick={async () => {
                                            const ok = await copyTextToClipboard(outcome.signedJwt!);
                                            message[ok ? 'success' : 'error'](
                                                ok ? t('license.generation.result.copied') : t('license.issued.copyFailed'),
                                            );
                                        }}
                                    >
                                        {t('license.issued.copy')}
                                    </Button>
                                </Space.Compact>
                            </Descriptions.Item>
                            <Descriptions.Item label={t('license.generation.result.expiry')}>
                                {outcome.expiryAtUtc
                                    ? formatDate(outcome.expiryAtUtc, formatLocale, {
                                          year: 'numeric',
                                          month: '2-digit',
                                          day: '2-digit',
                                      })
                                    : '—'}
                            </Descriptions.Item>
                        </Descriptions>
                    </Space>
                ) : null}
            </Modal>
        </>
    );
}
