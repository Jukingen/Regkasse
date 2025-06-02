import { useCallback, useRef } from 'react';

export function useMemoizedCallback<T extends (...args: any[]) => any>(
  callback: T,
  deps: any[]
): T {
  const ref = useRef<{
    deps: any[];
    callback: T;
    memoized: T;
  }>();

  if (!ref.current || !deps.every((dep, i) => ref.current.deps[i] === dep)) {
    ref.current = {
      deps,
      callback,
      memoized: ((...args: any[]) => ref.current!.callback(...args)) as T,
    };
  }

  return ref.current.memoized;
} 