export type CategoryGroup = {
    name: string;
    displayName: string;
    description: string;
    icon: string;
    productCount: number;
    subcategories?: CategoryGroup[];
};

export const CATEGORY_GROUPS: CategoryGroup[] = [
    {
        name: 'pizzas',
        displayName: '🍕 Pizzen',
        description: 'Margherita, Salami, Funghi, Hawaii, Capricciosa, Quattro Formaggi und mehr',
        icon: '🍕',
        productCount: 35,
        subcategories: [
            { name: 'Pizza-mittel', displayName: 'Mittel (Ø 36cm)', description: 'Klassische Pizzen', icon: '🍕', productCount: 25 },
            { name: 'Pizza-Partner', displayName: 'Partner (Ø 40cm)', description: 'Größere Pizzen zum Teilen', icon: '🍕', productCount: 25 },
            { name: 'Familien-Pizza', displayName: 'Familie (Ø 50cm)', description: 'Extra große Familien-Pizza', icon: '🍕', productCount: 1 },
            { name: 'Mexikanische-Pizza-mittel', displayName: 'Mexikanisch Mittel', description: 'Scharfe Pizzen mit Jalapenos', icon: '🌶️', productCount: 4 },
            { name: 'Mexikanische-Pizza-Partner', displayName: 'Mexikanisch Partner', description: 'Scharfe Pizzen zum Teilen', icon: '🌶️', productCount: 4 },
            { name: 'Calzone', displayName: 'Calzone', description: 'Gefaltete Pizzen', icon: '🥟', productCount: 11 },
        ],
    },
    {
        name: 'salads',
        displayName: '🥗 Salate',
        description: 'Frische Salate mit verschiedenen Dressings',
        icon: '🥗',
        productCount: 10,
        subcategories: [
            { name: 'Salate', displayName: 'Alle Salate', description: 'Chefsalat, Thunfischsalat, Bauernsalat', icon: '🥗', productCount: 10 },
        ],
    },
    {
        name: 'pasta',
        displayName: '🍝 Pasta',
        description: 'Bolognese, Carbonara, Arrabbiata, Lasagne',
        icon: '🍝',
        productCount: 6,
        subcategories: [
            { name: 'Pasta', displayName: 'Pastagerichte', description: 'Mit Nudelsorte nach Wahl', icon: '🍝', productCount: 6 },
        ],
    },
    {
        name: 'burgers',
        displayName: '🍔 Burger',
        description: 'Hamburger, Cheeseburger, Chickenburger, BBQ Burger',
        icon: '🍔',
        productCount: 9,
        subcategories: [
            { name: 'Burger', displayName: 'Burger', description: 'Mit Pommes frites serviert', icon: '🍔', productCount: 9 },
        ],
    },
    {
        name: 'kebap',
        displayName: '🥙 Kebap',
        description: 'Döner, Dürüm, Kebap-Teller',
        icon: '🥙',
        productCount: 10,
        subcategories: [
            { name: 'Kebap', displayName: 'Kebap Gerichte', description: 'Mit Salat, Tomaten, Zwiebeln, Rotkraut', icon: '🥙', productCount: 10 },
        ],
    },
    {
        name: 'snacks',
        displayName: '🥪 Stangerl & Baguettes',
        description: 'Stangerl, Baguettes, Imbiss',
        icon: '🥪',
        productCount: 25,
        subcategories: [
            { name: 'Stangerl', displayName: 'Stangerl', description: 'mit Tomaten, Käse und Oregano', icon: '🥪', productCount: 11 },
            { name: 'Baguettes', displayName: 'Baguettes', description: 'mit Tomaten, Käse und Oregano', icon: '🥖', productCount: 10 },
            { name: 'Imbiss', displayName: 'Imbiss', description: 'Schnitzel, Nuggets, Wings, Pommes', icon: '🍟', productCount: 11 },
        ],
    },
    {
        name: 'desserts',
        displayName: '🍰 Desserts',
        description: 'Mohr im Hemd, Palatschinken',
        icon: '🍰',
        productCount: 3,
        subcategories: [
            { name: 'Desserts', displayName: 'Desserts', description: 'Süße Nachspeisen', icon: '🍰', productCount: 3 },
        ],
    },
    {
        name: 'drinks',
        displayName: '🥤 Getränke',
        description: 'Coca Cola, Fanta, Sprite, Mezzo Mix, Almdudler, Eistee, Ayran, Mineralwasser, Red Bull',
        icon: '🥤',
        productCount: 16,
        subcategories: [
            { name: 'Alkoholfreie-Getrnke', displayName: 'Alkoholfreie Getränke', description: '0,33l und 0,5l Flaschen', icon: '🥤', productCount: 16 },
        ],
    },
];

export function enrichGroupsWithCatalog(groups: CategoryGroup[], countByName: Map<string, number>): CategoryGroup[] {
    return groups.map((group) => {
        const subcategories = group.subcategories?.map((sub) => ({
            ...sub,
            productCount: countByName.get(sub.name) ?? sub.productCount,
        }));
        const productCount = subcategories
            ? subcategories.reduce((sum, sub) => sum + sub.productCount, 0)
            : countByName.get(group.name) ?? group.productCount;

        return { ...group, subcategories, productCount };
    });
}
