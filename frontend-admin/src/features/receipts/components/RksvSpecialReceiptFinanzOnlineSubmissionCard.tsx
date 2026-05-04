'use client';

/**
 * FinanzOnline/BMF submission snapshot for RKSV Startbeleg and Jahresbeleg (admin read model).
 */

import React, { useCallback } from 'react';
import { Alert, Button, Card, Descriptions, Space, Tag, Typography, message } from 'antd';
import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { RksvFinanzOnlineSubmissionStatusDto } from '@/api/generated/model';
import { postApiAdminFinanzonlineReconciliationRetryPaymentId } from '@/api/generated/admin/admin';
import { useI18n } from '@/i18n';
import { RECEIPT_KEYS } from '@/features/receipts/hooks/useReceiptListQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';
import {
    isRksvFinanzOnlineTrackedSpecialReceiptKind,
    rksvFinanzOnlineSubmissionStatusTagColor,
    shouldOfferFinanzOnlineReconciliationRetry,
} from '@/features/receipts/utils/rksvFinanzOnlineSubmissionUi';

dayjs.extend(utc);

const { Text } = Typography;

function formatUtc(iso: string | null | undefined): string {
    if (!iso) return '—';
    const d = dayjs.utc(iso);
    return d.isValid() ? `${d.format('DD.MM.YYYY HH:mm:ss')} UTC` : '—';
}

function statusTranslationKey(status: string | undefined): string {
    const s = (status ?? '').trim();
    const map: Record<string, string> = {
        Pending: 'receipts.detail.finanzOnlineSubmission.status.pending',
        Submitted: 'receipts.detail.finanzOnlineSubmission.status.submitted',
        Verified: 'receipts.detail.finanzOnlineSubmission.status.verified',
        Failed: 'receipts.detail.finanzOnlineSubmission.status.failed',
        ManualVerificationRequired: 'receipts.detail.finanzOnlineSubmission.status.manualVerificationRequired',
        NotRequired: 'receipts.detail.finanzOnlineSubmission.status.notRequired',
    };
    return map[s] ?? 'receipts.detail.finanzOnlineSubmission.status.unknown';
}

function formatStatusLabel(
    translate: (key: string, vars?: Record<string, string>) => string,
    st: string | undefined,
): string {
    if (!st?.trim()) return translate('receipts.detail.finanzOnlineSubmission.valueStatusMissing');
    const key = statusTranslationKey(st);
    if (key === 'receipts.detail.finanzOnlineSubmission.status.unknown') {
        return translate(key, { raw: st });
    }
    return translate(key);
}

export interface RksvSpecialReceiptFinanzOnlineSubmissionCardProps {
    receiptId: string;
    paymentId: string | null | undefined;
    rksvSpecialReceiptKind: string | null | undefined;
    submission: RksvFinanzOnlineSubmissionStatusDto | null | undefined;
}

export default function RksvSpecialReceiptFinanzOnlineSubmissionCard({
    receiptId,
    paymentId,
    rksvSpecialReceiptKind,
    submission,
}: RksvSpecialReceiptFinanzOnlineSubmissionCardProps) {
    const { t } = useI18n();
    const { hasPermission } = usePermissions();
    const queryClient = useQueryClient();
    const p = (key: string) => t(`receipts.detail.finanzOnlineSubmission.${key}`);

    const canSubmitFo = hasPermission(PERMISSIONS.FINANZONLINE_SUBMIT);
    const pid = (paymentId ?? '').trim();

    const retryMutation = useMutation({
        mutationFn: async () => {
            if (!pid) throw new Error('missing_payment');
            return postApiAdminFinanzonlineReconciliationRetryPaymentId(pid);
        },
        onSuccess: async () => {
            message.success(p('retrySuccess'));
            await queryClient.invalidateQueries({ queryKey: RECEIPT_KEYS.detail(receiptId) });
        },
        onError: () => {
            message.error(p('retryFailed'));
        },
    });

    const onRetry = useCallback(() => {
        void retryMutation.mutateAsync();
    }, [retryMutation]);

    if (!isRksvFinanzOnlineTrackedSpecialReceiptKind(rksvSpecialReceiptKind)) {
        return null;
    }

    const st = submission?.status?.trim();
    const statusLabel = formatStatusLabel(t, st);
    const color = rksvFinanzOnlineSubmissionStatusTagColor(st);
    const showRetry =
        canSubmitFo &&
        Boolean(pid) &&
        shouldOfferFinanzOnlineReconciliationRetry(st ?? null);

    const errParts = [submission?.lastErrorCode?.trim(), submission?.lastErrorMessage?.trim()].filter(Boolean);
    const errLine = errParts.length ? errParts.join(' — ') : '—';

    return (
        <Card title={p('cardTitle')}>
            <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <Alert type="info" showIcon message={p('disclaimerTitle')} description={p('disclaimerBody')} />
                <Descriptions bordered size="small" column={1}>
                    <Descriptions.Item label={p('labelStatus')}>
                        <Text>
                            <Tag color={color}>{statusLabel}</Tag>
                        </Text>
                    </Descriptions.Item>
                    <Descriptions.Item label={p('labelLastAttempt')}>
                        {formatUtc(submission?.lastAttemptAtUtc)}
                    </Descriptions.Item>
                    <Descriptions.Item label={p('labelSubmittedAt')}>
                        {formatUtc(submission?.submittedAtUtc)}
                    </Descriptions.Item>
                    <Descriptions.Item label={p('labelVerifiedAt')}>
                        {formatUtc(submission?.verifiedAtUtc)}
                    </Descriptions.Item>
                    <Descriptions.Item label={p('labelAttemptCount')}>
                        {submission?.attemptCount != null ? String(submission.attemptCount) : '—'}
                    </Descriptions.Item>
                    <Descriptions.Item label={p('labelLastError')}>
                        <Text type={errLine !== '—' ? 'danger' : 'secondary'}>{errLine}</Text>
                    </Descriptions.Item>
                    <Descriptions.Item label={p('labelExternalReference')}>
                        {submission?.externalReference?.trim() ? (
                            <Text code copyable>
                                {submission.externalReference.trim()}
                            </Text>
                        ) : (
                            '—'
                        )}
                    </Descriptions.Item>
                </Descriptions>
                {showRetry ? (
                    <Button type="primary" loading={retryMutation.isPending} onClick={onRetry}>
                        {p('retryButton')}
                    </Button>
                ) : null}
            </Space>
        </Card>
    );
}
