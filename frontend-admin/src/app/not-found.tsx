'use client';

import { NotFoundAccessView } from '@/shared/auth/NotFoundAccessView';

/** Global fallback for routes outside the protected shell (login, static, etc.). */
export default function NotFound() {
    return (
        <div style={{ minHeight: '100vh', display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
            <NotFoundAccessView />
        </div>
    );
}
