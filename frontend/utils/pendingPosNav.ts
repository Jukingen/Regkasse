/** One-shot flags for POS navigation side effects (e.g. open merge sheet after split screen). */
let pendingMergeSheet = false;

export function requestMergeSheet(): void {
  pendingMergeSheet = true;
}

export function consumeMergeSheetRequest(): boolean {
  const value = pendingMergeSheet;
  pendingMergeSheet = false;
  return value;
}
