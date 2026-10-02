import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { render, screen, waitFor } from '@testing-library/react-native';
import React from 'react';

import { StaffPicker } from '../components/StaffPicker';
import { changeLanguage } from '../i18n';

const mockListPosStaff = jest.fn<(...args: unknown[]) => Promise<unknown[]>>();

jest.mock('../services/api/staffService', () => ({
  listPosStaff: (...args: unknown[]) => mockListPosStaff(...args),
  staffInitials: (name: string) => {
    const parts = String(name).trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return `${parts[0][0] ?? ''}${parts[parts.length - 1][0] ?? ''}`.toUpperCase();
  },
}));

describe('StaffPicker', () => {
  beforeEach(async () => {
    jest.clearAllMocks();
    await changeLanguage('de');
    mockListPosStaff.mockResolvedValue([
      { id: 'staff-1', name: 'Anna Kasse', role: 'Cashier' },
    ]);
  });

  it('renders initials and role from GET /api/pos/staff', async () => {
    const onSelect = jest.fn();
    await render(<StaffPicker selectedId={null} onSelect={onSelect} />);

    expect(await screen.findByLabelText('Anna Kasse, Kassierer')).toBeTruthy();
    expect(screen.getByText('AK')).toBeTruthy();
    expect(screen.getByText('Kassierer')).toBeTruthy();
    await waitFor(() => {
      expect(mockListPosStaff).toHaveBeenCalled();
    });
  });
});
