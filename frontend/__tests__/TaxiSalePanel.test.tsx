import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen } from '@testing-library/react-native';
import React from 'react';

import { TaxiSalePanel } from '../components/TaxiSalePanel';
import { TaxiTripProvider } from '../contexts/TaxiTripContext';
import { changeLanguage } from '../i18n';

const mockTaxiProfile = {
  taxiTariffPerKm: 2.4,
  posLayout: 'taxi',
  profileId: 'taxi',
  posFeatures: { routeTracking: true },
  requiredFields: { customer: [], product: [] },
  optionalFields: { customer: [], product: [] },
};

jest.mock('../contexts/VerticalProfileContext', () => ({
  useVerticalProfileContext: () => mockTaxiProfile,
  useVerticalFeatures: () => mockTaxiProfile,
}));

describe('TaxiSalePanel', () => {
  beforeEach(async () => {
    await changeLanguage('de');
    mockTaxiProfile.posLayout = 'taxi';
    mockTaxiProfile.posFeatures.routeTracking = true;
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

  it('hides the panel unless the layout is taxi and routeTracking is on', async () => {
    mockTaxiProfile.posFeatures.routeTracking = false;
    const hidden = await render(
      <TaxiTripProvider>
        <TaxiSalePanel
          products={[]}
          selectedProductId={null}
          onSelectProduct={() => undefined}
          onPayment={() => undefined}
          canPay={false}
        />
      </TaxiTripProvider>
    );
    expect(screen.queryByLabelText('Fahrt starten')).toBeNull();
    hidden.unmount();

    mockTaxiProfile.posFeatures.routeTracking = true;
    mockTaxiProfile.posLayout = 'standard';
    await render(
      <TaxiTripProvider>
        <TaxiSalePanel
          products={[]}
          selectedProductId={null}
          onSelectProduct={() => undefined}
          onPayment={() => undefined}
          canPay={false}
        />
      </TaxiTripProvider>
    );
    expect(screen.queryByLabelText('Fahrt starten')).toBeNull();
  });
});
