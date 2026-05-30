'use client';

import { useCallback, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  DatePicker,
  Form,
  Input,
  Modal,
  Space,
  Spin,
  Switch,
  Table,
  Tabs,
  Tag,
  Typography,
  message,
} from 'antd';
import { DeleteOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import dayjs, { type Dayjs } from 'dayjs';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import type { PermissionCatalogItemDto } from '@/features/users/api/roleManagementApi';
import type { UserPermissionOverrideDto } from '@/features/users/api/userPermissionOverridesApi';
import {
  useUserEffectivePermissions,
  useUserPermissionOverrideMutations,
} from '@/features/users/hooks/useUserEffectivePermissions';
import { usePermissionsCatalog } from '@/features/users/hooks/usePermissionsCatalog';
import { permissionCatalogGroupToSlug } from '@/features/users/utils/permissionCatalogGroup';
import { resolvePermissionDisplayLabel } from '@/features/users/utils/permissionDisplayLabel';
import { useI18n } from '@/i18n';
import { permissionImplied } from '@/shared/auth/permissionImplication';

export type UserPermissionsModalProps = {
  open: boolean;
  userId: string;
  userName: string;
  userRole?: string | null;
  onClose: () => void;
};

type OverrideFormValues = {
  reason?: string;
  expiresAt?: Dayjs | null;
};

function groupCatalogByGroup(catalog: PermissionCatalogItemDto[]): Map<string, PermissionCatalogItemDto[]> {
  const map = new Map<string, PermissionCatalogItemDto[]>();
  for (const item of catalog) {
    const slug = permissionCatalogGroupToSlug(item.group?.trim() || 'Other');
    if (!map.has(slug)) map.set(slug, []);
    map.get(slug)!.push(item);
  }
  return map;
}

export function UserPermissionsModal({
  open,
  userId,
  userName,
  userRole,
  onClose,
}: UserPermissionsModalProps) {
  const { t } = useI18n();
  const [activeTab, setActiveTab] = useState<string>();
  const [pendingToggle, setPendingToggle] = useState<{ permission: string; isGranted: boolean } | null>(null);
  const [overrideForm] = Form.useForm<OverrideFormValues>();

  const targetIsSuperAdmin = isSuperAdmin(userRole ?? undefined);
  const readOnly = targetIsSuperAdmin;

  const effectiveQuery = useUserEffectivePermissions(userId, open);
  const catalogQuery = usePermissionsCatalog({ enabled: open });
  const { upsertMutation, deleteMutation } = useUserPermissionOverrideMutations(userId);

  const rolePermissions = effectiveQuery.data?.rolePermissions ?? [];
  const effectivePermissions = effectiveQuery.data?.effectivePermissions ?? [];
  const overrides = effectiveQuery.data?.overrides ?? [];

  const overridesByPermission = useMemo(() => {
    const map = new Map<string, UserPermissionOverrideDto>();
    for (const o of overrides) {
      if (!map.has(o.permission)) map.set(o.permission, o);
    }
    return map;
  }, [overrides]);

  const handleDeleteOverride = useCallback(
    async (record: UserPermissionOverrideDto) => {
      try {
        await deleteMutation.mutateAsync(record.id);
        message.success(t('users.permissionsModal.removeSuccess'));
      } catch {
        message.error(t('users.permissionsModal.updateError'));
      }
    },
    [deleteMutation, t],
  );

  const groupedCatalog = useMemo(
    () => groupCatalogByGroup(catalogQuery.data ?? []),
    [catalogQuery.data],
  );

  const tabItems = useMemo(() => {
    const items = Array.from(groupedCatalog.entries()).map(([slug, itemsInGroup]) => ({
      key: slug,
      label: t(`users.roleDrawer.groups.${slug}`),
      children: (
        <PermissionGroupTable
          permissions={itemsInGroup.map((i) => i.key)}
          rolePermissions={rolePermissions}
          effectivePermissions={effectivePermissions}
          overridesByPermission={overridesByPermission}
          readOnly={readOnly}
          onToggle={(permission, isGranted) => setPendingToggle({ permission, isGranted })}
          t={t}
        />
      ),
    }));

    items.push({
      key: 'overrides',
      label: t('users.permissionsModal.tabOverrides'),
      children: (
        <OverridesTable
          overrides={overrides}
          readOnly={readOnly}
          loading={deleteMutation.isPending}
          onDelete={(record) => void handleDeleteOverride(record)}
          t={t}
        />
      ),
    });

    return items;
  }, [
    groupedCatalog,
    rolePermissions,
    effectivePermissions,
    overridesByPermission,
    readOnly,
    overrides,
    deleteMutation.isPending,
    handleDeleteOverride,
    t,
  ]);

  const submitToggle = useCallback(async () => {
    if (!pendingToggle) return;
    try {
      const values = await overrideForm.validateFields();
      await upsertMutation.mutateAsync({
        permission: pendingToggle.permission,
        isGranted: pendingToggle.isGranted,
        reason: values.reason?.trim() || t('users.permissionsModal.defaultReason'),
        expiresAt: values.expiresAt ? values.expiresAt.toISOString() : null,
      });
      message.success(t('users.permissionsModal.updateSuccess'));
      setPendingToggle(null);
      overrideForm.resetFields();
    } catch (err) {
      if (err && typeof err === 'object' && 'errorFields' in err) return;
      message.error(t('users.permissionsModal.updateError'));
    }
  }, [pendingToggle, overrideForm, upsertMutation, t]);

  const loading = effectiveQuery.isLoading || catalogQuery.isLoading;
  const error = effectiveQuery.isError || catalogQuery.isError;

  return (
    <>
      <Modal
        title={t('users.permissionsModal.title', { name: userName })}
        open={open}
        onCancel={onClose}
        width={860}
        destroyOnClose
        footer={[
          <Button key="close" onClick={onClose}>
            {t('users.permissionsModal.close')}
          </Button>,
        ]}
      >
        {targetIsSuperAdmin ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message={t('users.permissionsModal.superAdminTitle')}
            description={t('users.permissionsModal.superAdminDescription')}
          />
        ) : (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message={t('users.permissionsModal.infoTitle')}
            description={t('users.permissionsModal.infoDescription')}
          />
        )}

        {error ? (
          <Alert
            type="error"
            showIcon
            message={t('users.permissionsModal.loadError')}
            action={
              <Button size="small" onClick={() => void effectiveQuery.refetch()}>
                {t('users.list.retry')}
              </Button>
            }
          />
        ) : loading ? (
          <div className="flex justify-center py-12">
            <Spin />
          </div>
        ) : (
          <Tabs
            activeKey={activeTab ?? tabItems[0]?.key}
            onChange={setActiveTab}
            items={tabItems}
            size="small"
          />
        )}
      </Modal>

      <Modal
        title={t('users.permissionsModal.confirmTitle')}
        open={pendingToggle != null}
        onCancel={() => {
          setPendingToggle(null);
          overrideForm.resetFields();
        }}
        onOk={() => void submitToggle()}
        confirmLoading={upsertMutation.isPending}
        destroyOnClose
      >
        <Typography.Paragraph type="secondary">
          {pendingToggle
            ? t('users.permissionsModal.confirmBody', {
                permission: resolvePermissionDisplayLabel(pendingToggle.permission, t),
                status: pendingToggle.isGranted
                  ? t('users.permissionsModal.statusGranted')
                  : t('users.permissionsModal.statusDenied'),
              })
            : null}
        </Typography.Paragraph>
        <Form form={overrideForm} layout="vertical">
          <Form.Item name="reason" label={t('users.permissionsModal.reasonLabel')}>
            <Input.TextArea rows={2} maxLength={500} />
          </Form.Item>
          <Form.Item name="expiresAt" label={t('users.permissionsModal.expiresLabel')}>
            <DatePicker className="w-full" format="DD.MM.YYYY" />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}

type PermissionGroupTableProps = {
  permissions: string[];
  rolePermissions: string[];
  effectivePermissions: string[];
  overridesByPermission: Map<string, UserPermissionOverrideDto>;
  readOnly: boolean;
  onToggle: (permission: string, isGranted: boolean) => void;
  t: (key: string, options?: Record<string, string | number>) => string;
};

function PermissionGroupTable({
  permissions,
  rolePermissions,
  effectivePermissions,
  overridesByPermission,
  readOnly,
  onToggle,
  t,
}: PermissionGroupTableProps) {
  const columns: ColumnsType<{ permission: string }> = [
    {
      title: t('users.permissionsModal.columnPermission'),
      dataIndex: 'permission',
      key: 'permission',
      render: (perm: string) => (
        <div>
          <div>{resolvePermissionDisplayLabel(perm, t)}</div>
          <Typography.Text type="secondary" className="text-xs">
            {perm}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: t('users.permissionsModal.columnDefault'),
      key: 'default',
      width: 130,
      render: (_: unknown, record: { permission: string }) => {
        const allowed = permissionImplied(record.permission, rolePermissions);
        return (
          <Tag color={allowed ? 'green' : 'default'}>
            {allowed ? t('users.permissionsModal.allowed') : t('users.permissionsModal.notAllowed')}
          </Tag>
        );
      },
    },
    {
      title: t('users.permissionsModal.columnOverride'),
      key: 'override',
      width: 120,
      render: (_: unknown, record: { permission: string }) => {
        const override = overridesByPermission.get(record.permission);
        if (!override) return <Tag>{t('users.permissionsModal.noOverride')}</Tag>;
        return (
          <Tag color={override.isGranted ? 'blue' : 'red'}>
            {override.isGranted
              ? t('users.permissionsModal.overrideGrant')
              : t('users.permissionsModal.overrideDeny')}
          </Tag>
        );
      },
    },
    {
      title: t('users.permissionsModal.columnEffective'),
      key: 'effective',
      width: 120,
      render: (_: unknown, record: { permission: string }) => {
        const allowed = permissionImplied(record.permission, effectivePermissions);
        return (
          <Tag color={allowed ? 'success' : 'error'}>
            {allowed ? t('users.permissionsModal.allowed') : t('users.permissionsModal.notAllowed')}
          </Tag>
        );
      },
    },
    {
      title: t('users.permissionsModal.columnIndividual'),
      key: 'individual',
      width: 120,
      render: (_: unknown, record: { permission: string }) => {
        const effectiveAllowed = permissionImplied(record.permission, effectivePermissions);
        return (
          <Switch
            checked={effectiveAllowed}
            disabled={readOnly}
            checkedChildren={t('users.permissionsModal.switchOn')}
            unCheckedChildren={t('users.permissionsModal.switchOff')}
            onChange={(checked) => onToggle(record.permission, checked)}
          />
        );
      },
    },
  ];

  return (
    <Table
      dataSource={permissions.map((permission) => ({ permission }))}
      columns={columns}
      rowKey="permission"
      pagination={false}
      size="small"
    />
  );
}

type OverridesTableProps = {
  overrides: UserPermissionOverrideDto[];
  readOnly: boolean;
  loading: boolean;
  onDelete: (record: UserPermissionOverrideDto) => void;
  t: (key: string, options?: Record<string, string | number>) => string;
};

function OverridesTable({ overrides, readOnly, loading, onDelete, t }: OverridesTableProps) {
  const columns: ColumnsType<UserPermissionOverrideDto> = [
    {
      title: t('users.permissionsModal.columnPermission'),
      dataIndex: 'permission',
      render: (perm: string) => resolvePermissionDisplayLabel(perm, t),
    },
    {
      title: t('users.permissionsModal.columnStatus'),
      dataIndex: 'isGranted',
      width: 120,
      render: (granted: boolean) =>
        granted ? t('users.permissionsModal.statusGranted') : t('users.permissionsModal.statusDenied'),
    },
    {
      title: t('users.permissionsModal.reasonLabel'),
      dataIndex: 'reason',
      ellipsis: true,
      render: (v: string | null | undefined) => v?.trim() || '—',
    },
    {
      title: t('users.permissionsModal.expiresLabel'),
      dataIndex: 'expiresAt',
      width: 120,
      render: (d: string | null | undefined) => (d ? dayjs(d).format('DD.MM.YYYY') : '—'),
    },
    {
      key: 'actions',
      width: 56,
      render: (_: unknown, record) =>
        readOnly ? null : (
          <Button
            type="text"
            danger
            icon={<DeleteOutlined />}
            loading={loading}
            aria-label={t('users.permissionsModal.removeOverride')}
            onClick={() => onDelete(record)}
          />
        ),
    },
  ];

  return (
    <Table
      dataSource={overrides}
      columns={columns}
      rowKey="id"
      pagination={false}
      size="small"
      locale={{ emptyText: t('users.permissionsModal.noOverrides') }}
    />
  );
}
