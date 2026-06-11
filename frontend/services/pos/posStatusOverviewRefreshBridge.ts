type RefreshFn = (force?: boolean) => Promise<void>;

let refreshImpl: RefreshFn | null = null;

export function registerPosStatusOverviewRefresh(fn: RefreshFn): () => void {
  refreshImpl = fn;
  return () => {
    if (refreshImpl === fn) {
      refreshImpl = null;
    }
  };
}

export function refreshPosStatusOverview(force = false): Promise<void> {
  return refreshImpl?.(force) ?? Promise.resolve();
}
