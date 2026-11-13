'use client';

import { ClearOutlined, SearchOutlined } from '@ant-design/icons';
import { Button, Col, DatePicker, Input, Row, Select, Switch, Typography } from 'antd';
import type { Dayjs } from 'dayjs';
import { useEffect, useState } from 'react';

import { AuditQuickUserLookup } from '@/features/audit/components/AuditQuickUserLookup';
import { EntityTypeFilterSelect } from '@/features/audit-logs/components/EntityTypeFilterSelect';
import { StatusFilterSelect } from '@/features/audit-logs/components/StatusFilterSelect';
import { UserFilterSelect } from '@/features/audit-logs/components/UserFilterSelect';
import type {
  AuditLogEntityTypeFilter,
  AuditLogStatusFilter,
} from '@/features/audit-logs/constants/auditLogFilters';
import {
  AUDIT_ACTION_FILTER_VALUES,
  getAuditActionLabelKey,
} from '@/features/audit-logs/utils/auditActionLabels';
import { useI18n } from '@/i18n';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';

const { RangePicker } = DatePicker;

export type AuditFilterBarProps = {
  search?: string;
  action?: string;
  userId?: string;
  targetUserId?: string;
  entityType?: AuditLogEntityTypeFilter;
  entityId?: string;
  ipAddress?: string;
  status?: AuditLogStatusFilter;
  statusOutcome?: 'success' | 'failure';
  hasChanges?: boolean;
  dateRange: [Dayjs | null, Dayjs | null] | null;
  onSearchChange: (search: string | undefined) => void;
  onActionChange: (action: string | undefined) => void;
  onUserIdChange: (userId: string | undefined) => void;
  onTargetUserIdChange: (targetUserId: string | undefined) => void;
  onEntityTypeChange: (entityType: AuditLogEntityTypeFilter | undefined) => void;
  onEntityIdChange: (entityId: string | undefined) => void;
  onIpAddressChange: (ipAddress: string | undefined) => void;
  onStatusChange: (status: AuditLogStatusFilter | undefined) => void;
  onStatusOutcomeChange: (outcome: 'success' | 'failure' | undefined) => void;
  onHasChangesChange: (hasChanges: boolean | undefined) => void;
  onDateRangeChange: (startDate: string | undefined, endDate: string | undefined) => void;
  onClearFilters: () => void;
};

