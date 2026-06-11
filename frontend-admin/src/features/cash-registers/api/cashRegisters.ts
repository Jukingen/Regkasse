import type { CashRegister } from '@/api/generated/model';
import type {
    CashRegisterDeviceInfo,
    CashRegisterTseHealthResponse,
    TseHealthStatus,
} from '@/features/cash-registers/types/enhancedCashRegister';
import { customInstance } from '@/lib/axios';

export type CashRegisterCapabilities = {
    allowHardDelete: boolean;
    decommissionViaSchlussbeleg: boolean;
};

export type DecommissionCashRegisterRequest = {
    reason?: string | null;
};

export type DecommissionCashRegisterResponse = {
    cashRegisterId: string;
    paymentId: string;
    receiptId: string;
    receiptNumber: string;
    message: string;
};

export type HardDeleteCashRegisterRequest = {
    confirmPhrase: string;
};

export type CreateCashRegisterRequest = {
    registerNumber: string;
    location: string;
    tenantId?: string | null;
};

export type CreateCashRegisterResponse = {
    message: string;
    register: CashRegister;
};

export const cashRegisterListQueryKey = ['cash-registers'] as const;

/** Admin FA projection from `GET /api/admin/cash-registers`. */
export type AdminCashRegisterListItem = {
    id: string;
    tenantId: string;
    tenantName?: string | null;
    tenantSlug?: string | null;
    registerNumber: string;
    location: string;
    status: number;
    startingBalance?: number;
    currentBalance?: number;
    lastBalanceUpdate?: string;
    currentUserId?: string | null;
    currentCashierName?: string | null;
    isActive?: boolean;
    isDefaultForTenant?: boolean;
    decommissionedAtUtc?: string | null;
    decommissionReason?: string | null;
    createdAt?: string;
    createdBy?: string | null;
    updatedAt?: string | null;
    updatedBy?: string | null;
    lastMonatsbelegUtc?: string | null;
    lastJahresbelegUtc?: string | null;
    tseHealthStatus?: TseHealthStatus | string | null;
    offlineQueueCount?: number;
    lastSyncAtUtc?: string | null;
    deviceInfo?: CashRegisterDeviceInfo | null;
};

export type AdminCashRegisterPagedResult = {
    items: AdminCashRegisterListItem[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
};

export type ListAdminCashRegistersParams = {
    tenantId?: string;
    excludeStatus?: string;
    page?: number;
    pageSize?: number;
};

export const adminCashRegisterListQueryKey = (params?: ListAdminCashRegistersParams) =>
    [
        'admin',
        'cash-registers',
        'list',
        params?.tenantId ?? '__all__',
        params?.excludeStatus ?? '__none__',
        params?.pageSize ?? 100,
    ] as const;

export const cashRegisterByTenantQueryKey = (tenantId?: string) =>
    ['cash-registers', 'by-tenant', tenantId ?? '__none__'] as const;

export async function listCashRegistersByTenant(): Promise<AdminCashRegisterListItem[]> {
    const data = await customInstance<AdminCashRegisterListItem[]>({
        url: '/api/admin/cash-registers/by-tenant',
        method: 'GET',
    });
    return data ?? [];
}

export async function listAdminCashRegisters(
    params?: ListAdminCashRegistersParams,
): Promise<AdminCashRegisterPagedResult> {
    const data = await customInstance<{
        items?: AdminCashRegisterListItem[];
        totalCount?: number;
        page?: number;
        pageSize?: number;
        totalPages?: number;
    }>({
        url: '/api/admin/cash-registers',
        method: 'GET',
        params: {
            tenantId: params?.tenantId,
            excludeStatus: params?.excludeStatus,
            page: params?.page ?? 1,
            pageSize: params?.pageSize ?? 100,
        },
    });

    return {
        items: data.items ?? [],
        totalCount: data.totalCount ?? 0,
        page: data.page ?? 1,
        pageSize: data.pageSize ?? 100,
        totalPages: data.totalPages ?? 0,
    };
}

export async function createCashRegister(
    body: CreateCashRegisterRequest,
): Promise<CreateCashRegisterResponse> {
    return customInstance<CreateCashRegisterResponse>({
        url: '/api/admin/cash-registers',
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        data: body,
    });
}

export async function getCashRegisterCapabilities(): Promise<CashRegisterCapabilities> {
    const data = await customInstance<{
        allowHardDelete?: boolean;
        decommissionViaSchlussbeleg?: boolean;
    }>({
        url: '/api/admin/cash-registers/capabilities',
        method: 'GET',
    });
    return {
        allowHardDelete: data.allowHardDelete === true,
        decommissionViaSchlussbeleg: data.decommissionViaSchlussbeleg !== false,
    };
}

/** RKSV Schlussbeleg + Decommissioned status (audit logged server-side). */
export async function decommissionCashRegister(
    id: string,
    body: DecommissionCashRegisterRequest,
): Promise<DecommissionCashRegisterResponse> {
    return customInstance<DecommissionCashRegisterResponse>({
        url: `/api/admin/cash-registers/${id}/decommission`,
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        data: body,
    });
}

export async function hardDeleteCashRegister(
    id: string,
    body: HardDeleteCashRegisterRequest,
): Promise<void> {
    await customInstance<void>({
        url: `/api/admin/cash-registers/${id}`,
        method: 'DELETE',
        headers: { 'Content-Type': 'application/json' },
        data: body,
    });
}

export async function getCashRegisterTseHealth(id: string): Promise<CashRegisterTseHealthResponse> {
    return customInstance<CashRegisterTseHealthResponse>({
        url: `/api/admin/cash-registers/${id}/tse-health`,
        method: 'GET',
    });
}
