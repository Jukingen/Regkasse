/**
 * Portal target for admin shell header dropdowns (tenant switcher, language, user menu).
 * Renders in document.body to avoid clipping from `.admin-header-toolbar-scroll` overflow.
 */
export function getAdminHeaderPopupContainer(): HTMLElement {
  if (typeof document === 'undefined') {
    return null as unknown as HTMLElement;
  }
  return document.body;
}
