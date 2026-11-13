import { customInstance } from '@/lib/axios';

export type WorkingHoursDay = {
  openTime: string;
  closeTime: string;
  isClosed: boolean;
};

export type WorkingHoursSpecialDay = {
  date: string;
  isClosed: boolean;
  openTime?: string | null;
  closeTime?: string | null;
};

export type WorkingHoursSettings = {
  reminderHoursBeforeClosing: number;
  stopOnlineOrdersMinutesBeforeClose: number;
  autoClosePOSAtClosing: boolean;
  closedDayMessage: string;
  specialDays: WorkingHoursSpecialDay[];
  monday: WorkingHoursDay;
  tuesday: WorkingHoursDay;
  wednesday: WorkingHoursDay;
  thursday: WorkingHoursDay;
  friday: WorkingHoursDay;
  saturday: WorkingHoursDay;
  sunday: WorkingHoursDay;
};

export type WorkingHoursDayKey = Exclude<
  keyof WorkingHoursSettings,
  | 'reminderHoursBeforeClosing'
  | 'stopOnlineOrdersMinutesBeforeClose'
  | 'autoClosePOSAtClosing'
  | 'closedDayMessage'
  | 'specialDays'
>;

export const WORKING_HOURS_DAY_KEYS: WorkingHoursDayKey[] = [
  'monday',
  'tuesday',
  'wednesday',
  'thursday',
  'friday',
  'saturday',
  'sunday',
];

const DEFAULT_OPEN_DAY: WorkingHoursDay = {
  openTime: '09:00',
  closeTime: '22:00',
  isClosed: false,
};

export function createDefaultWorkingHours(): WorkingHoursSettings {
  return {
    reminderHoursBeforeClosing: 1,
    stopOnlineOrdersMinutesBeforeClose: 30,
    autoClosePOSAtClosing: false,
    closedDayMessage: 'Heute geschlossen',
    specialDays: [],
    monday: { ...DEFAULT_OPEN_DAY },
    tuesday: { ...DEFAULT_OPEN_DAY },
    wednesday: { ...DEFAULT_OPEN_DAY },
    thursday: { ...DEFAULT_OPEN_DAY },
    friday: { ...DEFAULT_OPEN_DAY },
    saturday: { ...DEFAULT_OPEN_DAY },
    sunday: { ...DEFAULT_OPEN_DAY },
  };
}

type ApiDay = {
  openTime?: string;
  closeTime?: string;
  isClosed?: boolean;
  OpenTime?: string;
  CloseTime?: string;
  IsClosed?: boolean;
};

type ApiSpecialDay = {
  date?: string;
  Date?: string;
  isClosed?: boolean;
  IsClosed?: boolean;
  openTime?: string | null;
  OpenTime?: string | null;
  closeTime?: string | null;
  CloseTime?: string | null;
};

type ApiDto = {
  reminderHoursBeforeClosing?: number;
  ReminderHoursBeforeClosing?: number;
  stopOnlineOrdersMinutesBeforeClose?: number;
  StopOnlineOrdersMinutesBeforeClose?: number;
  autoClosePOSAtClosing?: boolean;
  AutoClosePOSAtClosing?: boolean;
  closedDayMessage?: string;
  ClosedDayMessage?: string;
  specialDays?: ApiSpecialDay[];
  SpecialDays?: ApiSpecialDay[];
  monday?: ApiDay;
  tuesday?: ApiDay;
  wednesday?: ApiDay;
  thursday?: ApiDay;
  friday?: ApiDay;
  saturday?: ApiDay;
  sunday?: ApiDay;
  Monday?: ApiDay;
  Tuesday?: ApiDay;
  Wednesday?: ApiDay;
  Thursday?: ApiDay;
  Friday?: ApiDay;
  Saturday?: ApiDay;
  Sunday?: ApiDay;
};

function mapDay(day: ApiDay | undefined): WorkingHoursDay {
  return {
    openTime: day?.openTime ?? day?.OpenTime ?? DEFAULT_OPEN_DAY.openTime,
    closeTime: day?.closeTime ?? day?.CloseTime ?? DEFAULT_OPEN_DAY.closeTime,
    isClosed: day?.isClosed ?? day?.IsClosed ?? false,
  };
}

function mapSpecialDay(day: ApiSpecialDay): WorkingHoursSpecialDay {
  return {
    date: day.date ?? day.Date ?? '',
    isClosed: day.isClosed ?? day.IsClosed ?? false,
    openTime: day.openTime ?? day.OpenTime ?? null,
    closeTime: day.closeTime ?? day.CloseTime ?? null,
  };
}

function mapFromApi(dto: ApiDto | null | undefined): WorkingHoursSettings {
  if (!dto) return createDefaultWorkingHours();
  const specialRaw = dto.specialDays ?? dto.SpecialDays ?? [];
  return {
    reminderHoursBeforeClosing:
      dto.reminderHoursBeforeClosing ?? dto.ReminderHoursBeforeClosing ?? 1,
    stopOnlineOrdersMinutesBeforeClose:
      dto.stopOnlineOrdersMinutesBeforeClose ?? dto.StopOnlineOrdersMinutesBeforeClose ?? 30,
    autoClosePOSAtClosing: dto.autoClosePOSAtClosing ?? dto.AutoClosePOSAtClosing ?? false,
    closedDayMessage: dto.closedDayMessage ?? dto.ClosedDayMessage ?? 'Heute geschlossen',
    specialDays: specialRaw.map(mapSpecialDay).filter((d) => d.date),
    monday: mapDay(dto.monday ?? dto.Monday),
    tuesday: mapDay(dto.tuesday ?? dto.Tuesday),
    wednesday: mapDay(dto.wednesday ?? dto.Wednesday),
    thursday: mapDay(dto.thursday ?? dto.Thursday),
    friday: mapDay(dto.friday ?? dto.Friday),
    saturday: mapDay(dto.saturday ?? dto.Saturday),
    sunday: mapDay(dto.sunday ?? dto.Sunday),
  };
}

export async function fetchWorkingHours(): Promise<WorkingHoursSettings> {
  const res = await customInstance<ApiDto>({
    url: '/api/settings/working-hours',
    method: 'GET',
  });
  return mapFromApi(res);
}

export async function updateWorkingHours(
  payload: WorkingHoursSettings
): Promise<WorkingHoursSettings> {
  const res = await customInstance<ApiDto>({
    url: '/api/settings/working-hours',
    method: 'PUT',
    data: payload,
  });
  return mapFromApi(res);
}
