'use client';

import React, { ReactNode } from 'react';
import { AuthGate } from '@/shared/auth/AuthGate';

export default function PublicLayout({
    children,
}: {
    children: ReactNode;
}) {
    return (
        <AuthGate mode="public">
            {children}
        </AuthGate>
    );
}