export function AuditFilterBar({
  search,
  action,
  userId,
  targetUserId,
  entityType,
  entityId,
  ipAddress,
  status,
  statusOutcome,
  hasChanges,
  dateRange,
  onSearchChange,
  onActionChange,
  onUserIdChange,
  onTargetUserIdChange,
  onEntityTypeChange,
  onEntityIdChange,
  onIpAddressChange,
  onStatusChange,
  onStatusOutcomeChange,
  onHasChangesChange,
  onDateRangeChange,
  onClearFilters,
}: AuditFilterBarProps) {
  const { t } = useI18n();
  const [searchDraft, setSearchDraft] = useState(search ?? '');

  useEffect(() => {
    setSearchDraft(search ?? '');
  }, [search]);

  const actionFilterOptions = AUDIT_ACTION_FILTER_VALUES.map((value) => {
    const labelKey = getAuditActionLabelKey(value);
    return {
      value,
      label: labelKey ? t(labelKey as 'common.auditLogs.actionLabels.login') : value,
    };
  });

  const outcomeOptions = [
    { value: 'success', label: t('common.auditLogs.statusOutcome.success') },
    { value: 'failure', label: t('common.auditLogs.statusOutcome.failure') },
  ];

  const commitSearch = (value: string) => {
    const trimmed = value.trim();
    setSearchDraft(trimmed);
    onSearchChange(trimmed || undefined);
  };

  return (
    <div>
      <Row gutter={[12, 12]} align="middle" style={{ marginBottom: 12 }}>
        <Col xs={24} lg={16}>
          <Input.Search
            allowClear
            size="large"
            prefix={<SearchOutlined />}
            placeholder={t('common.auditLogs.searchPlaceholder')}
            value={searchDraft}
            onChange={(e) => setSearchDraft(e.target.value)}
            onSearch={commitSearch}
            enterButton={t('common.auditLogs.searchButton')}
          />
          <Typography.Text type="secondary" style={{ display: 'block', marginTop: 6, fontSize: 12 }}>
            {t('common.auditLogs.searchHint')}
          </Typography.Text>
        </Col>
        <Col xs={24} lg={8}>
          <AuditQuickUserLookup
            onFilterByUser={(id) => onUserIdChange(id)}
            onSearchAudits={(term) => commitSearch(term)}
          />
        </Col>
      </Row>

      <Row gutter={[12, 12]} align="middle">
        <Col xs={24} sm={12} md={6} lg={4}>
          <UserFilterSelect
            value={userId}
            onChange={onUserIdChange}
            placeholder={t('common.auditLogs.actorUserPlaceholder')}
          />
        </Col>
        <Col xs={24} sm={12} md={6} lg={4}>
          <UserFilterSelect
            value={targetUserId}
            onChange={onTargetUserIdChange}
            placeholder={t('common.auditLogs.targetUserPlaceholder')}
          />
        </Col>
        <Col xs={24} sm={12} md={6} lg={4}>
          <EntityTypeFilterSelect value={entityType} onChange={onEntityTypeChange} />
        </Col>
        <Col xs={24} sm={12} md={6} lg={3}>
          <Input
            allowClear
            placeholder={t('common.auditLogs.entityIdPlaceholder')}
            value={entityId}
            onChange={(e) => onEntityIdChange(e.target.value.trim() || undefined)}
          />
        </Col>
        <Col xs={24} sm={12} md={6} lg={3}>
          <Input
            allowClear
            placeholder={t('common.auditLogs.ipAddressPlaceholder')}
            value={ipAddress}
            onChange={(e) => onIpAddressChange(e.target.value.trim() || undefined)}
          />
        </Col>
        <Col xs={24} sm={12} md={6} lg={3}>
          <StatusFilterSelect value={status} onChange={onStatusChange} disabled={!!statusOutcome} />
        </Col>
        <Col xs={24} sm={12} md={6} lg={3}>
          <Select
            allowClear
            placeholder={t('common.auditLogs.statusOutcomePlaceholder')}
            style={{ width: '100%' }}
            value={statusOutcome}
            disabled={!!status}
            onChange={(v) => onStatusOutcomeChange(v)}
            options={outcomeOptions}
          />
        </Col>
        <Col xs={24} sm={12} md={6} lg={4}>
          <Select
            placeholder={t('common.auditLogs.actionPlaceholder')}
            style={{ width: '100%' }}
            allowClear
            value={action}
            onChange={(value) => onActionChange(value ?? undefined)}
            options={actionFilterOptions}
            showSearch
            optionFilterProp="label"
          />
        </Col>
        <Col xs={24} sm={12} md={8} lg={4}>
          <RangePicker
            format={DAYJS_DATE_FORMAT}
            value={dateRange ?? undefined}
            onChange={(dates) =>
              onDateRangeChange(dates?.[0]?.format('YYYY-MM-DD'), dates?.[1]?.format('YYYY-MM-DD'))
            }
          />
        </Col>
        <Col xs={24} sm={12} md={8} lg={4}>
          <span style={{ marginRight: 8 }}>{t('common.auditLogs.hasChangesOnly')}</span>
          <Switch
            checked={hasChanges === true}
            onChange={(checked) => onHasChangesChange(checked ? true : undefined)}
          />
        </Col>
        <Col xs={24} sm={12} md={4} lg={24} style={{ textAlign: 'right' }}>
          <Button icon={<ClearOutlined />} onClick={onClearFilters}>
            {t('common.auditLogs.clearFilters')}
          </Button>
        </Col>
      </Row>
    </div>
  );
}
