import {
  useGetApiAdminDailyClosingCalendar,
} from '@/api/generated/admin/admin';
import type {
  DailyClosingCalendarDto,
  DailyClosingDayDto,
} from '@/api/generated/model';

export type DailyClosingCalendarDay = DailyClosingDayDto;
export type DailyClosingCalendar = DailyClosingCalendarDto;

export function useDailyClosingCalendar(
  year: number,
  month: number,
  cashRegisterId: string | undefined,
  enabled: boolean
) {
  return useGetApiAdminDailyClosingCalendar(
    {
      year,
      month,
      cashRegisterId,
    },
    {
      query: {
        enabled,
        staleTime: 30_000,
      },
    }
  );
}
