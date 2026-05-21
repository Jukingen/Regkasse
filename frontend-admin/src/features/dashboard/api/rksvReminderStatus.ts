import { customInstance } from '@/lib/axios';
import type { RksvReminderStatusDto } from '@/api/generated/model';

export type RksvReminderRegisterStatusItemDto = {
    cashRegisterId: string;
    status: RksvReminderStatusDto;
};

/** Single request for dashboard RKSV reminder card (replaces per-register N+1). */
export async function getRksvReminderStatusOverview(): Promise<RksvReminderRegisterStatusItemDto[]> {
    return customInstance<RksvReminderRegisterStatusItemDto[]>({
        url: '/api/rksv/reminder/status-overview',
        method: 'GET',
    });
}
