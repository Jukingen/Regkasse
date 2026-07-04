'use client';

import React from 'react';
import { StaffSecondaryNav } from '@/features/staff/components/StaffSecondaryNav';

export default function StaffLayout({ children }: { children: React.ReactNode }) {
    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <StaffSecondaryNav />
            {children}
        </div>
    );
}
