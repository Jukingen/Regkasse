import { describe, expect, it } from '@jest/globals';
import fs from 'fs';
import path from 'path';

describe('Monatsbeleg banner-first placement (Paket 45)', () => {
  const tabsLayout = fs.readFileSync(
    path.join(__dirname, '../app/(tabs)/_layout.tsx'),
    'utf8'
  );
  const cashRegister = fs.readFileSync(
    path.join(__dirname, '../app/(tabs)/cash-register.tsx'),
    'utf8'
  );
  const banner = fs.readFileSync(
    path.join(__dirname, '../components/MonatsbelegSalesWarningBanner.tsx'),
    'utf8'
  );

  it('does not auto-mount MonatsbelegSessionBlockModal from tabs layout', () => {
    expect(tabsLayout).not.toContain('MonatsbelegSessionBlockModal');
  });

  it('keeps the hard-block modal on cash-register for payment/shift attempts', () => {
    expect(cashRegister).toContain('MonatsbelegSessionBlockModal');
    expect(cashRegister).toContain('forcedVisible={monatsbelegHardBlockVisible}');
    expect(cashRegister).toContain('isReadinessMonatsbelegGateActive');
  });

  it('extends the existing dashboard banner instead of adding a second banner', () => {
    expect(cashRegister).toContain('<MonatsbelegSalesWarningBanner');
    expect((cashRegister.match(/<MonatsbelegSalesWarningBanner/g) ?? []).length).toBe(1);
    expect(cashRegister).not.toContain('MonatsbelegLockBanner');
    expect(banner).toContain('resolveMonatsbelegBannerState');
    expect(banner).toContain('checkout:monatsbeleg.banner.');
    expect(banner).toContain('monatsbeleg-dashboard-banner');
  });
});
