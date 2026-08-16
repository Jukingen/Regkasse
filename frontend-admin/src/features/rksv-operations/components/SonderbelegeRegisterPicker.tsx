'use client';

import { Collapse, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useCallback, useMemo } from 'react';

import type { EnhancedCashRegister } from '@/features/cash-registers/types/enhancedCashRegister';
import { rawRegisterStatus } from '@/features/cash-registers/utils/registerStatus';
import {
  compareSonderbelegeRegisters,
  formatSonderbelegeRegisterLabel,
  groupRegistersByTenant,
  resolveRegisterName,
  resolveTenantLabel,
  sonderbelegeStatusVisual,
} from '@/features/rksv-operations/utils/sonderbelegeRegisterDisplay';
import { useI18n } from '@/i18n';
import { formatRegisterDisplayLabel } from '@/shared/utils/registerIdentity';

export type SonderbelegeRegisterPickerProps = {
  registers: EnhancedCashRegister[];
  loading?: boolean;
  registerId?: string;
  onRegisterChange: (registerId: string | undefined) => void;
  /** Super Admin: show tenant filter; Manager: omitted. */
  showTenantFilter?: boolean;
  tenants: Array<{ id: string; name: string; slug: string }>;
  tenantsLoading?: boolean;
  tenantFilterId?: string;
  onTenantFilterChange: (tenantId: string | undefined) => void;
};

function statusLabelKey(key: string): string {
  return `rksvHub.sonderbelege.status.${key}`;
}

