'use client';

import type { ReactNode } from 'react';

import { DensityContext, type DensityContextValue } from './DensityContext';

type DensityProviderProps = DensityContextValue & {
  children: ReactNode;
};

export function DensityProvider({ children, densityMode, setDensityMode }: DensityProviderProps) {
  return (
    <DensityContext.Provider value={{ densityMode, setDensityMode }}>
      {children}
    </DensityContext.Provider>
  );
}
