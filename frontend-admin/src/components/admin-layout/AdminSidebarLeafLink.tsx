'use client';

import React, { useCallback } from 'react';
import Link from 'next/link';
import { useQueryClient } from '@tanstack/react-query';
import { prefetchAdminRoute } from '@/lib/adminRoutePrefetch';

type AdminSidebarLeafLinkProps = {
    href: string;
    children: React.ReactNode;
};

/**
 * Sidebar leaf link with ellipsis overflow; pair with Menu `title` for collapsed tooltips.
 */
export function AdminSidebarLeafLink({ href, children }: AdminSidebarLeafLinkProps) {
    const queryClient = useQueryClient();
    const warmRoute = useCallback(() => {
        prefetchAdminRoute(queryClient, href);
    }, [queryClient, href]);

    return (
        <Link
            href={href}
            className="admin-sidebar-leaf-link"
            prefetch={false}
            onMouseEnter={warmRoute}
            onFocus={warmRoute}
        >
            {typeof children === 'string' || typeof children === 'number' ? (
                <span className="admin-sidebar-leaf-link__text">{children}</span>
            ) : (
                children
            )}
        </Link>
    );
}
