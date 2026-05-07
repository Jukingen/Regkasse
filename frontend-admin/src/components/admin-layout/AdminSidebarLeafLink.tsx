'use client';

import React from 'react';
import Link from 'next/link';

type AdminSidebarLeafLinkProps = {
    href: string;
    children: React.ReactNode;
};

/**
 * Sidebar leaf link with ellipsis overflow; pair with Menu `title` for collapsed tooltips.
 */
export function AdminSidebarLeafLink({ href, children }: AdminSidebarLeafLinkProps) {
    return (
        <Link href={href} className="admin-sidebar-leaf-link" prefetch={false}>
            {typeof children === 'string' || typeof children === 'number' ? (
                <span className="admin-sidebar-leaf-link__text">{children}</span>
            ) : (
                children
            )}
        </Link>
    );
}
