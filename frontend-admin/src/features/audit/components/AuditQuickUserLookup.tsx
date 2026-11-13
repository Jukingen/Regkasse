'use client';

import { SearchOutlined, UserOutlined } from '@ant-design/icons';
import { Avatar, Button, Input, Space, Tag, Typography } from 'antd';
import { useState } from 'react';

import {
  type AuditUserLookupResult,
  useAuditUserLookup,
} from '@/features/audit/hooks/useAuditUserLookup';
import { formatRoleBadgeLabel } from '@/features/users/utils/roleDisplayLabel';
import { useI18n } from '@/i18n';

export type AuditQuickUserLookupProps = {
  onFilterByUser: (userId: string) => void;
  onSearchAudits: (search: string) => void;
};

export function AuditQuickUserLookup({
  onFilterByUser,
  onSearchAudits,
}: AuditQuickUserLookupProps) {
  const { t } = useI18n();
  const [draft, setDraft] = useState('');
  const { lookup, clear, result, matches, hasSearched, isLookingUp, isError } = useAuditUserLookup();

  const runLookup = () => {
    lookup(draft);
  };

  const applyUser = (user: AuditUserLookupResult) => {
    onFilterByUser(user.id);
  };

  return (
    <div>
      <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
        {t('common.auditLogs.quickUser.title')}
      </Typography.Text>
      <Space.Compact style={{ width: '100%', maxWidth: 480 }}>
        <Input
          allowClear
          prefix={<UserOutlined />}
          placeholder={t('common.auditLogs.quickUser.placeholder')}
          value={draft}
          onChange={(e) => {
            setDraft(e.target.value);
            if (!e.target.value.trim()) clear();
          }}
          onPressEnter={runLookup}
        />
        <Button type="primary" icon={<SearchOutlined />} loading={isLookingUp} onClick={runLookup}>
          {t('common.auditLogs.quickUser.find')}
        </Button>
      </Space.Compact>

      {isError ? (
        <Typography.Text type="danger" style={{ display: 'block', marginTop: 8, fontSize: 12 }}>
          {t('common.auditLogs.quickUser.error')}
        </Typography.Text>
      ) : null}

      {!isLookingUp && hasSearched && matches.length === 0 && !isError ? (
        <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8, fontSize: 12 }}>
          {t('common.auditLogs.quickUser.noResults')}{' '}
          <Button
            type="link"
            size="small"
            style={{ padding: 0, height: 'auto' }}
            onClick={() => onSearchAudits(draft.trim())}
          >
            {t('common.auditLogs.quickUser.searchAuditsInstead')}
          </Button>
        </Typography.Text>
      ) : null}

      {result ? (
        <div
          style={{
            marginTop: 12,
            padding: 12,
            background: 'var(--ant-color-fill-quaternary, #fafafa)',
            borderRadius: 8,
            maxWidth: 480,
          }}
        >
          <Space align="start" size="middle">
            <Avatar size="large" style={{ backgroundColor: '#1677ff' }}>
              {result.displayName.charAt(0).toUpperCase()}
            </Avatar>
            <div style={{ flex: 1, minWidth: 0 }}>
              <Typography.Text strong>{result.displayName}</Typography.Text>
              <div>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  @{result.userName}
                </Typography.Text>
              </div>
              <div>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {result.email}
                </Typography.Text>
              </div>
              <Space wrap size={[4, 4]} style={{ marginTop: 6 }}>
                <Tag color="blue">{formatRoleBadgeLabel(t, result.role)}</Tag>
                <Tag>
                  {t('common.auditLogs.userInfo.id')}: {result.id.slice(0, 8)}…
                </Tag>
              </Space>
              <Space wrap style={{ marginTop: 8 }}>
                <Button type="primary" size="small" onClick={() => applyUser(result)}>
                  {t('common.auditLogs.quickUser.filterAudits')}
                </Button>
                <Button size="small" onClick={() => onSearchAudits(result.userName)}>
                  {t('common.auditLogs.quickUser.searchByUserName')}
                </Button>
                {matches.length > 1 ? (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {t('common.auditLogs.quickUser.moreMatches', { count: matches.length })}
                  </Typography.Text>
                ) : null}
              </Space>
            </div>
          </Space>
          {matches.length > 1 ? (
            <div style={{ marginTop: 10 }}>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('common.auditLogs.quickUser.otherMatches')}
              </Typography.Text>
              <Space wrap size={[4, 4]} style={{ marginTop: 4 }}>
                {matches.slice(1).map((m) => (
                  <Button key={m.id} size="small" type="link" onClick={() => applyUser(m)}>
                    {m.displayName}
                  </Button>
                ))}
              </Space>
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
