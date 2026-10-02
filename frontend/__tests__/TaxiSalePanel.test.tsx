import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen } from '@testing-library/react-native';
import React from 'react';

import { TaxiSalePanel } from '../components/TaxiSalePanel';
import { TaxiTripProvider } from '../contexts/TaxiTripContext';
import { changeLanguage } from '../i18n';

jest.mock('../contexts/VerticalProfileContext', () => ({
  useVerticalProfileContext: () => ({
    taxiTariffPerKm: 2.4,
    posLayout: 'taxi',
    profileId: 'taxi',
    posFeatures: { routeTracking: true },
    requiredFields: { customer: [], product: [] },
    optionalFields: { customer: [], product: [] },
  }),
}));

describe('TaxiSalePanel', () => {
  beforeEach(async () => {
    await changeLanguage('de');
  });

  it('shows Start Trip and End Trip in taxi mode', async () => {
    const onSelectProduct = jest.fn();
    const onPayment = jest.fn();

    await render(
      <TaxiTripProvider>
        <TaxiSalePanel
          products={[]}
          selectedProductId={null}
          onSelectProduct={onSelectProduct}
          onPayment={onPayment}
          canPay={false}
        />
      </TaxiTripProvider>
    );

    const start = await screen.findByLabelText('Fahrt starten');
    expect(start).toBeTruthy();
    await fireEvent.press(start);
    expect(await screen.findByLabelText('Fahrt beenden')).toBeTruthy();
  });
});
