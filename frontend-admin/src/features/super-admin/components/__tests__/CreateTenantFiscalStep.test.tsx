import { Form, Input } from 'antd';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import React from 'react';
import { describe, expect, it } from 'vitest';

import type { CountryProfileSummaryDto } from '@/api/generated/model';
import { CreateTenantCountryDrivenFields } from '@/features/super-admin/components/CreateTenantCountryDrivenFields';
import { I18nProvider } from '@/i18n';

const COUNTRIES: CountryProfileSummaryDto[] = [
  {
    code: 'AT',
    name: 'Austria',
    currency: 'EUR',
    defaultLocale: 'de-DE',
    fiscalSystem: 'RKSV_AT',
    fiscalSystemLabel: 'RKSV',
    eInvoicingStandards: [],
    allowedVatRegimes: ['AT_RKSV_STANDARD'],
    vatIdPattern: String.raw`^ATU\d{8}$`,
    fiscalSections: [
      {
        id: 'rksv',
        titleKey: 'tenants.create.fiscal.rksv.title',
        helperKey: 'tenants.create.fiscal.rksv.helper',
        flags: [{ name: 'Fiscal.RksvAt', enabled: true, locked: true }],
      },
    ],
  },
  {
    code: 'DE',
    name: 'Germany',
    currency: 'EUR',
    defaultLocale: 'de-DE',
    fiscalSystem: 'KASSENSICHERHEIT_DE',
    fiscalSystemLabel: 'KassenSicherheit',
    eInvoicingStandards: ['ZUGFERD', 'XRECHNUNG'],
    allowedVatRegimes: ['DE_USTG_STANDARD'],
    vatIdPattern: String.raw`^DE\d{9}$`,
    fiscalSections: [
      {
        id: 'kassenSicherheit',
        titleKey: 'tenants.create.fiscal.kassenSicherheit.title',
        helperKey: 'tenants.create.fiscal.kassenSicherheit.helper',
        flags: [{ name: 'Fiscal.KassenSicherheitDe', enabled: true, locked: false }],
      },
    ],
  },
  {
    code: 'CH',
    name: 'Switzerland',
    currency: 'CHF',
    defaultLocale: 'de-CH',
    fiscalSystem: 'MWST_CH',
    fiscalSystemLabel: 'MWST',
    eInvoicingStandards: ['QR_RECHNUNG'],
    allowedVatRegimes: ['CH_MWST_STANDARD'],
    vatIdPattern: '^CHE-...',
    fiscalSections: [
      {
        id: 'qrRechnung',
        titleKey: 'tenants.create.fiscal.qrRechnung.title',
        helperKey: 'tenants.create.fiscal.qrRechnung.helper',
        flags: [{ name: 'EInvoicing.QrRechnung', enabled: true, locked: false }],
      },
    ],
  },
  {
    code: 'EU_DEFAULT',
    name: 'EU',
    currency: 'EUR',
    defaultLocale: 'en',
    fiscalSystem: 'NONE',
    fiscalSystemLabel: 'None',
    eInvoicingStandards: ['EN_16931'],
    allowedVatRegimes: ['NON_EU'],
    vatIdPattern: '^[A-Z]{2}',
    fiscalSections: [
      {
        id: 'en16931',
        titleKey: 'tenants.create.fiscal.en16931.title',
        helperKey: 'tenants.create.fiscal.en16931.helper',
        flags: [{ name: 'EInvoicing.En16931', enabled: true, locked: false }],
      },
    ],
  },
];

function Harness({ countryCode }: { countryCode: string }) {
  const [form] = Form.useForm();
  return (
    <I18nProvider>
      <Form form={form} initialValues={{ countryCode }}>
        <Form.Item name="countryCode" hidden>
          <Input />
        </Form.Item>
        <CreateTenantCountryDrivenFields countries={COUNTRIES} />
      </Form>
    </I18nProvider>
  );
}

describe('CreateTenant fiscal sections from countries payload', () => {
  it('shows RKSV for AT and keeps the flag locked', async () => {
    render(<Harness countryCode="AT" />);
    expect(await screen.findByTestId('fiscal-section-rksv')).toBeInTheDocument();
    expect(screen.queryByTestId('fiscal-section-kassenSicherheit')).not.toBeInTheDocument();
    expect(within(screen.getByTestId('fiscal-section-rksv')).getByRole('checkbox')).toBeDisabled();
  });

  it('shows KassenSicherheit for DE and hides RKSV', async () => {
    render(<Harness countryCode="DE" />);
    expect(await screen.findByTestId('fiscal-section-kassenSicherheit')).toBeInTheDocument();
    expect(screen.queryByTestId('fiscal-section-rksv')).not.toBeInTheDocument();
    expect(within(screen.getByTestId('fiscal-section-kassenSicherheit')).getByRole('checkbox')).toBeEnabled();
  });

  it('shows QR-Rechnung for CH', async () => {
    render(<Harness countryCode="CH" />);
    expect(await screen.findByTestId('fiscal-section-qrRechnung')).toBeInTheDocument();
    expect(screen.queryByTestId('fiscal-section-rksv')).not.toBeInTheDocument();
  });

  it('shows EN 16931 for the EU payload', async () => {
    const user = userEvent.setup();
    render(<Harness countryCode="EU_DEFAULT" />);
    expect(await screen.findByTestId('fiscal-section-en16931')).toBeInTheDocument();
    const flag = within(screen.getByTestId('fiscal-section-en16931')).getByRole('checkbox');
    expect(flag).toBeEnabled();
    await user.click(flag);
    expect(flag).not.toBeChecked();
  });
});
