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
