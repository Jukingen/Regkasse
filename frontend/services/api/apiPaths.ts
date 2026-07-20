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
    COMPANY: {
        /** GET - RKSV §8 company header for receipts (tenant-scoped; alias: /pos/company-profile) */
        INFO: '/pos/company',
    },
    PUBLIC_ONLINE_ORDERS: {
        /** GET - Anonymous order status (?tenant=&orderNumber=&phone=) */
        STATUS: '/public/online-orders/status',
    },
    PUBLIC_CUSTOMER: {
        /** GET - Customer portal dashboard (?tenant=&phone=) */
        DASHBOARD: '/public/customer/dashboard',
    },
    PUBLIC_TENANTS: {
        /** GET - Public tenant profile by slug */
        BY_SLUG: (slug: string) => `/public/tenants/${encodeURIComponent(slug)}`,
        /** GET - Live menu for website / customer app */
        MENU: (slug: string) => `/public/tenants/${encodeURIComponent(slug)}/menu`,
    },
} as const;
