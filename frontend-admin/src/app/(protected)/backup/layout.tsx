'use client';

import React from 'react';
import { BackupSecondaryNav } from '@/features/backup/components/BackupSecondaryNav';

export default function BackupAreaLayout({ children }: { children: React.ReactNode }) {
    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <BackupSecondaryNav />
            {children}
        </div>
    );
}
