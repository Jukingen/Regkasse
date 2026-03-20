/**
 * Centralized API endpoint paths — MUST match swagger.json / backend route attributes exactly.
 * 
 * Rule: Never hardcode endpoint path strings elsewhere. Import from here.
 * Canonical POS surfaces live under /pos/*.
 */
export const API_PATHS = {
    PRODUCT: {
        /** GET - Paginated active products */
        LIST: '/pos',
        /** GET - All active products without pagination */
        ALL: '/pos/all',
        /** GET - Catalog: categories with IDs + products with categoryId */
        CATALOG: '/pos/catalog',
        /** GET - Active products grouped by category */
        ACTIVE: '/pos/active',
        /** GET - All unique category names */
        CATEGORIES: '/pos/categories',
        /** GET - Products filtered by category name */
        CATEGORY: (name: string) => `/pos/category/${encodeURIComponent(name)}`,
        /** GET - Search products by name/category query params */
        SEARCH: '/pos/search',
        /** GET/PUT - Single product by ID */
        BY_ID: (id: string) => `/pos/${id}`,
        /** GET - Product modifier groups (Extra Zutaten) */
        MODIFIER_GROUPS: (id: string) => `/pos/${id}/modifier-groups`,
        /** PUT - Update product stock */
        STOCK: (id: string) => `/pos/stock/${id}`,
        /** GET - Debug: categories and products info */
        DEBUG: '/pos/debug/categories-products',
    },
    // Future: CART, AUTH, INVOICE, etc.
} as const;