export function SonderbelegeRegisterPicker({
  registers,
  loading = false,
  registerId,
  onRegisterChange,
  showTenantFilter = false,
  tenants,
  tenantsLoading = false,
  tenantFilterId,
  onTenantFilterChange,
}: SonderbelegeRegisterPickerProps) {
  const { t } = useI18n();

  const sortedRegisters = useMemo(
    () => [...registers].sort(compareSonderbelegeRegisters),
    [registers]
  );

  const tenantGroups = useMemo(
    () => groupRegistersByTenant(sortedRegisters),
    [sortedRegisters]
  );

  const showGroupedBrowser = showTenantFilter && tenantGroups.length > 1 && !tenantFilterId;

  const statusText = useCallback(
    (status: number | undefined) => {
      const visual = sonderbelegeStatusVisual(status);
      if (visual.key === 'unknown') {
        return status != null
          ? t('rksvHub.sonderbelege.status.unknown', { status })
          : t('rksvHub.sonderbelege.status.unknown', { status: '—' });
      }
      return t(statusLabelKey(visual.key));
    },
    [t]
  );

  const tenantFilterOptions = useMemo(
    () =>
      tenants.map((row) => ({
        value: row.id,
        label: `${row.name} (${row.slug})`,
      })),
    [tenants]
  );

  const selectOptions = useMemo(() => {
    const includeTenantInLabel = showTenantFilter && !tenantFilterId;

    if (showGroupedBrowser) {
      return tenantGroups.map((group) => ({
        label: t('rksvHub.sonderbelege.tenantGroupLabel', {
          name: group.tenantName,
          count: group.registers.length,
        }),
        options: group.registers.map((reg) => {
          const status = rawRegisterStatus(reg);
          const visual = sonderbelegeStatusVisual(status);
          return {
            value: String(reg.id),
            label: formatSonderbelegeRegisterLabel(reg, { includeTenant: false }),
            searchText: [
              formatRegisterDisplayLabel(reg.registerNumber),
              resolveRegisterName(reg),
              resolveTenantLabel(reg),
              statusText(status),
            ]
              .join(' ')
              .toLowerCase(),
            status,
            statusEmoji: visual.emoji,
            statusColor: visual.color,
            statusLabel: statusText(status),
          };
        }),
      }));
    }

    return sortedRegisters.map((reg) => {
      const status = rawRegisterStatus(reg);
      const visual = sonderbelegeStatusVisual(status);
      return {
        value: String(reg.id),
        label: formatSonderbelegeRegisterLabel(reg, {
          includeTenant: includeTenantInLabel,
        }),
        searchText: [
          formatRegisterDisplayLabel(reg.registerNumber),
          resolveRegisterName(reg),
          resolveTenantLabel(reg),
          statusText(status),
        ]
          .join(' ')
          .toLowerCase(),
        status,
        statusEmoji: visual.emoji,
        statusColor: visual.color,
        statusLabel: statusText(status),
      };
    });
  }, [
    showGroupedBrowser,
    showTenantFilter,
    sortedRegisters,
    statusText,
    t,
    tenantFilterId,
    tenantGroups,
  ]);

  const tableColumns: ColumnsType<EnhancedCashRegister> = useMemo(
    () => [
      {
        title: t('rksvHub.sonderbelege.registerNumber'),
        key: 'registerNumber',
        width: 110,
        sorter: (a, b) =>
          formatRegisterDisplayLabel(a.registerNumber).localeCompare(
            formatRegisterDisplayLabel(b.registerNumber),
            'de',
            { numeric: true }
          ),
        defaultSortOrder: 'ascend',
        render: (_, row) => (
          <Typography.Text strong>
            {formatRegisterDisplayLabel(row.registerNumber)}
          </Typography.Text>
        ),
      },
      {
        title: t('rksvHub.sonderbelege.registerName'),
        key: 'name',
        ellipsis: true,
        render: (_, row) => resolveRegisterName(row),
      },
      {
        title: t('rksvHub.sonderbelege.statusColumn'),
        key: 'status',
        width: 160,
        render: (_, row) => {
          const status = rawRegisterStatus(row);
          const visual = sonderbelegeStatusVisual(status);
          return (
            <Tag color={visual.color}>
              {visual.emoji} {statusText(status)}
            </Tag>
          );
        },
      },
    ],
    [statusText, t]
  );

  const collapseItems = useMemo(
    () =>
      tenantGroups.map((group) => ({
        key: group.tenantId,
        label: t('rksvHub.sonderbelege.tenantGroupLabel', {
          name: group.tenantName,
          count: group.registers.length,
        }),
        children: (
          <Table<EnhancedCashRegister>
            size="small"
            rowKey={(row) => String(row.id)}
            pagination={false}
            columns={tableColumns}
            dataSource={group.registers}
            rowSelection={{
              type: 'radio',
              selectedRowKeys: registerId ? [registerId] : [],
              onChange: (keys) => {
                const next = keys[0];
                onRegisterChange(next != null ? String(next) : undefined);
              },
            }}
            onRow={(row) => ({
              onClick: () => onRegisterChange(String(row.id)),
              style: { cursor: 'pointer' },
            })}
          />
        ),
      })),
    [onRegisterChange, registerId, t, tableColumns, tenantGroups]
  );

  const defaultCollapseKeys = useMemo(() => {
    if (registerId) {
      const group = tenantGroups.find((g) =>
        g.registers.some((r) => String(r.id) === registerId)
      );
      if (group) return [group.tenantId];
    }
    // Expand tenants with few registers; collapse large groups by default beyond first.
    return tenantGroups
      .filter((g) => g.registers.length <= 5)
      .slice(0, 3)
      .map((g) => g.tenantId);
  }, [registerId, tenantGroups]);

  return (
    <Space orientation="vertical" style={{ width: '100%' }} size="middle">
      {showTenantFilter ? (
        <div>
          <Typography.Text strong>{t('rksvHub.sonderbelege.filterByTenant')}</Typography.Text>
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('rksvHub.sonderbelege.allTenants')}
            style={{ width: '100%', marginTop: 8 }}
            loading={tenantsLoading}
            value={tenantFilterId}
            onChange={(value) => {
              const next = typeof value === 'string' && value.trim() ? value.trim() : undefined;
              onTenantFilterChange(next);
            }}
            options={tenantFilterOptions}
          />
        </div>
      ) : null}

      <div>
        <Typography.Text strong>{t('rksvHub.sonderbelege.selectRegisterLabel')}</Typography.Text>
        <Select
          showSearch
          allowClear
          placeholder={t('rksvHub.sonderbelege.selectRegisterPlaceholder')}
          style={{ width: '100%', marginTop: 8 }}
          loading={loading}
          value={registerId}
          onChange={(v) => onRegisterChange(v)}
          options={selectOptions}
          optionFilterProp="searchText"
          optionRender={(option) => {
            const data = option.data as {
              label?: string;
              statusEmoji?: string;
              statusColor?: string;
              statusLabel?: string;
            };
            return (
              <Space style={{ width: '100%', justifyContent: 'space-between' }}>
                <span>{data.label}</span>
                {data.statusLabel ? (
                  <Tag color={data.statusColor ?? 'default'} style={{ marginInlineEnd: 0 }}>
                    {data.statusEmoji} {data.statusLabel}
                  </Tag>
                ) : null}
              </Space>
            );
          }}
          labelRender={(props) => {
            const selected = sortedRegisters.find((r) => String(r.id) === String(props.value));
            if (!selected) return props.label;
            return formatSonderbelegeRegisterLabel(selected, {
              includeTenant: showTenantFilter,
            });
          }}
        />
      </div>

      {showGroupedBrowser ? (
        <div>
          <Typography.Text strong>{t('rksvHub.sonderbelege.groupedByTenant')}</Typography.Text>
          <Collapse
            style={{ marginTop: 8 }}
            items={collapseItems}
            defaultActiveKey={defaultCollapseKeys}
          />
        </div>
      ) : null}

      {!showGroupedBrowser && sortedRegisters.length > 0 ? (
        <Table<EnhancedCashRegister>
          size="small"
          rowKey={(row) => String(row.id)}
          pagination={sortedRegisters.length > 8 ? { pageSize: 8 } : false}
          columns={tableColumns}
          dataSource={sortedRegisters}
          rowSelection={{
            type: 'radio',
            selectedRowKeys: registerId ? [registerId] : [],
            onChange: (keys) => {
              const next = keys[0];
              onRegisterChange(next != null ? String(next) : undefined);
            },
          }}
          onRow={(row) => ({
            onClick: () => onRegisterChange(String(row.id)),
            style: { cursor: 'pointer' },
          })}
        />
      ) : null}
    </Space>
  );
}
