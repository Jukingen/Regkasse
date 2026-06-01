/**
 * @deprecated Use `useAntdApp()` from `@/hooks/useAntdApp` in React components/hooks.
 * Non-React code (axios interceptors, query client) should use `showAntdError` from `@/lib/antdAppBridge`.
 */
export { appMessage as message, appModal as modal } from '@/lib/antdAppBridge';
