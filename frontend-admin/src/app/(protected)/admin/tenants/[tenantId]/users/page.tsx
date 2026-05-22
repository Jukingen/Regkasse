'use client';

import { useEffect } from 'react';
import { useParams, useRouter } from 'next/navigation';

import { ADMIN_USERS_PAGE_PATH, buildAdminUsersPageHref } from '@/features/users/utils/adminUsersPageUrl';

/** Legacy route — redirects to centralized user management. */
export default function TenantUsersRedirectPage() {
    const params = useParams();
    const router = useRouter();
    const tenantId = typeof params.tenantId === 'string' ? params.tenantId : '';

    useEffect(() => {
        router.replace(tenantId ? buildAdminUsersPageHref(tenantId) : ADMIN_USERS_PAGE_PATH);
    }, [tenantId, router]);

    return null;
}
