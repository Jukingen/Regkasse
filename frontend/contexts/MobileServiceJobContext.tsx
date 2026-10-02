import React, { createContext, useContext, useMemo, useState } from 'react';

export interface ServiceAddress {
  street: string;
  postalCode: string;
  city: string;
  notes: string;
}

const EMPTY_ADDRESS: ServiceAddress = {
  street: '',
  postalCode: '',
  city: '',
  notes: '',
};

interface MobileServiceJobContextValue {
  customerAddress: ServiceAddress;
  jobLocation: ServiceAddress;
  setCustomerAddress: (value: ServiceAddress) => void;
  setJobLocation: (value: ServiceAddress) => void;
  reset: () => void;
}

const MobileServiceJobContext = createContext<MobileServiceJobContextValue | undefined>(undefined);

export function emptyServiceAddress(): ServiceAddress {
  return { ...EMPTY_ADDRESS };
}

export function hasServiceAddress(value: ServiceAddress): boolean {
  return Boolean(
    value.street.trim() || value.postalCode.trim() || value.city.trim() || value.notes.trim()
  );
}

export function toAddressPayload(value: ServiceAddress) {
  if (!hasServiceAddress(value)) return null;
  return {
    street: value.street.trim() || null,
    postalCode: value.postalCode.trim() || null,
    city: value.city.trim() || null,
    notes: value.notes.trim() || null,
  };
}

export function formatServiceAddress(value?: ServiceAddress | null): string {
  if (!value) return '';
  const line1 = value.street.trim();
  const line2 = [value.postalCode, value.city].map((part) => part.trim()).filter(Boolean).join(' ');
  return [line1, line2].filter(Boolean).join(', ');
}

export function MobileServiceJobProvider({ children }: { children: React.ReactNode }) {
  const [customerAddress, setCustomerAddress] = useState<ServiceAddress>(emptyServiceAddress);
  const [jobLocation, setJobLocation] = useState<ServiceAddress>(emptyServiceAddress);

  const value = useMemo<MobileServiceJobContextValue>(
    () => ({
      customerAddress,
      jobLocation,
      setCustomerAddress,
      setJobLocation,
      reset: () => {
        setCustomerAddress(emptyServiceAddress());
        setJobLocation(emptyServiceAddress());
      },
    }),
    [customerAddress, jobLocation]
  );

  return (
    <MobileServiceJobContext.Provider value={value}>{children}</MobileServiceJobContext.Provider>
  );
}

export function useMobileServiceJob(): MobileServiceJobContextValue {
  const context = useContext(MobileServiceJobContext);
  if (!context) {
    throw new Error('useMobileServiceJob must be used within MobileServiceJobProvider');
  }
  return context;
}
