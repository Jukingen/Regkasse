'use client';

import React, { ReactNode } from 'react';
import { AuthGate } from '@/shared/auth/AuthGate';

/** Public auth shell for /login and nested entry routes (forgot-username). */
export default function LoginLayout({ children }: { children: ReactNode }) {
    return <AuthGate mode="public">{children}</AuthGate>;
}
