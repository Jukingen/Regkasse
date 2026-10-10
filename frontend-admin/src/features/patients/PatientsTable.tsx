'use client';

import { Button, Descriptions, Modal, Table } from 'antd';
import React, { useState } from 'react';

import { useI18n } from '@/i18n';

export type PatientRow = {
  id: string;
  customerName: string;
  petName: string;
  petSpecies: string;
  petBreed: string;
  petBirthDate: string;
  lastVisit: string;
  phone: string;
  email: string;
};

export function PatientsTable({ rows, loading }: { rows: PatientRow[]; loading?: boolean }) {
  const { t } = useI18n();
  const [open, setOpen] = useState<PatientRow | null>(null);

  return (
    <>
      <Table<PatientRow>
        rowKey="id"
        loading={loading}
        dataSource={rows}
        pagination={false}
        locale={{ emptyText: t('admin.patients.empty') }}
        columns={[
          { title: t('admin.patients.colCustomer'), dataIndex: 'customerName' },
          { title: t('admin.patients.colPet'), dataIndex: 'petName' },
          { title: t('admin.patients.colSpecies'), dataIndex: 'petSpecies' },
          { title: t('admin.patients.colLastVisit'), dataIndex: 'lastVisit' },
          {
            title: t('admin.patients.colContact'),
            render: (_, row) => [row.phone, row.email].filter(Boolean).join(' · '),
          },
          {
            title: t('admin.patients.details'),
            render: (_, row) => (
              <Button type="link" onClick={() => setOpen(row)}>
                {t('admin.patients.details')}
              </Button>
            ),
          },
        ]}
      />
      <Modal
        open={open != null}
        destroyOnHidden
        title={open?.petName || t('admin.patients.title')}
        onCancel={() => setOpen(null)}
        footer={null}
      >
        {open ? (
          <Descriptions column={1} size="small">
            <Descriptions.Item label={t('admin.patients.colCustomer')}>{open.customerName}</Descriptions.Item>
            <Descriptions.Item label={t('admin.patients.colPet')}>{open.petName}</Descriptions.Item>
            <Descriptions.Item label={t('admin.patients.colSpecies')}>{open.petSpecies}</Descriptions.Item>
            <Descriptions.Item label={t('admin.patients.breed')}>{open.petBreed}</Descriptions.Item>
            <Descriptions.Item label={t('admin.patients.birthDate')}>{open.petBirthDate}</Descriptions.Item>
            <Descriptions.Item label={t('admin.patients.colLastVisit')}>{open.lastVisit}</Descriptions.Item>
            <Descriptions.Item label={t('admin.patients.phone')}>{open.phone}</Descriptions.Item>
            <Descriptions.Item label={t('admin.patients.email')}>{open.email}</Descriptions.Item>
          </Descriptions>
        ) : null}
      </Modal>
    </>
  );
}
