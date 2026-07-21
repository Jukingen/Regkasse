export type GlobalSearchMatchRank = 'exact' | 'startsWith' | 'contains';

export type GlobalSearchResultItem = {
  id: string;
  menuKey: string;
  href: string;
  label: string;
  breadcrumb?: string;
  keywords: string[];
};

export type RankedGlobalSearchResult = {
  item: GlobalSearchResultItem;
  rank: GlobalSearchMatchRank;
  score: number;
};

/** Pre-built, locale-resolved nav search catalog (before permission filter). */
export type MenuSearchIndexSource = {
  items: GlobalSearchResultItem[];
  /** Domain / nested hub labels for optional group-name matching */
  groupLabels: string[];
};

/** Permission-filtered index consumed by GlobalSearch. */
export type MenuSearchIndex = {
  items: GlobalSearchResultItem[];
  /** Active UI text locale when the index was built */
  locale: string;
  /** Allowed sidebar leaf `menuKey` values (same set as filtered sidebar) */
  allowedMenuKeys: ReadonlySet<string>;
};
