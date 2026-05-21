import { AXIOS_INSTANCE } from '@/lib/axios';

export const QUICK_USER_ROLES = ['Manager', 'Cashier', 'Accountant'] as const;

export type QuickUserRole = (typeof QUICK_USER_ROLES)[number];

export type CreateQuickUserRequest = {
    role: QuickUserRole | string;
};

export type CreateQuickUserResult = {
    userId: string;
    email: string;
    generatedPassword: string;
    forcePasswordChangeOnNextLogin: boolean;
    success: boolean;
    tenantPortalUrl?: string | null;
    role?: string | null;
};

export async function createQuickUser(
    tenantId: string,
    body: CreateQuickUserRequest,
): Promise<CreateQuickUserResult> {
    const { data } = await AXIOS_INSTANCE.post<CreateQuickUserResult>(
        `/api/admin/tenants/${tenantId}/users/quick`,
        body,
    );
    return data;
}
