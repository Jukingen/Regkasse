import { describe, expect, it } from 'vitest';
import {
  buildCronFromPlannerState,
  buildCronFromSchedule,
  parseCronToSchedule,
  plannerStateToPutSchedule,
  type BackupSchedulePlannerState,
} from '@/features/backup/logic/backupScheduleCronCodec';
import { BackupScheduleFrequency } from '@/api/generated/model';

describe('backupScheduleCronCodec', () => {
  it('buildCronFromSchedule daily', () => {
    const cron = buildCronFromSchedule({
      frequency: BackupScheduleFrequency.Daily,
      hourUtc: 3,
      minuteUtc: 15,
    });
    expect(cron).toBe('15 3 * * *');
  });

  it('buildCronFromSchedule weekly Monday', () => {
    const cron = buildCronFromSchedule({
      frequency: BackupScheduleFrequency.Weekly,
      hourUtc: 2,
      minuteUtc: 0,
      dayOfWeek: 1,
    });
    expect(cron).toBe('0 2 * * 1');
  });

  it('parseCronToSchedule roundtrips monthly', () => {
    const cron = '30 4 15 * *';
    const state = parseCronToSchedule(cron);
    expect(state.frequency).toBe('Monthly');
    expect(state.dayOfMonth).toBe(15);
    expect(state.hourUtc).toBe(4);
    expect(state.minuteUtc).toBe(30);
    expect(buildCronFromPlannerState(state)).toBe(cron);
  });

  it('plannerStateToPutSchedule omits day fields for non-matching frequencies', () => {
    const daily: BackupSchedulePlannerState = {
      frequency: 'Daily',
      hourUtc: 2,
      minuteUtc: 0,
      dayOfWeek: 1,
      dayOfMonth: 15,
      customCron: '0 2 * * *',
    };
    const put = plannerStateToPutSchedule(daily);
    expect(put.dayOfWeek).toBeNull();
    expect(put.dayOfMonth).toBeNull();
    expect(put.customCron).toBeNull();
  });

  it('buildCronFromPlannerState matches buildCronFromSchedule for weekly', () => {
    const state: BackupSchedulePlannerState = {
      frequency: 'Weekly',
      hourUtc: 2,
      minuteUtc: 0,
      dayOfWeek: 1,
      dayOfMonth: 1,
      customCron: '0 2 * * 1',
    };
    expect(buildCronFromPlannerState(state)).toBe(
      buildCronFromSchedule(plannerStateToPutSchedule(state)),
    );
  });
});
