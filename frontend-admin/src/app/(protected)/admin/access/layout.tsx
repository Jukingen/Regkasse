'use client';

import React from 'react';
import { AccessSecondaryNav } from '@/features/access/components/AccessSecondaryNav';

export default function AccessLayout({ children }: { children: React.ReactNode }) {
    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <AccessSecondaryNav />
            {children}
        </div>
    );
}
