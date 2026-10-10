import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { render, screen, waitFor } from '@testing-library/react-native';
import React from 'react';

import OnboardingScreen from '../app/(auth)/onboarding';
import i18n, { changeLanguage, i18nReady } from '../i18n';
import { loginIndustrySubtitle } from '../services/verticalProfiles/posOnboarding';

const mockReplace = jest.fn<(...args: unknown[]) => void>();
const mockSecureGet = jest.fn<(...args: unknown[]) => Promise<string | null>>();
const mockSecureSet = jest.fn<(...args: unknown[]) => Promise<void>>();

const profile = {
  current: {
    profileId: 'vet',
    isLoading: false,
    source: 'network' as 'default' | 'cache' | 'network',
    error: null as string | null,
  },
};

jest.mock('../src/components/common/WaveLoader', () => {
  const React = require('react') as typeof import('react');
  const { Text } = require('react-native') as typeof import('react-native');
  return {
    WaveLoader: () => React.createElement(Text, null, 'loading'),
  };
});

jest.mock('expo-router', () => ({
  useRouter: () => ({
    replace: (...args: unknown[]) => mockReplace(...args),
  }),
}));

jest.mock('../contexts/VerticalProfileContext', () => ({
  useVerticalProfileContext: () => profile.current,
}));

jest.mock('../services/secureStorage', () => ({
  secureStorage: {
    getItem: (...args: unknown[]) => mockSecureGet(...args),
    setItem: (...args: unknown[]) => mockSecureSet(...args),
  },
}));

async function renderOnboarding(profileId: string) {
  profile.current = {
    profileId,
    isLoading: false,
    source: 'network',
    error: null,
  };
  await i18nReady;
  await changeLanguage('de');
  await render(<OnboardingScreen />);
}

describe('POS onboarding', () => {
  beforeEach(() => {
    mockReplace.mockReset();
    mockSecureGet.mockReset();
    mockSecureSet.mockReset();
    mockSecureGet.mockResolvedValue(null);
    mockSecureSet.mockResolvedValue(undefined);
  });

  it('shows the veterinary slides', async () => {
    await renderOnboarding('vet');
    expect(screen.getByText('Willkommen in Ihrer Tierarztpraxis')).toBeTruthy();
    expect(screen.getByText('Patientenakte')).toBeTruthy();
    expect(screen.getByText('1 / 3')).toBeTruthy();
  });

  it('shows the hair-salon slides', async () => {
    await renderOnboarding('hair-salon');
    expect(screen.getByText('Ihr Friseursalon')).toBeTruthy();
    expect(screen.getByText('Terminkalender')).toBeTruthy();
  });

  it('shows the taxi slides', async () => {
    await renderOnboarding('taxi');
    expect(screen.getByText('Ihr Taxi-Betrieb')).toBeTruthy();
    expect(screen.getByText('Fahrt')).toBeTruthy();
  });

  it('skips the deck when the SecureStore flag is set', async () => {
    mockSecureGet.mockResolvedValue('1');
    await renderOnboarding('vet');
    await waitFor(() => {
      expect(mockReplace).toHaveBeenCalledWith('/(tabs)/cash-register');
    });
  });

  it('builds the login industry line from the loaded profile', async () => {
    await i18nReady;
    await changeLanguage('de');
    const line = (profileId: string) =>
      loginIndustrySubtitle(
        profileId,
        (key) => i18n.t(key, { ns: 'verticalProfiles' }),
        (key, options) => i18n.t(key, { ns: 'auth', ...options })
      );

    expect(line('vet')).toBe('Regkasse — Tierarztpraxis');
    expect(line('hair-salon')).toBe('Regkasse — Friseursalon');
    expect(line('taxi')).toBe('Regkasse — Taxi-Betrieb');
    expect(line('default')).toBeNull();
  });
});
