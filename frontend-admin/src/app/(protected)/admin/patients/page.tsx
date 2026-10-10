'use client';

import { Typography } from 'antd';
import React, { useMemo } from 'react';

import { useGetApiCustomer } from '@/api/generated/customer/customer';
import type { Customer } from '@/api/generated/model';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { extractListItems } from '@/features/customers/hooks/useCustomers';
import { PatientsTable, type PatientRow } from '@/features/patients/PatientsTable';
import { useI18n } from '@/i18n';

function text(value: string | null | undefined): string {
  return value?.trim() ? value : '—';
}

export function toPatientRows(customers: Customer[]): PatientRow[] {
  return customers.flatMap((customer) => {
    const pet = customer.petData;
    if (!pet?.petName?.trim() && !pet?.petSpecies?.trim()) return [];
    return [
      {
        id: customer.id ?? customer.name,
        customerName: customer.name,
        petName: text(pet?.petName),
        petSpecies: text(pet?.petSpecies),
        petBreed: text(pet?.petBreed),
        petBirthDate: text(pet?.petBirthDate),
        lastVisit: text(customer.lastVisit),
        phone: text(customer.phone),
        email: text(customer.email),
      },
    ];
  });
}

export default function AdminPatientsPage() {
  const { t } = useI18n();
  const query = useGetApiCustomer(
    { pageNumber: 1, pageSize: 100 },
    { query: { select: (raw) => extractListItems(raw) } }
  );
  const rows = useMemo(() => toPatientRows(query.data ?? []), [query.data]);

  return (
    <AdminPageShell>
      <AdminPageHeader title={t('admin.patients.title')} subtitle={t('admin.patients.subtitle')} />
      <Typography.Paragraph type="secondary">{t('admin.patients.sourceNote')}</Typography.Paragraph>
      <PatientsTable rows={rows} loading={query.isLoading} />
    </AdminPageShell>
  );
}
