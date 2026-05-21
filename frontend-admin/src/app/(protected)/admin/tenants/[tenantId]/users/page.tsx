'use client';

import { useEffect } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Spin } from 'antd';

/** Legacy route — tenant users live on the detail page users tab. */
export default function SuperAdminTenantUsersRedirectPage() {
    const params = useParams();
    const router = useRouter();
    const tenantId = typeof params.tenantId === 'string' ? params.tenantId : '';

    useEffect(() => {
        if (tenantId) {
            router.replace(`/admin/tenants/${tenantId}?tab=users`);
        }
    }, [router, tenantId]);

    return <Spin fullscreen tip="…" />;
}
