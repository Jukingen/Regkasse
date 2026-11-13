export type CategoryGroup = {
  name: string;
  displayName: string;
  description: string;
  icon: string;
  productCount: number;
  subcategories?: CategoryGroup[];
};

/** Legacy demo-products.json labels → normalized catalog category names (mirrors backend SystemCategories). */
export const LEGACY_TO_CATALOG_CATEGORY_NAME: Record<string, string> = {
  'Pizza-mittel': 'Pizza, mittel',
  'Pizza-Partner': 'Pizza, Partner',
  'Mexikanische-Pizza-mittel': 'Mexikanische Pizza, mittel',
  'Mexikanische-Pizza-Partner': 'Mexikanische Pizza, Partner',
  'Alkoholfreie-Getrnke': 'Alkoholfreie Getränke',
};

export function expandDemoCategoryReferences(name: string): string[] {
  const refs = new Set<string>([name]);
  const normalized = LEGACY_TO_CATALOG_CATEGORY_NAME[name];
  if (normalized) refs.add(normalized);
  for (const [legacy, catalogName] of Object.entries(LEGACY_TO_CATALOG_CATEGORY_NAME)) {
    if (catalogName === name) refs.add(legacy);
  }
  return [...refs];
}

/** Maps catalog/normalized labels back to demo-products.json import keys for API requests. */
export function toLegacyImportCategoryName(name: string): string {
  if (Object.prototype.hasOwnProperty.call(LEGACY_TO_CATALOG_CATEGORY_NAME, name)) {
    return name;
  }
  for (const [legacy, catalogName] of Object.entries(LEGACY_TO_CATALOG_CATEGORY_NAME)) {
    if (name === catalogName) return legacy;
  }
  return name;
}

export function resolveCatalogCategoryCount(
  name: string,
  countByName: Map<string, number>,
  fallback: number
): number {
  for (const ref of expandDemoCategoryReferences(name)) {
    const count = countByName.get(ref);
    if (count != null) return count;
  }
  return fallback;
}

export const CATEGORY_GROUPS: CategoryGroup[] = [
  {
    name: 'pizzas',
    displayName: '🍕 Pizzen',
    description: 'Margherita, Salami, Funghi, Hawaii, Capricciosa, Quattro Formaggi und mehr',
    icon: '🍕',
    productCount: 85,
    subcategories: [
      {
        name: 'Pizza-mittel',
        displayName: 'Mittel (Ø 36cm)',
        description: 'Klassische Pizzen',
        icon: '🍕',
        productCount: 33,
      },
      {
        name: 'Pizza-Partner',
        displayName: 'Partner (Ø 40cm)',
        description: 'Größere Pizzen zum Teilen',
        icon: '🍕',
        productCount: 32,
      },
      {
        name: 'Familien-Pizza',
        displayName: 'Familie (Ø 50cm)',
        description: 'Extra große Familien-Pizza',
        icon: '🍕',
        productCount: 1,
      },
      {
        name: 'Mexikanische-Pizza-mittel',
        displayName: 'Mexikanisch Mittel',
        description: 'Scharfe Pizzen mit Jalapenos',
        icon: '🌶️',
        productCount: 4,
      },
      {
        name: 'Mexikanische-Pizza-Partner',
        displayName: 'Mexikanisch Partner',
        description: 'Scharfe Pizzen zum Teilen',
        icon: '🌶️',
        productCount: 4,
      },
      {
        name: 'Calzone',
        displayName: 'Calzone',
        description: 'Gefaltete Pizzen',
        icon: '🥟',
        productCount: 11,
      },
    ],
  },
  {
    name: 'salads',
    displayName: '🥗 Salate',
    description: 'Frische Salate mit verschiedenen Dressings',
    icon: '🥗',
    productCount: 11,
    subcategories: [
      {
        name: 'Salate',
        displayName: 'Alle Salate',
        description: 'Chefsalat, Thunfischsalat, Bauernsalat',
        icon: '🥗',
        productCount: 11,
      },
    ],
  },
  {
    name: 'pasta',
    displayName: '🍝 Pasta',
    description: 'Bolognese, Carbonara, Arrabbiata, Lasagne',
    icon: '🍝',
    productCount: 6,
    subcategories: [
      {
        name: 'Pasta',
        displayName: 'Pastagerichte',
        description: 'Mit Nudelsorte nach Wahl',
        icon: '🍝',
        productCount: 6,
      },
    ],
  },
  {
    name: 'burgers',
    displayName: '🍔 Burger',
    description: 'Hamburger, Cheeseburger, Chickenburger, BBQ Burger',
    icon: '🍔',
    productCount: 9,
    subcategories: [
      {
        name: 'Burger',
        displayName: 'Burger',
        description: 'Mit Pommes frites serviert',
        icon: '🍔',
        productCount: 9,
      },
    ],
  },
  {
    name: 'kebap',
    displayName: '🥙 Kebap',
    description: 'Döner, Dürüm, Kebap-Teller',
    icon: '🥙',
    productCount: 10,
    subcategories: [
      {
        name: 'Kebap',
        displayName: 'Kebap Gerichte',
        description: 'Mit Salat, Tomaten, Zwiebeln, Rotkraut',
        icon: '🥙',
        productCount: 10,
      },
    ],
  },
  {
    name: 'snacks',
    displayName: '🥪 Stangerl & Baguettes',
    description: 'Stangerl, Baguettes, Imbiss',
    icon: '🥪',
    productCount: 25,
    subcategories: [
      {
        name: 'Stangerl',
        displayName: 'Stangerl',
        description: 'mit Tomaten, Käse und Oregano',
        icon: '🥪',
        productCount: 11,
      },
      {
        name: 'Baguettes',
        displayName: 'Baguettes',
        description: 'mit Tomaten, Käse und Oregano',
        icon: '🥖',
        productCount: 10,
      },
      {
        name: 'Imbiss',
        displayName: 'Imbiss',
        description: 'Schnitzel, Nuggets, Wings, Pommes',
        icon: '🍟',
        productCount: 11,
      },
    ],
  },
  {
    name: 'desserts',
    displayName: '🍰 Desserts',
    description: 'Mohr im Hemd, Palatschinken',
    icon: '🍰',
    productCount: 3,
    subcategories: [
      {
        name: 'Desserts',
        displayName: 'Desserts',
        description: 'Süße Nachspeisen',
        icon: '🍰',
        productCount: 3,
      },
    ],
  },
  {
    name: 'drinks',
    displayName: '🥤 Getränke',
    description:
      'Coca Cola, Fanta, Sprite, Mezzo Mix, Almdudler, Eistee, Ayran, Mineralwasser, Red Bull',
    icon: '🥤',
    productCount: 17,
    subcategories: [
      {
        name: 'Alkoholfreie-Getrnke',
        displayName: 'Alkoholfreie Getränke',
        description: '0,33l und 0,5l Flaschen',
        icon: '🥤',
        productCount: 17,
      },
    ],
  },
];

export function enrichGroupsWithCatalog(
  groups: CategoryGroup[],
  countByName: Map<string, number>
): CategoryGroup[] {
  return groups.map((group) => {
    const subcategories = group.subcategories?.map((sub) => ({
      ...sub,
      productCount: resolveCatalogCategoryCount(sub.name, countByName, sub.productCount),
    }));
    const productCount = subcategories
      ? subcategories.reduce((sum, sub) => sum + sub.productCount, 0)
      : resolveCatalogCategoryCount(group.name, countByName, group.productCount);

    return { ...group, subcategories, productCount };
  });
}
