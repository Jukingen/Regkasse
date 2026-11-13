'use client';

import Link from 'next/link';
import { useQueryClient } from '@tanstack/react-query';
import React, { useCallback } from 'react';

import { prefetchAdminRoute } from '@/lib/adminRoutePrefetch';

type AdminSidebarLeafLinkProps = {
  href: string;
  children: React.ReactNode;
  /** Ctrl/Cmd+Click opens menu permission info instead of navigating. */
  onModifierClick?: (event: React.MouseEvent) => void;
};

/**
 * Sidebar leaf link with ellipsis overflow.
 * Menu `title` stays the plain item name (collapsed icon discoverability only — not contextual help).
 * Ctrl/Cmd+Click opens menu permission info. Contextual help uses visible subtitles.
 */
export function AdminSidebarLeafLink({
  href,
  children,
  onModifierClick,
}: AdminSidebarLeafLinkProps) {
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
      onClick={(e) => {
        if ((e.ctrlKey || e.metaKey) && onModifierClick) {
          e.preventDefault();
          e.stopPropagation();
          onModifierClick(e);
        }
      }}
    >
      {typeof children === 'string' || typeof children === 'number' ? (
        <span className="admin-sidebar-leaf-link__text">{children}</span>
      ) : (
        children
      )}
    </Link>
  );
}
