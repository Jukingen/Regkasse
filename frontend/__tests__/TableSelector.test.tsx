import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { render, screen } from '@testing-library/react-native';
import React from 'react';

import { TableSelector } from '../components/TableSelector';
import { changeLanguage } from '../i18n';

jest.mock('../components/TableSelectorTile', () => {
  const React = require('react') as typeof import('react');
  const { Text } = require('react-native') as typeof import('react-native');
  return {
    TableSelectorTile: ({ tableNumber }: { tableNumber: number }) =>
      React.createElement(Text, null, `tile-${tableNumber}`),
    webTablePressableOutlineOff: {},
  };
});

const mockTableFeatures = { tables: true };

jest.mock('../contexts/VerticalProfileContext', () => ({
  useVerticalFeatures: () => ({
    profileId: 'gastronomy-tables',
    posLayout: 'tables',
    posFeatures: mockTableFeatures,
    requiredFields: { customer: [], product: [] },
    optionalFields: { customer: [], product: [] },
  }),
}));

jest.mock('react-native-safe-area-context', () => ({
  useSafeAreaInsets: () => ({ top: 0, right: 0, bottom: 0, left: 0 }),
}));

function renderSelector() {
  return render(
    <TableSelector
      selectedTable={1}
      onTableSelect={() => undefined}
      tableCarts={new Map()}
      recoveryData={null}
      tableSelectionLoading={null}
      onClearAllTables={() => undefined}
    />
  );
}

describe('TableSelector', () => {
  beforeEach(async () => {
    await changeLanguage('de');
    mockTableFeatures.tables = true;
  });

  it('shows the table row when tables is on', async () => {
    await renderSelector();
    expect(await screen.findByText('Tisch wählen')).toBeTruthy();
    expect(screen.getByText('tile-1')).toBeTruthy();
  });

  it('hides the table row when tables is off', async () => {
    mockTableFeatures.tables = false;
    await renderSelector();
    expect(screen.queryByText('Tisch wählen')).toBeNull();
    expect(screen.queryByText('tile-1')).toBeNull();
  });
});
