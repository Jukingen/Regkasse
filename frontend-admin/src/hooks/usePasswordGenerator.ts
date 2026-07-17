'use client';

import { useCallback } from 'react';

import { generateCompliantPassword } from '@/features/super-admin/lib/generateCompliantPassword';

/**
 * Secure password generator for Super Admin onboarding / credential handoff.
 */
export function usePasswordGenerator() {
    const generatePassword = useCallback((length = 16) => generateCompliantPassword(length), []);

    return { generatePassword };
}
