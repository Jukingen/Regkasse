/**
 * Centralized API endpoint paths — MUST match swagger.json / backend route attributes exactly.
 * 
 * Rule: Never hardcode endpoint path strings elsewhere. Import from here.
 * Backend controller routes use PascalCase (e.g. /Product, /Cart, /Auth).
 * These constants ensure frontend always matches backend casing.
 */
export const API_PATHS = {
    PRODUCT: {
        /** GET - Paginated active products */
        LIST: '/Product',
        /** GET - All active products without pagination */
        ALL: '/Product/all',
        /** GET - Catalog: categories with IDs + products with categoryId */
        CATALOG: '/Product/catalog',
        /** GET - Active products grouped by category */
        ACTIVE: '/Product/active',
        /** GET - All unique category names */
        CATEGORIES: '/Product/categories',
        /** GET - Products filtered by category name */
        CATEGORY: (name: string) => `/Product/category/${encodeURIComponent(name)}`,
        /** GET - Search products by name/category query params */
        SEARCH: '/Product/search',
        /** GET/PUT - Single product by ID */
        BY_ID: (id: string) => `/Product/${id}`,
        /** PUT - Update product stock */
        STOCK: (id: string) => `/Product/stock/${id}`,
        /** GET - Debug: categories and products info */
        DEBUG: '/Product/debug/categories-products',
    },
    // Future: CART, AUTH, INVOICE, etc.
} as const;
