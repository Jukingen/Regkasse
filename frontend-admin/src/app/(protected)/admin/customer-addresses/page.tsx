'use client';

import React, { useMemo } from 'react';

import { useGetApiCustomer } from '@/api/generated/customer/customer';
import type { Customer, CustomerAddressData } from '@/api/generated/model';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import {
  CustomerAddressesTable,
  type CustomerAddressRow,
} from '@/features/customer-addresses/CustomerAddressesTable';
import { extractListItems } from '@/features/customers/hooks/useCustomers';
import { useI18n } from '@/i18n';

type CustomerWithAddress = Customer & { addressData?: CustomerAddressData | null };

function text(value: string | null | undefined): string {
  return value?.trim() ? value : '—';
}

export function toAddressRows(customers: CustomerWithAddress[]): CustomerAddressRow[] {
  return customers.flatMap((customer) => {
    const address = customer.addressData;
    if (!address?.street?.trim() && !address?.postalCode?.trim() && !address?.city?.trim()) {
      return [];
    }
    return [
      {
        id: customer.id ?? customer.name,
        customerName: customer.name,
        street: text(address?.street),
        postalCode: text(address?.postalCode),
        city: text(address?.city),
        contact: [customer.phone, customer.email].filter((part) => part?.trim()).join(' · ') || '—',
      },
    ];
  });
}

export default function AdminCustomerAddressesPage() {
  const { t } = useI18n();
  const query = useGetApiCustomer(
    { pageNumber: 1, pageSize: 100 },
    { query: { select: (raw) => extractListItems(raw) as CustomerWithAddress[] } }
  );
  const rows = useMemo(() => toAddressRows(query.data ?? []), [query.data]);

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('admin.customerAddresses.title')}
        subtitle={t('admin.customerAddresses.subtitle')}
      />
      <CustomerAddressesTable rows={rows} loading={query.isLoading} />
    </AdminPageShell>
  );
}
