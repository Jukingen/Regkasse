'use client';

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { catalogs, readMarketingLang, writeMarketingLang } from '@/i18n/catalogs';
import type { Catalog, Lang } from '@/i18n/types';

type MarketingI18n = {
  lang: Lang;
  setLang: (lang: Lang) => void;
  t: Catalog;
};

const MarketingI18nContext = createContext<MarketingI18n | null>(null);

export function MarketingI18nProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>('de');

  useEffect(() => {
    const stored = readMarketingLang();
    setLangState(stored);
    document.documentElement.lang = stored;
  }, []);

  const value = useMemo<MarketingI18n>(
    () => ({
      lang,
      setLang: (next) => {
        setLangState(next);
        writeMarketingLang(next);
      },
      t: catalogs[lang],
    }),
    [lang],
  );

  return <MarketingI18nContext.Provider value={value}>{children}</MarketingI18nContext.Provider>;
}

export function useTranslations(): MarketingI18n {
  const value = useContext(MarketingI18nContext);
  if (!value) {
    throw new Error('useTranslations must be used inside MarketingI18nProvider');
  }
  return value;
}
