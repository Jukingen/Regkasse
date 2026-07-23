'use client';

import { Alert } from 'antd';
import { useSearchParams } from 'next/navigation';
import { useEffect, useState } from 'react';

import {
  EXPORT_TEMPLATE_QUERY_KEY,
  resolveTemplatePeriod,
} from '@/features/exports/applyExportTemplate';
import { getExportTemplateById } from '@/features/exports/exportTemplatesStorage';
import type { ExportTemplate } from '@/features/exports/exportTemplateTypes';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useTenant } from '@/features/tenancy/providers/TenantProvider';
import { useI18n } from '@/i18n/I18nProvider';
import type { Dayjs } from 'dayjs';

type Props = {
  expectedKind: ExportTemplate['config']['kind'];
  onApplied?: (template: ExportTemplate) => void;
  /** When kind is tagesbericht, receive resolved range. */
  onTagesberichtRange?: (range: [Dayjs, Dayjs]) => void;
};

/**
 * Reads ?exportTemplateId= and shows a banner; optionally notifies parent to apply settings.
 */
export function ExportTemplateApplyBanner({
  expectedKind,
  onApplied,
  onTagesberichtRange,
}: Props) {
  const { t } = useI18n();
  const searchParams = useSearchParams();
  const { user } = useAuth();
  const { tenant } = useTenant();
  const [name, setName] = useState<string | null>(null);

  useEffect(() => {
    const templateId = searchParams.get(EXPORT_TEMPLATE_QUERY_KEY)?.trim();
    if (!templateId) return;
    const tmpl = getExportTemplateById(templateId, {
      userId: user?.id ?? 'anon',
      tenantId: tenant?.id ?? 'default',
    });
    if (!tmpl || tmpl.config.kind !== expectedKind) return;
    setName(tmpl.name);
    onApplied?.(tmpl);
    if (tmpl.config.kind === 'tagesbericht' && onTagesberichtRange) {
      const range = resolveTemplatePeriod(
        tmpl.config.period,
        tmpl.config.customFromUtc,
        tmpl.config.customToUtc
      );
      onTagesberichtRange([range.from, range.to]);
    }
  }, [searchParams, user?.id, tenant?.id, expectedKind, onApplied, onTagesberichtRange]);

  if (!name) return null;

  return (
    <Alert
      type="success"
      showIcon
      closable
      onClose={() => setName(null)}
      style={{ marginBottom: 16 }}
      title={t('common.exportTemplates.appliedOnPage', { name })}
      description={
        expectedKind === 'backup'
          ? t('common.exportTemplates.backupApplyHint')
          : expectedKind === 'tagesbericht'
            ? t('common.exportTemplates.tagesberichtApplyHint')
            : undefined
      }
    />
  );
}
