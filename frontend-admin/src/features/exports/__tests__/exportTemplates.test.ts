import dayjs from 'dayjs';
import { describe, expect, it } from 'vitest';

import { resolveTemplatePeriod } from '@/features/exports/applyExportTemplate';
import { EXPORT_TEMPLATE_PRESETS } from '@/features/exports/exportTemplatePresets';
import { isExportTemplate } from '@/features/exports/exportTemplatesStorage';

describe('exportTemplates', () => {
  it('presets are valid templates', () => {
    for (const p of EXPORT_TEMPLATE_PRESETS) {
      expect(isExportTemplate(p)).toBe(true);
    }
  });

  it('resolveTemplatePeriod last24h', () => {
    const now = dayjs('2026-07-22T12:00:00.000Z');
    const range = resolveTemplatePeriod('last24h', undefined, undefined, now);
    expect(range.to.isSame(now)).toBe(true);
    expect(range.from.isSame(now.subtract(24, 'hour'))).toBe(true);
  });

  it('resolveTemplatePeriod currentMonth', () => {
    const now = dayjs('2026-07-22T12:00:00.000Z');
    const range = resolveTemplatePeriod('currentMonth', undefined, undefined, now);
    expect(range.from.isSame(now.startOf('month'))).toBe(true);
  });
});
