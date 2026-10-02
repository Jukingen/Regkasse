'use client';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Form, Input, Modal, Select, Space, Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useEffect, useMemo, useState } from 'react';

import {
  useGetApiAdminPeppolParticipantsTenantId,
  usePostApiAdminPeppolParticipants,
} from '@/api/generated/admin/admin';
import type { PeppolParticipantDto, RegisterPeppolParticipantRequest } from '@/api/generated/model';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

type ParticipantForm = {
  participantId: string;
  apEnvironment: string;
  legalEntityId?: string;
  eIdentifierScheme?: string;
  eIdentifierValue?: string;
};

function stamp(value: string | null | undefined): string {
  return value ? value.slice(0, 19).replace('T', ' ') : '—';
}

function updatedAt(row: PeppolParticipantDto): string {
  const value = (row as PeppolParticipantDto & { updatedAtUtc?: string | null }).updatedAtUtc;
  return stamp(value);
}

export function PeppolParticipantsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<ParticipantForm>();
  const [tenantId, setTenantId] = useState<string | undefined>();
  const [picked, setPicked] = useState(false);
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<PeppolParticipantDto | null>(null);

  const tenants = useQuery({
    queryKey: ['admin', 'peppol', 'participants', 'tenants'],
    queryFn: () => listAdminTenants(false),
  });

  useEffect(() => {
    if (picked) return;
    const first = tenants.data?.[0]?.id;
    if (first) {
      setTenantId(first);
      setPicked(true);
    }
  }, [picked, tenants.data]);

  const list = useGetApiAdminPeppolParticipantsTenantId(tenantId ?? '', {
    query: { enabled: Boolean(tenantId) },
  });

  const tenantName = useMemo(() => {
    const map = new Map((tenants.data ?? []).map((tenant) => [tenant.id, tenant.name]));
    return (id: string | undefined) => (id && map.get(id)) || id || '—';
  }, [tenants.data]);

  const create = usePostApiAdminPeppolParticipants({
    mutation: {
      onSuccess: async () => {
        if (tenantId) {
          await queryClient.invalidateQueries({
            queryKey: [`/api/admin/peppol/participants/${tenantId}`],
          });
        }
        notify.success(t('admin.peppol.participants.saved'));
        setOpen(false);
        setEditing(null);
      },
      onError: (err) => {
        notify.apiError(err, {
          logContext: 'PeppolParticipants.save',
          fallbackKey: 'admin.peppol.participants.loadError',
        });
      },
    },
  });

  const openCreate = () => {
    setEditing(null);
    form.setFieldsValue({
      participantId: '',
      apEnvironment: 'TEST',
      legalEntityId: '',
      eIdentifierScheme: '',
      eIdentifierValue: '',
    });
    setOpen(true);
  };

  const openEdit = (row: PeppolParticipantDto) => {
    setEditing(row);
    form.setFieldsValue({
      participantId: row.participantId ?? '',
      apEnvironment: row.apEnvironment ?? 'TEST',
      legalEntityId: row.legalEntityId ?? '',
      eIdentifierScheme: row.eIdentifierScheme ?? '',
      eIdentifierValue: row.eIdentifierValue ?? '',
    });
    setOpen(true);
  };

  const submit = (values: ParticipantForm) => {
    if (!tenantId) return;
    const body: RegisterPeppolParticipantRequest = {
      tenantId,
      participantId: values.participantId.trim(),
      apEnvironment: values.apEnvironment,
      legalEntityId: values.legalEntityId?.trim() || null,
      eIdentifierScheme: values.eIdentifierScheme?.trim() || null,
      eIdentifierValue: values.eIdentifierValue?.trim() || null,
    };
    create.mutate({ data: body });
  };

  const columns: ColumnsType<PeppolParticipantDto> = [
    {
      title: t('admin.peppol.participants.colTenant'),
      dataIndex: 'tenantId',
      render: (value: string | undefined) => tenantName(value),
    },
    {
      title: t('admin.peppol.participants.colParticipantId'),
      dataIndex: 'participantId',
      render: (value: string | null | undefined) => value || '—',
    },
    {
      title: t('admin.peppol.participants.colApEnvironment'),
      dataIndex: 'apEnvironment',
      render: (value: string | null | undefined) => value || '—',
    },
    {
      title: t('admin.peppol.participants.colLegalEntityId'),
      dataIndex: 'legalEntityId',
      render: (value: string | null | undefined) => value || '—',
    },
    {
      title: t('admin.peppol.participants.colEIdentifierScheme'),
      dataIndex: 'eIdentifierScheme',
      render: (value: string | null | undefined) => value || '—',
    },
    {
      title: t('admin.peppol.participants.colEIdentifierValue'),
      dataIndex: 'eIdentifierValue',
      render: (value: string | null | undefined) => value || '—',
    },
    {
      title: t('admin.peppol.participants.colUpdatedAtUtc'),
      key: 'updatedAtUtc',
      render: (_value, row) => updatedAt(row),
    },
    {
      title: t('admin.peppol.participants.edit'),
      key: 'edit',
      render: (_value, row) => (
        <Button type="link" onClick={() => openEdit(row)}>
          {t('admin.peppol.participants.edit')}
        </Button>
      ),
    },
  ];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <AdminPageHeader
        title={t('admin.peppol.participants.title')}
        subtitle={t('admin.peppol.participants.subtitle')}
      />
      {list.isError || tenants.isError ? (
        <Alert type="error" showIcon title={t('admin.peppol.participants.loadError')} />
      ) : null}
      <Space wrap>
        <Select
          aria-label={t('admin.peppol.participants.tenantFilter')}
          placeholder={t('admin.peppol.participants.tenantFilter')}
          style={{ minWidth: 280 }}
          loading={tenants.isLoading}
          value={tenantId}
          options={(tenants.data ?? []).map((tenant) => ({
            value: tenant.id,
            label: `${tenant.name} (${tenant.slug})`,
          }))}
          onChange={(value: string) => {
            setPicked(true);
            setTenantId(value);
          }}
        />
        <Button type="primary" disabled={!tenantId} onClick={openCreate}>
          {t('admin.peppol.participants.create')}
        </Button>
      </Space>
      {!tenantId ? <span>{t('admin.peppol.participants.missingTenant')}</span> : null}
      <Table<PeppolParticipantDto>
        rowKey={(row) => row.id ?? `${row.participantId ?? ''}-${row.apEnvironment ?? ''}`}
        columns={columns}
        dataSource={list.data ?? []}
        loading={list.isLoading}
        locale={{ emptyText: t('admin.peppol.participants.empty') }}
        pagination={false}
      />
      <Modal
        title={
          editing ? t('admin.peppol.participants.edit') : t('admin.peppol.participants.create')
        }
        open={open}
        destroyOnHidden
        onCancel={() => {
          setOpen(false);
          setEditing(null);
        }}
        okText={t('admin.peppol.participants.save')}
        cancelText={t('common.buttons.cancel')}
        confirmLoading={create.isPending}
        onOk={() => form.submit()}
      >
        <Form form={form} layout="vertical" onFinish={submit}>
          <Form.Item
            name="participantId"
            label={t('admin.peppol.participants.fieldParticipantId')}
            rules={[{ required: true }]}
          >
            <Input />
          </Form.Item>
          <Form.Item
            name="apEnvironment"
            label={t('admin.peppol.participants.fieldApEnvironment')}
            rules={[{ required: true }]}
          >
            <Select
              options={[
                { value: 'TEST', label: 'TEST' },
                { value: 'LIVE', label: 'LIVE' },
              ]}
            />
          </Form.Item>
          <Form.Item name="legalEntityId" label={t('admin.peppol.participants.fieldLegalEntityId')}>
            <Input />
          </Form.Item>
          <Form.Item
            name="eIdentifierScheme"
            label={t('admin.peppol.participants.fieldEIdentifierScheme')}
          >
            <Input />
          </Form.Item>
          <Form.Item
            name="eIdentifierValue"
            label={t('admin.peppol.participants.fieldEIdentifierValue')}
          >
            <Input />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
