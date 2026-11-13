'use client';

import { CopyOutlined, UserOutlined } from '@ant-design/icons';
import { Avatar, Button, Popover, Space, Tag, Typography } from 'antd';
import Link from 'next/link';
import { useMemo } from 'react';

import type { AuditActorUserInfo } from '@/features/audit/types/auditActorUser';
import { formatRoleBadgeLabel } from '@/features/users/utils/roleDisplayLabel';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

const ROLE_TAG_COLORS: Record<string, string> = {
  SuperAdmin: 'red',
  Manager: 'blue',
  Cashier: 'green',
  Accountant: 'purple',
  Waiter: 'orange',
  Kitchen: 'cyan',
  ReportViewer: 'default',
};

const ROLE_AVATAR_COLORS: Record<string, string> = {
  SuperAdmin: '#cf1322',
  Manager: '#1677ff',
  Cashier: '#389e0d',
  Accountant: '#531dab',
  Waiter: '#d46b08',
  Kitchen: '#08979c',
  ReportViewer: '#8c8c8c',
};

export type UserInfoDisplayProps = {
  user?: AuditActorUserInfo | null;
  userId?: string | null;
  userRole?: string | null;
  /** Compact cell layout for dense tables. */
  compact?: boolean;
  /**
   * Optional override when opening user details.
   * Default: navigate to `/admin/users/{id}`.
   */
  onUserClick?: (userId: string) => void;
};

export function UserInfoDisplay({
  user,
  userId,
  userRole,
  compact = true,
  onUserClick,
}: UserInfoDisplayProps) {
  const { t } = useI18n();
  const notify = useNotify();

  const unknown = t('common.auditLogs.userInfo.unknown');
  const noEmail = t('common.auditLogs.userInfo.noEmail');

  const displayName =
    user?.displayName?.trim() || user?.userName?.trim() || userId?.trim() || unknown;
  const userName = user?.userName?.trim() || unknown;
  const email = user?.email?.trim() || noEmail;
  const role = user?.role?.trim() || userRole?.trim() || '';
  const id = user?.id?.trim() || userId?.trim() || '';
  const userDetailHref = id ? `/admin/users/${encodeURIComponent(id)}` : null;

  const roleLabel = useMemo(
    () => (role ? formatRoleBadgeLabel(t, role) : t('common.auditLogs.userInfo.unknownRole')),
    [role, t]
  );
  const roleColor = ROLE_TAG_COLORS[role] ?? 'default';
  const avatarColor = ROLE_AVATAR_COLORS[role] ?? '#1677ff';
  const avatarLetter = displayName.charAt(0).toUpperCase() || '?';
  const idPreview = id.length > 8 ? `${id.slice(0, 8)}…` : id;

  const handleCopyId = async () => {
    if (!id) return;
    try {
      await navigator.clipboard.writeText(id);
      notify.successKey('common.auditLogs.userInfo.idCopied');
    } catch {
      notify.errorKey('common.auditLogs.userInfo.idCopyFailed');
    }
  };

  const handleOpenDetails = () => {
    if (!id) return;
    onUserClick?.(id);
  };

  const popoverContent = (
    <div style={{ maxWidth: 280 }} onClick={(e) => e.stopPropagation()}>
      <Space align="start" size="middle" style={{ marginBottom: 8 }}>
        <Avatar size="small" style={{ backgroundColor: avatarColor }}>
          {avatarLetter}
        </Avatar>
        <Typography.Text strong>{displayName}</Typography.Text>
      </Space>
      <div style={{ fontSize: 12, lineHeight: 1.7 }}>
        <div>
          <Typography.Text type="secondary">
            {t('common.auditLogs.userInfo.userName')}:{' '}
          </Typography.Text>
          <Typography.Text code style={{ fontSize: 11 }}>
            {userName}
          </Typography.Text>
        </div>
        <div>
          <Typography.Text type="secondary">
            {t('common.auditLogs.userInfo.email')}:{' '}
          </Typography.Text>
          <Typography.Text>{email}</Typography.Text>
        </div>
        <div>
          <Typography.Text type="secondary">{t('common.auditLogs.userInfo.role')}: </Typography.Text>
          <Tag color={roleColor}>{roleLabel}</Tag>
        </div>
        {id ? (
          <div style={{ marginTop: 8, paddingTop: 8, borderTop: '1px solid #f0f0f0' }}>
            <Typography.Text type="secondary" style={{ fontSize: 11 }}>
              {t('common.auditLogs.userInfo.id')}:{' '}
            </Typography.Text>
            <Typography.Text code copyable={{ text: id }} style={{ fontSize: 11 }}>
              {id}
            </Typography.Text>
          </div>
        ) : null}
      </div>
      {id ? (
        <Space orientation="vertical" size={0} style={{ marginTop: 8, width: '100%' }}>
          {userDetailHref && !onUserClick ? (
            <Link href={userDetailHref} onClick={(e) => e.stopPropagation()}>
              <Button type="link" size="small" icon={<UserOutlined />} style={{ paddingInline: 0 }}>
                {t('common.auditLogs.userInfo.goToDetails')}
              </Button>
            </Link>
          ) : (
            <Button
              type="link"
              size="small"
              icon={<UserOutlined />}
              style={{ paddingInline: 0 }}
              onClick={handleOpenDetails}
            >
              {t('common.auditLogs.userInfo.goToDetails')}
            </Button>
          )}
          <Button
            type="link"
            size="small"
            icon={<CopyOutlined />}
            style={{ paddingInline: 0 }}
            onClick={handleCopyId}
          >
            {t('common.auditLogs.userInfo.copyId')}
          </Button>
        </Space>
      ) : null}
    </div>
  );

  return (
    <Popover content={popoverContent} trigger="click" placement="right">
      <Space
        size={compact ? 4 : 8}
        style={{ cursor: 'pointer', maxWidth: '100%' }}
        onClick={(e) => e.stopPropagation()}
        onKeyDown={(e) => e.stopPropagation()}
      >
        <Avatar size="small" style={{ backgroundColor: avatarColor, flexShrink: 0 }}>
          {avatarLetter}
        </Avatar>
        <Typography.Text ellipsis style={{ maxWidth: compact ? 96 : 140, fontWeight: 500 }}>
          {displayName}
        </Typography.Text>
        {role ? (
          <Tag color={roleColor} style={{ marginInlineEnd: 0, fontSize: 11, lineHeight: '18px' }}>
            {roleLabel}
          </Tag>
        ) : null}
        {idPreview ? (
          <Typography.Text type="secondary" style={{ fontSize: 11, fontFamily: 'monospace' }}>
            {idPreview}
          </Typography.Text>
        ) : null}
      </Space>
    </Popover>
  );
}
