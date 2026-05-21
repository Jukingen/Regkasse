'use client';

import type { MouseEvent } from 'react';
import Link from 'next/link';
import { Tag, Tooltip } from 'antd';

import { useI18n } from '@/i18n';

export type TenantNoAdminWarningPillProps = {
    tenantId: string;
    /** Stop dropdown row click from firing when the pill is clicked. */
    onClickCapture?: (event: MouseEvent) => void;
};

export function buildTenantUsersTabHref(tenantId: string): string {
    return `/admin/tenants/${tenantId}?tab=users`;
}

/** Yellow warning pill — links to tenant user management (users tab). */
export function TenantNoAdminWarningPill({ tenantId, onClickCapture }: TenantNoAdminWarningPillProps) {
    const { t } = useI18n();
    const href = buildTenantUsersTabHref(tenantId);

    return (
        <Tooltip title={t('adminShell.tenant.devSwitcher.noAdminPillTooltip')}>
            <Link
                href={href}
                onClick={(e) => e.stopPropagation()}
                onClickCapture={onClickCapture}
                style={{ display: 'inline-flex', lineHeight: 1 }}
                aria-label={t('adminShell.tenant.devSwitcher.noAdminPillTooltip')}
            >
                <Tag
                    style={{
                        marginInlineEnd: 0,
                        cursor: 'pointer',
                        background: '#fffbe6',
                        borderColor: '#ffe58f',
                        color: '#ad6800',
                        fontWeight: 600,
                    }}
                >
                    ⚠️ {t('adminShell.tenant.devSwitcher.noAdminPill')}
                </Tag>
            </Link>
        </Tooltip>
    );
}
