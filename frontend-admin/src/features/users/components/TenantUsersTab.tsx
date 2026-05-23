'use client';

import { TenantUsersTabCore } from '@/features/users/components/TenantUsersTabCore';
import type { UsersPolicy } from '@/shared/auth/usersPolicy';

export type TenantUsersTabProps = {
    policy: UsersPolicy;
    roleDisplayLabel: (role: string) => string;
    onEdit: (userId: string) => void;
};

/** Mandant users on `/users` — delegates to shared core (ambient tenant APIs). */
export function TenantUsersTab({ policy, roleDisplayLabel, onEdit }: TenantUsersTabProps) {
    return (
        <TenantUsersTabCore policy={policy} roleDisplayLabel={roleDisplayLabel} onEdit={onEdit} />
    );
}
