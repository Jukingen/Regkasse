import { useQuery } from '@tanstack/react-query';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import type { CashRegister, RksvReminderRegisterStatusItemDto, RksvReminderStatusDto } from '@/api/generated/model';
import { customInstance } from '@/lib/axios';

export interface RksvReminderOverview {
  totalRegisters: number;
  missingStartbeleg: number;
  missingMonatsbeleg: number;
  missingJahresbeleg: number;
  overdueMonatsbeleg: number;
  lastUpdated: string;
  reminders: Array<{
    registerId: string;
    registerNumber: string;
    hasStartbeleg: boolean;
    monatsbelegStatus: 'ok' | 'missing' | 'overdue';
    jahresbelegStatus: 'ok' | 'missing' | 'overdue' | 'not_applicable';
    daysRemaining?: number;
    daysOverdue?: number;
  }>;
}

function normalizeRegisterRows(data: unknown): CashRegister[] {
  if (Array.isArray(data)) return data as CashRegister[];
  if (data && typeof data === 'object' && 'registers' in data) {
    const registers = (data as { registers?: CashRegister[] }).registers;
    if (Array.isArray(registers)) return registers;
  }
  return [];
}

function isDecommissioned(register: CashRegister): boolean {
  return register.status === 5;
}

function toMonatsbelegStatus(status: RksvReminderStatusDto | undefined): 'ok' | 'missing' | 'overdue' {
  const monatsbeleg = status?.monatsbeleg;
  if (!monatsbeleg) return 'missing';
  if (monatsbeleg.status === 'overdue') return 'overdue';
  if (monatsbeleg.isRequired || monatsbeleg.status === 'upcoming') return 'missing';
  return 'ok';
}

function toJahresbelegStatus(
  status: RksvReminderStatusDto | undefined,
): 'ok' | 'missing' | 'overdue' | 'not_applicable' {
  const jahresbeleg = status?.jahresbeleg;
  if (!jahresbeleg) return 'not_applicable';
  if (!jahresbeleg.isRequired) return 'not_applicable';
  if (jahresbeleg.status === 'overdue') return 'overdue';
  if (jahresbeleg.status === 'upcoming') return 'missing';
  return 'ok';
}

function getDaysRemaining(status: RksvReminderStatusDto | undefined): number | undefined {
  const jahresbeleg = status?.jahresbeleg;
  if (jahresbeleg?.isRequired && jahresbeleg.status !== 'overdue' && jahresbeleg.daysUntilDeadline != null) {
    return jahresbeleg.daysUntilDeadline;
  }

  const monatsbeleg = status?.monatsbeleg;
  if (monatsbeleg?.isRequired && monatsbeleg.status !== 'overdue' && monatsbeleg.daysUntilDeadline != null) {
    return monatsbeleg.daysUntilDeadline;
  }

  return undefined;
}

export const fetchRksvReminderOverview = async (): Promise<RksvReminderOverview | null> => {
  try {
    const [registerPayload, reminderItems] = await Promise.all([
      customInstance<{ registers?: CashRegister[] } | CashRegister[]>({
        url: '/api/CashRegister',
        method: 'GET',
      }),
      customInstance<RksvReminderRegisterStatusItemDto[]>({
        url: '/api/rksv/reminder/status-overview',
        method: 'GET',
      }),
    ]);

    const registerMap = new Map<string, CashRegister>();
    for (const register of normalizeRegisterRows(registerPayload)) {
      const registerId = register.id?.trim();
      if (!registerId || isDecommissioned(register)) continue;
      registerMap.set(registerId, register);
    }

    const reminders = reminderItems
      .map((item) => {
        const registerId = item.cashRegisterId?.trim();
        if (!registerId) return null;

        const register = registerMap.get(registerId);
        if (!register) return null;

        const reminderStatus = item.status;
        return {
          registerId,
          registerNumber: register.registerNumber?.trim() || registerId.slice(0, 8),
          hasStartbeleg: reminderStatus?.startbeleg?.status === 'present',
          monatsbelegStatus: toMonatsbelegStatus(reminderStatus),
          jahresbelegStatus: toJahresbelegStatus(reminderStatus),
          daysRemaining: getDaysRemaining(reminderStatus),
        };
      })
      .filter((item): item is RksvReminderOverview['reminders'][number] => item !== null);

    return {
      totalRegisters: registerMap.size,
      missingStartbeleg: reminders.filter((item) => !item.hasStartbeleg).length,
      missingMonatsbeleg: reminders.filter((item) => item.monatsbelegStatus === 'missing').length,
      missingJahresbeleg: reminders.filter((item) => item.jahresbelegStatus === 'missing').length,
      overdueMonatsbeleg: reminders.filter((item) => item.monatsbelegStatus === 'overdue').length,
      lastUpdated: new Date().toISOString(),
      reminders,
    };
  } catch (error) {
    console.error('Failed to fetch RKSV reminder overview:', error);
    return null;
  }
};

export const useRksvReminderOverview = () => {
  return useQuery({
    queryKey: rksvAdminQueryKeys.operations.reminderOverview,
    queryFn: fetchRksvReminderOverview,
    retry: 1,
    refetchInterval: 300000,
    refetchOnWindowFocus: true,
    staleTime: 120000,
  });
};
