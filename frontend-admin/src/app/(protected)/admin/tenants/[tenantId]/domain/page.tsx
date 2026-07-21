'use client';

import { useParams, useRouter } from 'next/navigation';
import { useEffect } from 'react';

/** Canonical Super Admin entry: `/tenant/[id]/domain`. */
export default function AdminTenantDomainRedirectPage() {
  const params = useParams();
  const router = useRouter();
  const tenantId = typeof params.tenantId === 'string' ? params.tenantId : '';

  useEffect(() => {
    router.replace(tenantId ? `/tenant/${tenantId}/domain` : '/admin/tenants');
  }, [tenantId, router]);

  return null;
}
