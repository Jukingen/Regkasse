import React, {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
} from 'react';

export type TaxiTripState = {
  running: boolean;
  startedAtUtc: string | null;
  routeFrom: string;
  routeTo: string;
  routeKm: string;
  manualAmount: string;
};

type TaxiTripContextValue = TaxiTripState & {
  startTrip: () => void;
  endTrip: () => void;
  resetTrip: () => void;
  setRouteFrom: (value: string) => void;
  setRouteTo: (value: string) => void;
  setRouteKm: (value: string) => void;
  setManualAmount: (value: string) => void;
};

const EMPTY: TaxiTripState = {
  running: false,
  startedAtUtc: null,
  routeFrom: '',
  routeTo: '',
  routeKm: '',
  manualAmount: '',
};

const TaxiTripContext = createContext<TaxiTripContextValue | undefined>(undefined);

export function TaxiTripProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<TaxiTripState>(EMPTY);

  const startTrip = useCallback(() => {
    setState({
      ...EMPTY,
      running: true,
      startedAtUtc: new Date().toISOString(),
    });
  }, []);

  const endTrip = useCallback(() => {
    setState((current) => ({ ...current, running: false }));
  }, []);

  const resetTrip = useCallback(() => {
    setState(EMPTY);
  }, []);

  const value = useMemo<TaxiTripContextValue>(
    () => ({
      ...state,
      startTrip,
      endTrip,
      resetTrip,
      setRouteFrom: (routeFrom) => setState((current) => ({ ...current, routeFrom })),
      setRouteTo: (routeTo) => setState((current) => ({ ...current, routeTo })),
      setRouteKm: (routeKm) => setState((current) => ({ ...current, routeKm })),
      setManualAmount: (manualAmount) =>
        setState((current) => ({ ...current, manualAmount })),
    }),
    [endTrip, resetTrip, startTrip, state]
  );

  return <TaxiTripContext.Provider value={value}>{children}</TaxiTripContext.Provider>;
}

export function useTaxiTrip(): TaxiTripContextValue {
  const context = useContext(TaxiTripContext);
  if (!context) {
    throw new Error('useTaxiTrip must be used within TaxiTripProvider');
  }
  return context;
}

export function useOptionalTaxiTrip(): TaxiTripContextValue | undefined {
  return useContext(TaxiTripContext);
}

export function parseTaxiKm(value: string): number | null {
  const normalized = value.trim().replace(',', '.');
  if (!normalized) return null;
  const parsed = Number(normalized);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
}

export function parseTaxiAmount(value: string): number | null {
  const normalized = value.trim().replace(',', '.');
  if (!normalized) return null;
  const parsed = Number(normalized);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}
