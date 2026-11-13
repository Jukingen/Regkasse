import { describe, expect, it, jest } from '@jest/globals';
import { render, screen } from '@testing-library/react-native';
import fs from 'fs';
import path from 'path';
import React from 'react';

import { MonatsbelegSessionBlockModal } from '../components/MonatsbelegSessionBlockModal';
import type { PosPermissionUser } from '../utils/posPermissions';

const mockAuthUser: { current: PosPermissionUser } = {
  current: { role: 'Waiter', permissions: ['order.view', 'order.create'] },
};

const mockRequestCreate = jest.fn();

jest.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({ user: mockAuthUser.current }),
}));

jest.mock('../contexts/PosRegisterReadinessContext', () => ({
  usePosRegisterReadiness: () => ({
    data: {
      effectiveRegisterId: '11111111-1111-4111-8111-111111111111',
      nextAction: 'monatsbeleg_required',
      monatsbelegSalesBlocked: true,
    },
    loading: false,
    error: null,
    refreshAsync: jest.fn(),
  }),
}));

jest.mock('../hooks/useMonatsbelegStatus', () => ({
  useMonatsbelegStatus: () => ({ data: { lastMonthMissing: true } }),
}));

jest.mock('../hooks/usePosMonatsbelegCreate', () => ({
  usePosMonatsbelegCreate: () => ({ busy: false, requestCreate: mockRequestCreate }),
}));

jest.mock('../services/api/cashRegisterService', () => ({
  notifyPosMonatsbelegManager: jest.fn(),
}));

jest.mock('../constants/posFeatureFlags', () => ({
  POS_ENSURE_READY_ON_ENTRY: true,
}));

jest.mock('../src/components/common/WaveLoader', () => ({
  WaveLoader: () => null,
}));

describe('MonatsbelegSessionBlockModal permission gate', () => {
  it('Waiter → no create button; only Manager kontaktieren', async () => {
    mockAuthUser.current = {
      role: 'Waiter',
      permissions: ['order.view', 'order.create'],
    };
    await render(<MonatsbelegSessionBlockModal forcedVisible />);

    expect(screen.getByLabelText('Manager kontaktieren')).toBeTruthy();
    expect(screen.queryByTestId('monatsbeleg-session-create')).toBeNull();
    expect(screen.queryByLabelText('Monatsbeleg jetzt erstellen')).toBeNull();
    expect(screen.queryByLabelText('Jahresbeleg jetzt erstellen')).toBeNull();
  });

  it('does not use canCreateSonderbeleg', () => {
    const source = fs.readFileSync(
      path.join(__dirname, '../components/MonatsbelegSessionBlockModal.tsx'),
      'utf8'
    );
    expect(source).toContain("const RKSV_MONATSBELEG_CREATE = 'rksv.monatsbeleg.create'");
    expect(source).toContain('hasPermission(user, RKSV_MONATSBELEG_CREATE)');
    expect(source).not.toContain('canCreateSonderbeleg');
  });

  it('Cashier → create button shown', async () => {
    mockAuthUser.current = {
      role: 'Cashier',
      permissions: ['rksv.monatsbeleg.create'],
    };
    await render(<MonatsbelegSessionBlockModal forcedVisible />);

    expect(screen.getByLabelText('Manager kontaktieren')).toBeTruthy();
    expect(screen.getByTestId('monatsbeleg-session-create')).toBeTruthy();
    expect(screen.getByLabelText('Monatsbeleg jetzt erstellen')).toBeTruthy();
  });
});
