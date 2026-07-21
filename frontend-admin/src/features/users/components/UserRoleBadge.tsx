'use client';

import {
  CalculatorOutlined,
  CoffeeOutlined,
  CrownOutlined,
  ShoppingOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { Tag } from 'antd';
import React from 'react';

import { formatRoleBadgeLabel, isCanonicalRoleName } from '@/features/users/utils/roleDisplayLabel';
import { useI18n } from '@/i18n';

const ROLE_COLORS: Record<string, string> = {
  SuperAdmin: 'red',
  Manager: 'blue',
  Cashier: 'green',
  Accountant: 'orange',
  Waiter: 'cyan',
  Kitchen: 'default',
  ReportViewer: 'default',
};

const ROLE_ICONS: Record<string, React.ReactNode> = {
  SuperAdmin: <CrownOutlined />,
  Manager: <UserOutlined />,
  Cashier: <ShoppingOutlined />,
  Accountant: <CalculatorOutlined />,
  Waiter: <CoffeeOutlined />,
  Kitchen: <UserOutlined />,
  ReportViewer: <UserOutlined />,
};

export type UserRoleBadgeProps = {
  role: string;
  /**
   * @deprecated Owner is shown in tenant/name columns (UnifiedAdminUsersView, TenantUserTable).
   * Role column must only show the canonical role label.
   */
  isOwner?: boolean;
  /** @deprecated Prefer role-based colors; kept for callers that still pass it. */
  platform?: boolean;
};

/** Canonical role badge for user tables — does not render tenant-owner badge. */
export function UserRoleBadge({ role }: UserRoleBadgeProps) {
  const { t } = useI18n();
  const label = isCanonicalRoleName(role) ? formatRoleBadgeLabel(t, role) : role;
  const color = ROLE_COLORS[role] ?? 'default';
  const icon = ROLE_ICONS[role] ?? <UserOutlined />;

  return (
    <Tag color={color} icon={icon}>
      {label}
    </Tag>
  );
}
