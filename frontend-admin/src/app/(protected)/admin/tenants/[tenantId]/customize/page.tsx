'use client';

import { useEffect } from 'react';
import { useParams, useRouter } from 'next/navigation';

/** Canonical Super Admin entry: `/tenant/[id]/customize`. */
export default function AdminTenantCustomizeRedirectPage() {
  const params = useParams();
  const router = useRouter();
  const tenantId = typeof params.tenantId === 'string' ? params.tenantId : '';

  useEffect(() => {
    router.replace(tenantId ? `/tenant/${tenantId}/customize` : '/admin/tenants');
  }, [tenantId, router]);

  return null;
}
