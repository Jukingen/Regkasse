'use client';

import { Button, Select, Space, Tag, Typography } from 'antd';
import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  addPackageToRole,
  listPermissionPackages,
  listRoleAssignedPackages,
  removePackageFromRole,
} from '@/features/users/api/permissionPackagesApi';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

type RolePackagesSectionProps = {
  roleName: string;
  canEdit: boolean;
};

export function RolePackagesSection({ roleName, canEdit }: RolePackagesSectionProps) {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const queryClient = useQueryClient();
  const [selectedPackageId, setSelectedPackageId] = useState<string | null>(null);

  const packagesQuery = useQuery({
    queryKey: ['permission-packages'],
    queryFn: listPermissionPackages,
  });
  const assignedQuery = useQuery({
    queryKey: ['permission-packages', 'role', roleName],
    queryFn: () => listRoleAssignedPackages(roleName),
    enabled: Boolean(roleName),
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['permission-packages', 'role', roleName] });
  };

  const addMutation = useMutation({
    mutationFn: (packageId: string) => addPackageToRole(roleName, packageId),
    onSuccess: () => {
      message.success(t('users.roleDrawer.packageAddSuccess'));
      setSelectedPackageId(null);
      invalidate();
    },
    onError: () => message.error(t('users.roleDrawer.packageAddError')),
  });

  const removeMutation = useMutation({
    mutationFn: (packageId: string) => removePackageFromRole(roleName, packageId),
    onSuccess: () => {
      message.success(t('users.roleDrawer.packageRemoveSuccess'));
      invalidate();
    },
    onError: () => message.error(t('users.roleDrawer.packageRemoveError')),
  });

  const assignedIds = useMemo(
    () => new Set((assignedQuery.data ?? []).map((p) => p.id)),
    [assignedQuery.data]
  );

  const addOptions = useMemo(
    () =>
      (packagesQuery.data ?? [])
        .filter((p) => !assignedIds.has(p.id))
        .map((p) => ({
          value: p.id,
          label: `${p.name} (${p.permissionCount})`,
        })),
    [packagesQuery.data, assignedIds]
  );

  const compositionKeys = useMemo(() => {
    const assigned = assignedQuery.data ?? [];
    const all = packagesQuery.data ?? [];
    const keys = new Set<string>();
    for (const a of assigned) {
      const full = all.find((p) => p.id === a.id);
      for (const key of full?.permissions ?? []) keys.add(key);
    }
    return Array.from(keys).sort();
  }, [assignedQuery.data, packagesQuery.data]);

  return (
    <div style={{ marginBottom: 16 }}>
      <Typography.Text strong>{t('users.roleDrawer.packagesSection')}</Typography.Text>
      <div style={{ marginTop: 8 }}>
        <Space wrap size={[4, 8]}>
          {(assignedQuery.data ?? []).map((pkg) => (
            <Tag
              key={pkg.id}
              closable={canEdit}
              onClose={(e) => {
                e.preventDefault();
                if (!canEdit) return;
                void removeMutation.mutateAsync(pkg.id);
              }}
            >
              {pkg.name} ({pkg.permissionCount})
            </Tag>
          ))}
          {(assignedQuery.data ?? []).length === 0 ? (
            <Typography.Text type="secondary">{t('users.roleDrawer.packagesEmpty')}</Typography.Text>
          ) : null}
        </Space>
      </div>
      {canEdit ? (
        <Space style={{ marginTop: 8 }} wrap>
          <Select
            style={{ minWidth: 220 }}
            placeholder={t('users.roleDrawer.packageSelectPlaceholder')}
            value={selectedPackageId}
            onChange={setSelectedPackageId}
            options={addOptions}
            allowClear
          />
          <Button
            type="default"
            disabled={!selectedPackageId}
            loading={addMutation.isPending}
            onClick={() => {
              if (selectedPackageId) void addMutation.mutateAsync(selectedPackageId);
            }}
          >
            {t('users.roleDrawer.packageAdd')}
          </Button>
        </Space>
      ) : null}
      {compositionKeys.length > 0 ? (
        <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}>
          {t('users.roleDrawer.packageComposition')}: {compositionKeys.slice(0, 24).join(', ')}
          {compositionKeys.length > 24 ? '…' : ''}
        </Typography.Paragraph>
      ) : null}
    </div>
  );
}
