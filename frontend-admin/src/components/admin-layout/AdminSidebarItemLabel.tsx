'use client';

import React from 'react';

type AdminSidebarItemLabelProps = {
  title: React.ReactNode;
  subtitle?: string;
};

/**
 * Sidebar menu title + optional muted subtitle below (no hover tooltip).
 * Screen readers get a single combined name when subtitle is present.
 */
export function AdminSidebarItemLabel({ title, subtitle }: AdminSidebarItemLabelProps) {
  const titleText = typeof title === 'string' || typeof title === 'number' ? String(title) : undefined;
  const accessibleName =
    titleText && subtitle ? `${titleText}. ${subtitle}` : titleText || undefined;

  return (
    <span
      className="admin-sidebar-item-label"
      {...(accessibleName ? { 'aria-label': accessibleName } : {})}
    >
      <span
        className="admin-sidebar-item-title"
        aria-hidden={accessibleName ? true : undefined}
      >
        {title}
      </span>
      {subtitle ? (
        <span className="admin-sidebar-item-subtitle" aria-hidden="true">
          {subtitle}
        </span>
      ) : null}
    </span>
  );
}
