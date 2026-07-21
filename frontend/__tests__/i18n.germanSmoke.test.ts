import { beforeAll, describe, expect, it } from '@jest/globals';
import i18next from 'i18next';

import deAuth from '../i18n/locales/de/auth.json';
import deCart from '../i18n/locales/de/cart.json';
import deCheckout from '../i18n/locales/de/checkout.json';
import deCommon from '../i18n/locales/de/common.json';
import deCustomers from '../i18n/locales/de/customers.json';
import deNavigation from '../i18n/locales/de/navigation.json';
import dePayment from '../i18n/locales/de/payment.json';
import deSettings from '../i18n/locales/de/settings.json';

/**
 * Smoke-test: POS German locale JSON resolves core UI strings correctly
 * (no raw keys / English "Voucher" leftovers).
 */
describe('POS i18n German (de)', () => {
  const i18n = i18next.createInstance();

  beforeAll(async () => {
    await i18n.init({
      lng: 'de',
      fallbackLng: 'de',
      defaultNS: 'common',
      resources: {
        de: {
          auth: deAuth,
          cart: deCart,
          checkout: deCheckout,
          common: deCommon,
          customers: deCustomers,
          navigation: deNavigation,
          payment: dePayment,
          settings: deSettings,
        },
      },
      interpolation: { escapeValue: false },
      react: { useSuspense: false },
    });
  });

  it('resolves core POS UI strings in German', () => {
    expect(i18n.t('auth:loginButton')).toBe('Anmelden');
    expect(i18n.t('auth:errors.invalidCredentials')).toContain('Passwort');
    expect(i18n.t('cart:checkoutButton')).toBe('Bezahlen');
    expect(i18n.t('cart:vat')).toBe('MwSt.');
    expect(i18n.t('cart:applyCoupon')).toBe('Gutschein anwenden');
    expect(i18n.t('payment:methods.voucher')).toBe('Gutschein');
    expect(i18n.t('payment:methods.cash')).toBe('Bargeld');
    expect(i18n.t('checkout:posFlow.payment.voucher.codeLabel')).toMatch(/Gutschein/);
    expect(i18n.t('customers:selectionTitle')).toBe('Kundenauswahl');
    expect(i18n.t('common:tax.reduced')).toBe('Ermäßigt');
    expect(i18n.t('navigation:backup')).toBe('Sicherung');
    expect(i18n.t('settings:shift.dailyClosing.reportVoucher')).toBe('Gutschein');
  });

  it('does not leave English voucher wording in German voucher keys', () => {
    const voucherSamples = [
      i18n.t('payment:methods.voucher'),
      i18n.t('cart:applyCoupon'),
      i18n.t('cart:couponModalTitle'),
      i18n.t('checkout:posFlow.payment.voucher.invalid'),
    ];
    for (const sample of voucherSamples) {
      expect(sample.toLowerCase()).not.toContain('voucher');
      expect(sample.toLowerCase()).toMatch(/gutschein/);
    }
  });
});
