import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { AppPermissions } from '@/shared/auth/permissions';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import { getApiRksvReminderStatusOverview } from '@/api/generated/rksv/rksv';
import type { RksvReminderRegisterStatusItemDto, RksvReminderStatusDto } from '@/api/generated/model';

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

function buildOverviewFromStatusItems(
  reminderItems: RksvReminderRegisterStatusItemDto[],
): RksvReminderOverview {
  const reminders = reminderItems
    .map((item) => {
      const registerId = item.cashRegisterId?.trim();
      if (!registerId) return null;

      const reminderStatus = item.status;
      return {
        registerId,
        registerNumber: registerId.slice(0, 8),
        hasStartbeleg: reminderStatus?.startbeleg?.status === 'present',
        monatsbelegStatus: toMonatsbelegStatus(reminderStatus),
        jahresbelegStatus: toJahresbelegStatus(reminderStatus),
        daysRemaining: getDaysRemaining(reminderStatus),
      };
    })
    .filter((item): item is NonNullable<typeof item> => item !== null);

  return {
    totalRegisters: reminders.length,
    missingStartbeleg: reminders.filter((item) => !item.hasStartbeleg).length,
    missingMonatsbeleg: reminders.filter((item) => item.monatsbelegStatus === 'missing').length,
    missingJahresbeleg: reminders.filter((item) => item.jahresbelegStatus === 'missing').length,
    overdueMonatsbeleg: reminders.filter((item) => item.monatsbelegStatus === 'overdue').length,
    lastUpdated: new Date().toISOString(),
    reminders,
  };
}

/** Single round-trip: backend overview already scopes active tenant registers. */
export const fetchRksvReminderOverview = async (): Promise<RksvReminderOverview> => {
  const reminderItems = await getApiRksvReminderStatusOverview();
  return buildOverviewFromStatusItems(reminderItems ?? []);
};

export const useRksvReminderOverview = () => {
  return useAuthorizedQuery({
    queryKey: rksvAdminQueryKeys.operations.reminderOverview,
    queryFn: fetchRksvReminderOverview,
    requiredPermission: AppPermissions.CashRegisterView,
    retry: false,
  });
};
