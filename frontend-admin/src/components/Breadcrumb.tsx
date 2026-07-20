'use client';

import type { CSSProperties } from 'react';
import { Breadcrumb as AntBreadcrumb } from 'antd';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useI18n } from '@/i18n';
import type { AdminBreadcrumbItem } from '@/shared/adminShellLabels';
import { buildPathBreadcrumbs } from '@/shared/buildPathBreadcrumbs';

export type BreadcrumbProps = {
    /**
     * Explicit crumbs (preferred for nested pages with custom titles).
     * When omitted, items are derived from the current pathname + nav catalog.
     */
    items?: AdminBreadcrumbItem[];
    className?: string;
    style?: CSSProperties;
};

/**
 * FA breadcrumb trail. Auto-builds from the URL when `items` are not provided.
 * Home is Übersicht (`/dashboard`) — not `/` and not an emoji.
 */
export function Breadcrumb({ items, className, style }: BreadcrumbProps) {
    const pathname = usePathname();
    const { t } = useI18n();

    const crumbs = items ?? buildPathBreadcrumbs(pathname ?? '/', t);

    return (
        <nav aria-label={t('common.aria.breadcrumbNav')}>
            <AntBreadcrumb
                className={className}
                style={{ marginBottom: 16, ...style }}
                items={crumbs.map((crumb) => ({
                    title: crumb.href ? <Link href={crumb.href}>{crumb.title}</Link> : crumb.title,
                }))}
            />
        </nav>
    );
}
