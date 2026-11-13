import dayjs, { type Dayjs } from 'dayjs';

import type { ExportTemplate, ExportTemplatePeriod } from '@/features/exports/exportTemplateTypes';
import { getExportTypeById } from '@/features/exports/exportTypeCatalog';

export type ResolvedDateRange = {
  from: Dayjs;
  to: Dayjs;
};

export function resolveTemplatePeriod(
  period: ExportTemplatePeriod,
  customFromUtc?: string,
  customToUtc?: string,
  now = dayjs()
): ResolvedDateRange {
  switch (period) {
    case 'last24h':
      return { from: now.subtract(24, 'hour'), to: now };
    case 'last7d':
      return { from: now.subtract(7, 'day').startOf('day'), to: now.endOf('day') };
    case 'currentMonth':
      return { from: now.startOf('month'), to: now.endOf('day') };
    case 'lastMonth': {
      const last = now.subtract(1, 'month');
      return { from: last.startOf('month'), to: last.endOf('month') };
    }
    case 'custom': {
      const from = customFromUtc ? dayjs(customFromUtc) : now.subtract(7, 'day');
      const to = customToUtc ? dayjs(customToUtc) : now;
      return { from, to };
    }
    default:
      return { from: now.subtract(7, 'day').startOf('day'), to: now.endOf('day') };
  }
}

/** Build target href with `exportTemplateId` query for page hydration. */
export function buildExportTemplateHref(template: ExportTemplate): string {
  const typeId =
    template.config.kind === 'dep-export'
      ? 'dep-export'
      : template.config.kind === 'tagesbericht'
        ? 'tagesbericht'
        : 'backup';
  const def = getExportTypeById(typeId);
  const base = def?.href ?? '/admin/download-history';
  const url = new URL(base, 'http://local.invalid');
  url.searchParams.set('exportTemplateId', template.id);
  return `${url.pathname}?${url.searchParams.toString()}`;
}

export const EXPORT_TEMPLATE_QUERY_KEY = 'exportTemplateId';
