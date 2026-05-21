import { AXIOS_INSTANCE } from '@/lib/axios';

export type AdminTenantCashRegister = {
    id: string;
    registerNumber: string;
    location: string;
    status: string;
    isActive: boolean;
    lastUsedAtUtc: string;
};

export async function listAdminTenantCashRegisters(tenantId: string): Promise<AdminTenantCashRegister[]> {
    const { data } = await AXIOS_INSTANCE.get<AdminTenantCashRegister[]>(
        `/api/admin/tenants/${tenantId}/cash-registers`,
    );
    return data;
}
