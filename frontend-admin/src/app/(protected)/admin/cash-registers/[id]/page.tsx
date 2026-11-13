'use client';

import { useParams } from 'next/navigation';

import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { CashRegisterDetail } from '@/features/cash-registers/components/CashRegisterDetail';

export default function AdminCashRegisterDetailPage() {
  const params = useParams();
  const id = typeof params.id === 'string' ? params.id : '';

  return (
    <AdminPageShell>
      <CashRegisterDetail registerId={id} />
    </AdminPageShell>
  );
}
