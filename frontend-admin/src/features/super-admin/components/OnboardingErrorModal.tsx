'use client';

/**
 * Tenant create failure with optional subdomain suggestions (rollback confirmed server-side).
 */
import React, { useMemo } from 'react';
import { Alert, Button, List, Modal, Typography } from 'antd';
import { CloseCircleFilled } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';

import { useI18n } from '@/i18n';
import { getAdminTenantSlugSuggestions } from '@/features/super-admin/api/adminTenants';
import type { TenantOnboardingError } from '@/features/super-admin/lib/parseTenantOnboardingError';
import styles from '@/styles/tenant-form.module.css';

export type OnboardingErrorModalProps = {
    open: boolean;
    error: TenantOnboardingError | null;
    companyName?: string;
    attemptedSlug?: string;
    onTrySlug: (slug: string) => void;
    onDismiss: () => void;
    onCancel: () => void;
};

function formatErrorMessage(
    t: (key: string, values?: Record<string, string>) => string,
    error: TenantOnboardingError,
    attemptedSlug?: string,
): string {
    if (error.code === 'tenant_slug_taken' && attemptedSlug) {
        return t('tenants.onboarding.errors.slugTaken', { slug: attemptedSlug });
    }
    if (error.code === 'tenant_admin_email_taken') {
        return t('tenants.onboarding.errors.adminEmailTaken');
    }
    return error.message;
}

export function OnboardingErrorModal({
    open,
    error,
    companyName,
    attemptedSlug,
    onTrySlug,
    onDismiss,
    onCancel,
}: OnboardingErrorModalProps) {
    const { t } = useI18n();

    const suggestionsQuery = useQuery({
        queryKey: ['admin', 'tenants', 'slug-suggestions', companyName, attemptedSlug],
        queryFn: () => getAdminTenantSlugSuggestions(companyName, attemptedSlug),
        enabled: open && !!error && (error.slugSuggestions.length === 0 || error.code === 'tenant_slug_taken'),
        staleTime: 30_000,
    });

    const suggestions = useMemo(() => {
        if (error?.slugSuggestions.length) {
            return error.slugSuggestions;
        }
        return suggestionsQuery.data ?? [];
    }, [error, suggestionsQuery.data]);

    const displayMessage = error
        ? formatErrorMessage(t, error, attemptedSlug)
        : t('tenants.messages.saveFailed');

    return (
        <Modal
            title={
                <span>
                    <CloseCircleFilled className={styles.onboardingErrorIcon} aria-hidden />{' '}
                    {t('tenants.onboarding.errors.title')}
                </span>
            }
            open={open && !!error}
            onCancel={onCancel}
            width={560}
            destroyOnClose
            footer={[
                <Button key="cancel" onClick={onCancel}>
                    {t('common.buttons.cancel')}
                </Button>,
                <Button
                    key="retry"
                    type="primary"
                    onClick={() => {
                        if (suggestions[0]) {
                            onTrySlug(suggestions[0]);
                        } else {
                            onDismiss();
                        }
                    }}
                >
                    {t('tenants.onboarding.errors.tryAgain')}
                </Button>,
            ]}
        >
            {error ? (
                <>
                    <Alert type="error" showIcon={false} message={displayMessage} style={{ marginBottom: 16 }} />

                    {suggestions.length > 0 ? (
                        <div>
                            <Typography.Text strong>{t('tenants.onboarding.errors.suggestionsTitle')}</Typography.Text>
                            <List
                                size="small"
                                style={{ marginTop: 8 }}
                                dataSource={suggestions}
                                renderItem={(slug) => (
                                    <List.Item
                                        actions={[
                                            <Button
                                                key="use"
                                                type="link"
                                                size="small"
                                                onClick={() => onTrySlug(slug)}
                                            >
                                                {t('tenants.onboarding.errors.useSuggestion')}
                                            </Button>,
                                        ]}
                                    >
                                        <Typography.Text code>{slug}</Typography.Text>
                                    </List.Item>
                                )}
                            />
                        </div>
                    ) : null}

                    <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
                        {t('tenants.onboarding.errors.rollbackNote')}
                    </Typography.Paragraph>
                </>
            ) : null}
        </Modal>
    );
}
