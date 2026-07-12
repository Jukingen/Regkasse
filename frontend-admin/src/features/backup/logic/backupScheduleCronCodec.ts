import type {
  BackupScheduleConfigurationDto,
  BackupScheduleFrequency,
} from '@/api/generated/model';
import { BackupScheduleFrequency as BackupScheduleFrequencyEnum } from '@/api/generated/model';
import {
  BACKUP_SCHEDULE_PRESET_CRONS,
  normalizeCronWhitespace,
  isPlausibleStandardCron,
} from '@/features/backup-dr/logic/backupScheduleSettingsApi';

export type { BackupScheduleFrequency };

/** UI / planner frequency labels (mapped to API numeric enum at boundaries). */
export type BackupScheduleFrequencyKey = 'Daily' | 'Weekly' | 'Monthly' | 'Custom';

const FREQUENCY_TO_API: Record<BackupScheduleFrequencyKey, BackupScheduleFrequency> = {
  Daily: BackupScheduleFrequencyEnum.NUMBER_0,
  Weekly: BackupScheduleFrequencyEnum.NUMBER_1,
  Monthly: BackupScheduleFrequencyEnum.NUMBER_2,
  Custom: BackupScheduleFrequencyEnum.NUMBER_3,
};

const FREQUENCY_FROM_API: Record<BackupScheduleFrequency, BackupScheduleFrequencyKey> = {
  [BackupScheduleFrequencyEnum.NUMBER_0]: 'Daily',
  [BackupScheduleFrequencyEnum.NUMBER_1]: 'Weekly',
  [BackupScheduleFrequencyEnum.NUMBER_2]: 'Monthly',
  [BackupScheduleFrequencyEnum.NUMBER_3]: 'Custom',
};

/** Planner/PUT schedule shape (required fields for local codec). */
export type BackupScheduleConfiguration = Required<
  Pick<BackupScheduleConfigurationDto, 'hourUtc' | 'minuteUtc'>
> &
  Pick<BackupScheduleConfigurationDto, 'dayOfWeek' | 'dayOfMonth' | 'customCron'> & {
    frequency: BackupScheduleFrequencyKey;
  };

export interface BackupSchedulePlannerState {
  frequency: BackupScheduleFrequencyKey;
  hourUtc: number;
  minuteUtc: number;
  dayOfWeek: number;
  dayOfMonth: number;
  customCron: string;
}

export const DEFAULT_SCHEDULE_PLANNER_STATE: BackupSchedulePlannerState = {
  frequency: 'Daily',
  hourUtc: 2,
  minuteUtc: 0,
  dayOfWeek: 1,
  dayOfMonth: 1,
  customCron: BACKUP_SCHEDULE_PRESET_CRONS.daily,
};

export function buildCronFromSchedule(config: BackupScheduleConfiguration): string {
  if (config.frequency === 'Custom') {
    return normalizeCronWhitespace(config.customCron ?? '');
  }
  if (config.frequency === 'Daily') {
    return `${config.minuteUtc} ${config.hourUtc} * * *`;
  }
  if (config.frequency === 'Weekly') {
    return `${config.minuteUtc} ${config.hourUtc} * * ${config.dayOfWeek ?? 1}`;
  }
  return `${config.minuteUtc} ${config.hourUtc} ${config.dayOfMonth ?? 1} * *`;
}

export function parseCronToSchedule(cron: string): BackupSchedulePlannerState {
  const n = normalizeCronWhitespace(cron);
  const m = /^(\d{1,2})\s+(\d{1,2})\s+(\*|\d{1,2})\s+(\*|\d{1,2})\s+(\*|\d{1,2})$/.exec(n);
  if (!m) {
    return {
      ...DEFAULT_SCHEDULE_PLANNER_STATE,
      frequency: 'Custom',
      customCron: n,
    };
  }

  const minuteUtc = Number.parseInt(m[1]!, 10);
  const hourUtc = Number.parseInt(m[2]!, 10);
  const dom = m[3]!;
  const month = m[4]!;
  const dow = m[5]!;

  if (dom === '*' && month === '*' && dow === '*') {
    return {
      frequency: 'Daily',
      hourUtc,
      minuteUtc,
      dayOfWeek: 1,
      dayOfMonth: 1,
      customCron: n,
    };
  }

  if (dom === '*' && month === '*' && dow !== '*') {
    return {
      frequency: 'Weekly',
      hourUtc,
      minuteUtc,
      dayOfWeek: Number.parseInt(dow, 10),
      dayOfMonth: 1,
      customCron: n,
    };
  }

  if (dom !== '*' && month === '*' && dow === '*') {
    return {
      frequency: 'Monthly',
      hourUtc,
      minuteUtc,
      dayOfWeek: 1,
      dayOfMonth: Number.parseInt(dom, 10),
      customCron: n,
    };
  }

  return {
    ...DEFAULT_SCHEDULE_PLANNER_STATE,
    frequency: 'Custom',
    hourUtc,
    minuteUtc,
    customCron: n,
  };
}

export function plannerStateToPutSchedule(
  state: BackupSchedulePlannerState,
): BackupScheduleConfigurationDto {
  return {
    frequency: FREQUENCY_TO_API[state.frequency],
    hourUtc: state.hourUtc,
    minuteUtc: state.minuteUtc,
    dayOfWeek: state.frequency === 'Weekly' ? state.dayOfWeek : null,
    dayOfMonth: state.frequency === 'Monthly' ? state.dayOfMonth : null,
    customCron: state.frequency === 'Custom' ? normalizeCronWhitespace(state.customCron) : null,
  };
}

export function isPlannerStateValid(state: BackupSchedulePlannerState): boolean {
  if (state.hourUtc < 0 || state.hourUtc > 23) return false;
  if (state.minuteUtc < 0 || state.minuteUtc > 59) return false;
  if (state.frequency === 'Weekly' && (state.dayOfWeek < 0 || state.dayOfWeek > 6)) return false;
  if (state.frequency === 'Monthly' && (state.dayOfMonth < 1 || state.dayOfMonth > 31)) return false;
  if (state.frequency === 'Custom') return isPlausibleStandardCron(state.customCron);
  return true;
}

export function buildCronFromPlannerState(state: BackupSchedulePlannerState): string {
  return buildCronFromSchedule({
    frequency: state.frequency,
    hourUtc: state.hourUtc,
    minuteUtc: state.minuteUtc,
    dayOfWeek: state.dayOfWeek,
    dayOfMonth: state.dayOfMonth,
    customCron: state.customCron,
  });
}

function normalizeApiFrequency(
  frequency: BackupScheduleConfigurationDto['frequency'] | undefined,
): BackupScheduleFrequencyKey {
  if (frequency === undefined) return 'Custom';
  return FREQUENCY_FROM_API[frequency] ?? 'Custom';
}

/** Map API schedule DTO to planner state. */
export function apiScheduleToPlannerState(
  schedule: BackupScheduleConfigurationDto | null | undefined,
  fallbackCron: string,
): BackupSchedulePlannerState {
  if (!schedule) return parseCronToSchedule(fallbackCron);

  return {
    frequency: normalizeApiFrequency(schedule.frequency),
    hourUtc: schedule.hourUtc ?? 2,
    minuteUtc: schedule.minuteUtc ?? 0,
    dayOfWeek: schedule.dayOfWeek ?? 1,
    dayOfMonth: schedule.dayOfMonth ?? 1,
    customCron: schedule.customCron ?? fallbackCron,
  };
}
