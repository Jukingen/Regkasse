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
  allergies?: string;
  chronicConditions?: string;
  insurance?: string;
  lastVisit: string;
  phone: string;
  email: string;
};

export function PatientsTable({
  rows,
  loading,
  kind = 'vet',
}: {
  rows: PatientRow[];
  loading?: boolean;
  kind?: 'vet' | 'doctor';
}) {
  const { t } = useI18n();
  const [open, setOpen] = useState<PatientRow | null>(null);
  const doctor = kind === 'doctor';

  return (
    <>
      <Table<PatientRow>
        rowKey="id"
        loading={loading}
        dataSource={rows}
        pagination={false}
        locale={{ emptyText: t('admin.patients.empty') }}
        columns={[
          {
            title: t(doctor ? 'admin.patients.colPatient' : 'admin.patients.colCustomer'),
            dataIndex: 'customerName',
          },
          ...(doctor
            ? [
                { title: t('admin.patients.birthDate'), dataIndex: 'petBirthDate' as const },
                { title: t('admin.patients.colAllergies'), dataIndex: 'allergies' as const },
                { title: t('admin.patients.colInsurance'), dataIndex: 'insurance' as const },
              ]
            : [
                { title: t('admin.patients.colPet'), dataIndex: 'petName' as const },
                { title: t('admin.patients.colSpecies'), dataIndex: 'petSpecies' as const },
              ]),
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
        title={open?.customerName || t(doctor ? 'admin.patients.doctorTitle' : 'admin.patients.title')}
        onCancel={() => setOpen(null)}
        footer={null}
      >
        {open ? (
          <Descriptions column={1} size="small">
            <Descriptions.Item label={t(doctor ? 'admin.patients.colPatient' : 'admin.patients.colCustomer')}>
              {open.customerName}
            </Descriptions.Item>
            {doctor ? (
              <>
                <Descriptions.Item label={t('admin.patients.birthDate')}>{open.petBirthDate}</Descriptions.Item>
                <Descriptions.Item label={t('admin.patients.colAllergies')}>{open.allergies}</Descriptions.Item>
                <Descriptions.Item label={t('admin.patients.chronicConditions')}>
                  {open.chronicConditions}
                </Descriptions.Item>
                <Descriptions.Item label={t('admin.patients.colInsurance')}>{open.insurance}</Descriptions.Item>
              </>
            ) : (
              <>
                <Descriptions.Item label={t('admin.patients.colPet')}>{open.petName}</Descriptions.Item>
                <Descriptions.Item label={t('admin.patients.colSpecies')}>{open.petSpecies}</Descriptions.Item>
                <Descriptions.Item label={t('admin.patients.breed')}>{open.petBreed}</Descriptions.Item>
                <Descriptions.Item label={t('admin.patients.birthDate')}>{open.petBirthDate}</Descriptions.Item>
              </>
            )}
            <Descriptions.Item label={t('admin.patients.colLastVisit')}>{open.lastVisit}</Descriptions.Item>
            <Descriptions.Item label={t('admin.patients.phone')}>{open.phone}</Descriptions.Item>
            <Descriptions.Item label={t('admin.patients.email')}>{open.email}</Descriptions.Item>
          </Descriptions>
        ) : null}
      </Modal>
    </>
  );
}
