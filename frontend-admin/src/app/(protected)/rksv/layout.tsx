'use client';

import React, { ReactNode } from 'react';
import { AdminOnlyGate } from '@/shared/auth/AdminOnlyGate';

export default function RksvLayout({ children }: { children: ReactNode }) {
    return <AdminOnlyGate>{children}</AdminOnlyGate>;
}
