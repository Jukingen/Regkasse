import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { render, screen, waitFor } from '@testing-library/react-native';
import React from 'react';
import { Text } from 'react-native';

import { IfVerticalFeature } from '../components/IfVerticalFeature';
import {
  VerticalProfileProvider,
  useVerticalProfileContext,
} from '../contexts/VerticalProfileContext';

const mockGet = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockSecureGet = jest.fn<(...args: unknown[]) => Promise<string | null>>();
const mockSecureSet = jest.fn<(...args: unknown[]) => Promise<void>>();

jest.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({
    isAuthenticated: true,
    isAuthReady: true,
    user: {
      id: 'user-1',
      tenantId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      tenantSlug: 'tenant-a',
    },
  }),
}));

jest.mock('../services/api/config', () => ({
  apiClient: {
    get: (...args: unknown[]) => mockGet(...args),
  },
}));

jest.mock('../services/secureStorage', () => ({
  secureStorage: {
    getItem: (...args: unknown[]) => mockSecureGet(...args),
    setItem: (...args: unknown[]) => mockSecureSet(...args),
  },
}));

function ProfileTestSurface() {
  const { profileId } = useVerticalProfileContext();
  return (
    <>
      <Text testID="profile-id">{profileId}</Text>
      <IfVerticalFeature feature="patientRecord">
        <Text>Patientenakte sichtbar</Text>
      </IfVerticalFeature>
    </>
  );
}

async function renderProfile(response: unknown) {
  mockGet.mockResolvedValue(response);
  await render(
    <VerticalProfileProvider>
      <ProfileTestSurface />
    </VerticalProfileProvider>
  );
  await waitFor(() => {
    expect(screen.getByTestId('profile-id').props.children).not.toBe('default');
  });
}

describe('VerticalProfileContext conditional rendering', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockSecureGet.mockResolvedValue(null);
    mockSecureSet.mockResolvedValue(undefined);
  });

  it('gastronomy profile hides patient records', async () => {
    await renderProfile({
      profileId: 'gastronomy',
      posFeatures: { tables: false, patientRecord: false },
      requiredFields: { customer: [], product: [] },
      optionalFields: { customer: [], product: [] },
      posLayout: 'standard',
    });

    expect(mockGet).toHaveBeenCalledWith('/pos/vertical-profile');
    expect(screen.queryByText('Patientenakte sichtbar')).toBeNull();
  });

  it('vet profile shows patient records', async () => {
    await renderProfile({
      profileId: 'vet',
      posFeatures: { tables: false, patientRecord: true },
      requiredFields: { customer: ['petName'], product: [] },
      optionalFields: { customer: ['patientNotes'], product: [] },
      posLayout: 'standard',
    });

    expect(screen.getByText('Patientenakte sichtbar')).toBeTruthy();
  });
});
