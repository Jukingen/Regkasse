import type { DensityMode } from './types';

export type DensityStyleTokens = {
  padding: string;
  fontSize: string;
  lineHeight: string;
};

export const DENSITY_STYLES: Record<DensityMode, DensityStyleTokens> = {
  comfortable: {
    padding: '16px',
    fontSize: '16px',
    lineHeight: '1.5',
  },
  standard: {
    padding: '12px',
    fontSize: '14px',
    lineHeight: '1.4',
  },
  compact: {
    padding: '8px',
    fontSize: '12px',
    lineHeight: '1.3',
  },
};

export function antdComponentSizeForDensity(density: DensityMode): 'small' | 'middle' | 'large' {
  if (density === 'compact') return 'small';
  if (density === 'comfortable') return 'large';
  return 'middle';
}

export function antdFontSizeForDensity(density: DensityMode): number {
  if (density === 'compact') return 12;
  if (density === 'comfortable') return 16;
  return 14;
}

export function antdTablePaddingForDensity(density: DensityMode): {
  padding: number;
  paddingLG: number;
} {
  if (density === 'compact') return { padding: 8, paddingLG: 8 };
  if (density === 'comfortable') return { padding: 16, paddingLG: 24 };
  return { padding: 12, paddingLG: 16 };
}
