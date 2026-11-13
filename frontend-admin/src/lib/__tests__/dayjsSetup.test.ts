import { describe, expect, it } from 'vitest';

import dayjs, { ensureDayjsPlugins } from '@/lib/dayjs';

describe('dayjs setup', () => {
  it('registers plugins idempotently', () => {
    ensureDayjsPlugins();
    ensureDayjsPlugins();

    expect(dayjs.utc('2026-07-20T12:00:00Z').format('YYYY-MM-DD')).toBe('2026-07-20');
    expect(dayjs.utc('2026-07-20T12:00:00Z').isoWeek()).toBeGreaterThan(0);
    expect(typeof dayjs().fromNow()).toBe('string');
    expect(dayjs.tz('2026-07-20 12:00:00', 'Europe/Vienna').isValid()).toBe(true);
    expect(dayjs('20.07.2026', 'DD.MM.YYYY', true).isValid()).toBe(true);
  });
});
