import React, { createContext, useMemo, useState, type ReactNode } from 'react';

export type TableSlot = {
  isOpen: boolean;
};

export type TableSlotContextValue = {
  slots: Record<number, TableSlot>;
  activeSlot: number;
  setActiveSlot: (slot: number) => void;
  openSlot: (slot: number) => void;
  closeSlot: (slot: number) => void;
};

const defaultSlots: Record<number, TableSlot> = Object.fromEntries(
  Array.from({ length: 9 }, (_, i) => [i + 1, { isOpen: false }])
);

export const TableSlotContext = createContext<TableSlotContextValue>({
  slots: defaultSlots,
  activeSlot: 1,
  setActiveSlot: () => undefined,
  openSlot: () => undefined,
  closeSlot: () => undefined,
});

/** Minimal stub provider for legacy MainScreen / TableDropdown demos. */
export function TableSlotProvider({ children }: { children: ReactNode }) {
  const [activeSlot, setActiveSlot] = useState(1);
  const [slots, setSlots] = useState<Record<number, TableSlot>>(defaultSlots);

  const value = useMemo<TableSlotContextValue>(
    () => ({
      slots,
      activeSlot,
      setActiveSlot,
      openSlot: (slot) =>
        setSlots((prev) => ({ ...prev, [slot]: { ...(prev[slot] ?? { isOpen: false }), isOpen: true } })),
      closeSlot: (slot) =>
        setSlots((prev) => ({
          ...prev,
          [slot]: { ...(prev[slot] ?? { isOpen: false }), isOpen: false },
        })),
    }),
    [slots, activeSlot]
  );

  return <TableSlotContext.Provider value={value}>{children}</TableSlotContext.Provider>;
}
