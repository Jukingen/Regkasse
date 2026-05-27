'use client';

import { createContext, useContext } from 'react';
import type { DensityMode } from './types';

export type DensityContextValue = {
  densityMode: DensityMode;
  setDensityMode: (mode: DensityMode) => void;
};

export const DensityContext = createContext<DensityContextValue | null>(null);

export function useDensityContext(): DensityContextValue {
  const ctx = useContext(DensityContext);
  if (!ctx) {
    throw new Error('useDensityContext must be used inside DensityProvider');
  }
  return ctx;
}
