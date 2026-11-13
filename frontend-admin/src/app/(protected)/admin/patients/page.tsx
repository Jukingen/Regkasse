'use client';

import { Typography } from 'antd';
import React, { useMemo } from 'react';

import { useAmbientVerticalProfile } from '@/api/admin/vertical-profile';
import { useGetApiCustomer } from '@/api/generated/customer/customer';
import type { Customer, CustomerPetData } from '@/api/generated/model';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { extractListItems } from '@/features/customers/hooks/useCustomers';
import { PatientsTable, type PatientRow } from '@/features/patients/PatientsTable';
import { useI18n } from '@/i18n';

function text(value: string | null | undefined): string {
  return value?.trim() ? value : '—';
}

function hasClinicalRecord(pet: CustomerPetData | null | undefined): boolean {
  return Boolean(
    pet?.petName?.trim()
      || pet?.petSpecies?.trim()
      || pet?.species?.trim()
      || pet?.allergies?.trim()
      || pet?.chronicConditions?.trim()
      || pet?.insurance?.trim()
      || pet?.birthDate?.trim()
      || pet?.vaccines?.trim()
  );
}

function toPatientRows(customers: Customer[]): PatientRow[] {
  return customers.flatMap((customer) => {
    const pet = customer.petData;
    if (!hasClinicalRecord(pet)) return [];
    return [
      {
        id: customer.id ?? customer.name,
        customerName: customer.name,
        petName: text(pet?.petName),
        petSpecies: text(pet?.species ?? pet?.petSpecies),
        petBreed: text(pet?.breed ?? pet?.petBreed),
        petBirthDate: text(pet?.birthDate ?? pet?.petBirthDate),
        allergies: text(pet?.allergies),
        chronicConditions: text(pet?.chronicConditions),
        insurance: text(pet?.insurance),
        lastVisit: text(customer.lastVisit),
        phone: text(customer.phone),
        email: text(customer.email),
      },
    ];
  });
}

export default function AdminPatientsPage() {
  const { t } = useI18n();
  const profile = useAmbientVerticalProfile();
  const kind = profile.data?.profileId === 'doctor' ? 'doctor' : 'vet';
  const query = useGetApiCustomer(
    { pageNumber: 1, pageSize: 100 },
    { query: { select: (raw) => extractListItems(raw) } }
  );
  const rows = useMemo(() => toPatientRows(query.data ?? []), [query.data]);

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t(kind === 'doctor' ? 'admin.patients.doctorTitle' : 'admin.patients.title')}
        subtitle={t(kind === 'doctor' ? 'admin.patients.doctorSubtitle' : 'admin.patients.subtitle')}
      />
      <Typography.Paragraph type="secondary">
        {t(kind === 'doctor' ? 'admin.patients.doctorSourceNote' : 'admin.patients.sourceNote')}
      </Typography.Paragraph>
      <PatientsTable rows={rows} loading={query.isLoading} kind={kind} />
    </AdminPageShell>
  );
}
