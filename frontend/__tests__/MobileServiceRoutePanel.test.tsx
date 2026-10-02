import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { render, screen } from '@testing-library/react-native';
import React from 'react';

import { MobileServiceRoutePanel } from '../components/MobileServiceRoutePanel';
import { MobileServiceJobProvider } from '../contexts/MobileServiceJobContext';
import { changeLanguage } from '../i18n';

const mockProfile = {
  profileId: 'mobile-services',
  posLayout: 'appointment',
  posFeatures: { routeTracking: true, appointment: true, serviceDuration: true },
  requiredFields: { customer: ['name', 'phone', 'address'], product: [] },
  optionalFields: { customer: [], product: [] },
};

jest.mock('../contexts/VerticalProfileContext', () => ({
  useVerticalFeatures: () => mockProfile,
  useVerticalProfileContext: () => mockProfile,
}));

jest.mock('../services/api/customerService', () => ({
  customerService: {
    create: jest.fn(async () => ({ id: 'c1', name: 'Lena' })),
    updateAddress: jest.fn(async () => ({ id: 'c1', name: 'Lena' })),
  },
}));

describe('MobileServiceRoutePanel', () => {
  beforeEach(async () => {
    await changeLanguage('de');
    mockProfile.profileId = 'mobile-services';
  });

  it('renders the route and address panel for mobile-services', async () => {
    await render(
      <MobileServiceJobProvider>
        <MobileServiceRoutePanel customerName="Lena Mobil" />
      </MobileServiceJobProvider>
    );

    expect(await screen.findByText('Adresse und Einsatzort')).toBeTruthy();
    expect(screen.getByText('Kundenadresse')).toBeTruthy();
    expect(screen.getByText('Einsatzort')).toBeTruthy();
    expect(screen.getByLabelText('Adresse speichern')).toBeTruthy();
  });

  it('exposes customer and job street fields', async () => {
    await render(
      <MobileServiceJobProvider>
        <MobileServiceRoutePanel customerName="Lena Mobil" />
      </MobileServiceJobProvider>
    );

    expect(await screen.findByLabelText('customer-Straße')).toBeTruthy();
    expect(screen.getByLabelText('job-Straße')).toBeTruthy();
  });
});
