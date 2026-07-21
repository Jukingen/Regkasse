'use client';

import { useParams, useRouter } from 'next/navigation';
import { useEffect } from 'react';

/** Canonical Super Admin entry also lives under tenants; keep `/tenant/[id]/digital` as primary. */
export default function AdminTenantDigitalRedirectPage() {
  const params = useParams();
  const router = useRouter();
  const tenantId = typeof params.tenantId === 'string' ? params.tenantId : '';

  useEffect(() => {
    router.replace(tenantId ? `/tenant/${tenantId}/digital` : '/admin/tenants');
  }, [tenantId, router]);

  return null;
}
