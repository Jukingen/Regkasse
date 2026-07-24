'use client';

import { Space, Typography } from 'antd';
import React, { ReactNode } from 'react';

import { Breadcrumb } from '@/components/Breadcrumb';
import type { AdminBreadcrumbItem } from '@/shared/adminShellLabels';

const { Title } = Typography;

interface AdminPageHeaderProps {
  /** Plain string or inline elements (e.g. title + status Tag). */
  title: React.ReactNode;
  breadcrumbs?: AdminBreadcrumbItem[];
  /** Right-side actions (tenant switcher, buttons, …). */
  actions?: ReactNode;
  /** Alias for `actions` (used by many admin pages). */
  extra?: ReactNode;
  /** Optional subtitle under the title row. */
  subtitle?: ReactNode;
  children?: ReactNode;
}

export function AdminPageHeader({
  title,
  breadcrumbs,
  actions,
  extra,
  subtitle,
  children,
}: AdminPageHeaderProps) {
  const right = actions ?? extra;
  return (
    <div style={{ marginBottom: 24 }}>
      {breadcrumbs && <Breadcrumb items={breadcrumbs} />}

      <header>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <Title level={1} style={{ margin: 0 }}>
              {title}
            </Title>
            {subtitle ? (
              <Typography.Paragraph type="secondary" style={{ margin: '8px 0 0' }}>
                {subtitle}
              </Typography.Paragraph>
            ) : null}
          </div>
          {right && <Space>{right}</Space>}
        </div>
      </header>

      {children ? <div style={{ marginTop: 16 }}>{children}</div> : null}
    </div>
  );
}
