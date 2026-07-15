'use client';

import { Suspense } from 'react';
import { Spin } from 'antd';

import { LoginForm } from '@/features/auth/components/LoginForm';

export default function LoginPage() {
    return (
        <Suspense
            fallback={
                <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
                    <Spin size="large" />
                </div>
            }
        >
            <LoginForm />
        </Suspense>
    );
}
