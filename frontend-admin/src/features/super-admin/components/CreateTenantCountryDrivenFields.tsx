'use client';

import { Card, Checkbox, Form, Typography } from 'antd';
import { useEffect } from 'react';

import type { CountryProfileSummaryDto } from '@/api/generated/model';
import type { CreateTenantFormValues } from '@/features/super-admin/components/createTenantFormTypes';
import { useI18n } from '@/i18n';

export type CreateTenantCountryDrivenFieldsProps = {
  countries: CountryProfileSummaryDto[];
};

const EMPTY_SECTIONS: NonNullable<CountryProfileSummaryDto['fiscalSections']> = [];

export function CreateTenantCountryDrivenFields({ countries }: CreateTenantCountryDrivenFieldsProps) {
  const { t } = useI18n();
  const form = Form.useFormInstance<CreateTenantFormValues>();
  const countryCode = Form.useWatch('countryCode', form);
  const fiscalFlags = Form.useWatch('fiscalFlags', form) ?? {};
  const selected = countries.find((country) => country.code === countryCode);
  const sections = selected?.fiscalSections ?? EMPTY_SECTIONS;

  useEffect(() => {
    const next: Record<string, boolean> = {};
    for (const section of sections) {
      for (const flag of section.flags ?? []) {
        if (!flag.name) continue;
        next[flag.name] = flag.locked ? true : Boolean(flag.enabled);
      }
    }
    form.setFieldValue('fiscalFlags', next);
  }, [countryCode, form, sections]);

  if (!selected) {
    return null;
  }

  return (
    <div data-testid="create-tenant-fiscal-step">
      <Card size="small" data-testid="create-tenant-fiscal-card" style={{ marginTop: 16 }}>
        <Typography.Text type="secondary">{t('tenantCountry.fiscalSystem')}</Typography.Text>
        <Typography.Paragraph data-testid="create-tenant-fiscal-label" style={{ marginBottom: 8 }}>
          {selected.fiscalSystemLabel ?? selected.fiscalSystem}
        </Typography.Paragraph>
        <Typography.Text type="secondary">{t('tenantCountry.vatId')}</Typography.Text>
        <Typography.Paragraph data-testid="create-tenant-vat-id-hint" style={{ marginBottom: 0 }}>
          {selected.vatIdPattern}
        </Typography.Paragraph>
      </Card>
      {sections.map((section) => (
        <Card
          key={section.id ?? section.titleKey}
          size="small"
          data-testid={`fiscal-section-${section.id}`}
          style={{ marginTop: 12 }}
          title={section.titleKey ? t(section.titleKey) : section.id}
        >
          {section.helperKey ? (
            <Typography.Paragraph type="secondary">{t(section.helperKey)}</Typography.Paragraph>
          ) : null}
          {(section.flags ?? []).map((flag) =>
            flag.name ? (
              <Checkbox
                key={flag.name}
                data-testid={`fiscal-flag-${flag.name}`}
                checked={flag.locked ? true : Boolean(fiscalFlags[flag.name])}
                disabled={Boolean(flag.locked)}
                onChange={(event) => {
                  if (flag.locked || !flag.name) return;
                  form.setFieldValue('fiscalFlags', {
                    ...form.getFieldValue('fiscalFlags'),
                    [flag.name]: event.target.checked,
                  });
                }}
              >
                {flag.name}
              </Checkbox>
            ) : null
          )}
        </Card>
      ))}
    </div>
  );
}
