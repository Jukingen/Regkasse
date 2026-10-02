import { describe, expect, it } from '@jest/globals';
import { render, screen } from '@testing-library/react-native';
import React from 'react';

import { OfflineQueueLimitBar } from '../components/OfflineQueueLimitBar';

function flattenStyle(style: unknown): Record<string, unknown> {
  if (!style) return {};
  if (Array.isArray(style)) {
    return Object.assign({}, ...style.map((part) => flattenStyle(part)));
  }
  return typeof style === 'object' ? (style as Record<string, unknown>) : {};
}

describe('OfflineQueueLimitBar', () => {
  it.each([
    { current: 0, percent: '0%', tone: 'green' },
    { current: 25, percent: '50%', tone: 'green' },
    { current: 40, percent: '80%', tone: 'yellow' },
    { current: 50, percent: '100%', tone: 'red' },
  ] as const)(
    'renders $percent fill in $tone at $current of 50',
    async ({ current, percent, tone }) => {
      await render(
        <OfflineQueueLimitBar
          current={current}
          limit={50}
          label={`${current} von 50 Offline-Transaktionen verwendet`}
        />
      );

      expect(screen.getByTestId('offline-queue-limit-bar')).toBeTruthy();
      const fill = screen.getByTestId(`offline-queue-limit-fill-${tone}`);
      const style = flattenStyle(fill.props.style);
      expect(style.width).toBe(percent);
      if (tone === 'yellow') {
        expect(style.backgroundColor).toBe('#eab308');
      }
      if (tone === 'red') {
        expect(style.backgroundColor).toBe('#dc2626');
      }
      if (tone === 'green') {
        expect(style.backgroundColor).toBe('#16a34a');
      }
      expect(
        screen.getByText(`${current} von 50 Offline-Transaktionen verwendet`)
      ).toBeTruthy();
    }
  );
});
