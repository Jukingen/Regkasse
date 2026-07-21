import { type ThemeConfig, theme as antdTheme } from 'antd';

import { antdFontSizeForDensity, antdTablePaddingForDensity } from '@/lib/personalization/density';
import type { DensityMode, ResolvedTheme } from '@/lib/personalization/types';

import baseTheme from './themeConfig';

const { defaultAlgorithm, darkAlgorithm } = antdTheme;

export function buildAntdTheme(resolved: ResolvedTheme, density: DensityMode): ThemeConfig {
  const algorithm = resolved === 'dark' ? darkAlgorithm : defaultAlgorithm;
  const algorithms = density === 'compact' ? [algorithm, antdTheme.compactAlgorithm] : algorithm;
  const tablePadding = antdTablePaddingForDensity(density);

  return {
    ...baseTheme,
    algorithm: algorithms,
    token: {
      ...baseTheme.token,
      fontSize: antdFontSizeForDensity(density),
    },
    components: {
      ...baseTheme.components,
      Layout: {
        ...baseTheme.components?.Layout,
        bodyBg: resolved === 'dark' ? '#000000' : '#f5f5f5',
        headerBg: resolved === 'dark' ? '#141414' : '#ffffff',
        siderBg: resolved === 'dark' ? '#141414' : '#ffffff',
      },
      Table: {
        ...baseTheme.components?.Table,
        padding: tablePadding.padding,
        paddingLG: tablePadding.paddingLG,
      },
      Card: {
        ...baseTheme.components?.Card,
        paddingLG: tablePadding.paddingLG,
      },
    },
  };
}
