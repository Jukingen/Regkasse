/**
 * Admin Products list: Lager column, stock action, and low-stock tags.
 * Next.js public env (build-time). See docs/inventory-lager-optional.md and
 * NEXT_PUBLIC_ADMIN_SHOW_INVENTORY_NAV for the sidebar /inventory module.
 */
export function parseAdminProductsLagerUiEnv(raw: string | undefined): boolean {
    if (raw === undefined || raw === '') return true;
    const v = raw.trim().toLowerCase();
    return v !== 'false' && v !== '0' && v !== 'no' && v !== 'off';
}

export function isAdminProductsLagerUiEnabled(): boolean {
    return parseAdminProductsLagerUiEnv(process.env.NEXT_PUBLIC_ADMIN_PRODUCTS_SHOW_LAGER);
}
