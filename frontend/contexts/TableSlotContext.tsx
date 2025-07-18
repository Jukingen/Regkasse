// Bu context, her masa/satış slotu için ayrı sepet ve hesap yönetimi sağlar. Slotlar arası geçiş ve aç/kapat işlemleri burada yönetilir.
import React, { createContext, useState } from 'react';

// Sepet yönetimi fonksiyonları: ürün ekle, miktar artır/azalt, ürün sil, sepeti temizle
export type CartItem = {
  productId: string;
  name: string;
  price: number;
  quantity: number;
  taxType: 'standard' | 'reduced' | 'special';
};

export type Slot = {
  cart: CartItem[];
  isOpen: boolean;
  bill: number;
};

type TableSlotContextType = {
  slots: Record<number, Slot>;
  activeSlot: number;
  setActiveSlot: (slot: number) => void;
  openSlot: (slot: number) => void;
  closeSlot: (slot: number) => void;
  addToCart: (slot: number, item: CartItem) => void;
  increaseQuantity: (slot: number, productId: string) => void;
  decreaseQuantity: (slot: number, productId: string) => void;
  removeFromCart: (slot: number, productId: string) => void;
  clearCart: (slot: number) => void;
  getCartTotal: (slot: number) => number;
};

const defaultSlots: Record<number, Slot> = {};
for (let i = 1; i <= 9; i++) {
  defaultSlots[i] = { cart: [], isOpen: false, bill: 0 };
}

export const TableSlotContext = createContext<TableSlotContextType>({
  slots: defaultSlots,
  activeSlot: 1,
  setActiveSlot: () => {},
  openSlot: () => {},
  closeSlot: () => {},
  addToCart: () => {},
  increaseQuantity: () => {},
  decreaseQuantity: () => {},
  removeFromCart: () => {},
  clearCart: () => {},
  getCartTotal: () => 0,
});

export const TableSlotProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [slots, setSlots] = useState<Record<number, Slot>>(defaultSlots);
  const [activeSlot, setActiveSlot] = useState<number>(1);

  const openSlot = (slot: number) => {
    setSlots(prev => ({
      ...prev,
      [slot]: { ...prev[slot], isOpen: true },
    }));
  };

  const closeSlot = (slot: number) => {
    setSlots(prev => ({
      ...prev,
      [slot]: { ...prev[slot], isOpen: false, cart: [], bill: 0 },
    }));
  };

  // Ürün ekle: Aynı ürün varsa miktarı artır, yoksa ekle
  const addToCart = (slot: number, item: CartItem) => {
    setSlots(prev => {
      const cart = prev[slot].cart;
      const existing = cart.find(ci => ci.productId === item.productId);
      let newCart;
      if (existing) {
        newCart = cart.map(ci =>
          ci.productId === item.productId
            ? { ...ci, quantity: ci.quantity + 1 }
            : ci
        );
      } else {
        newCart = [...cart, { ...item, quantity: 1 }];
      }
      return {
        ...prev,
        [slot]: { ...prev[slot], cart: newCart },
      };
    });
  };

  // Miktar artır
  const increaseQuantity = (slot: number, productId: string) => {
    setSlots(prev => {
      const cart = prev[slot].cart.map(ci =>
        ci.productId === productId ? { ...ci, quantity: ci.quantity + 1 } : ci
      );
      return {
        ...prev,
        [slot]: { ...prev[slot], cart },
      };
    });
  };

  // Miktar azalt (0 olursa ürünü sil)
  const decreaseQuantity = (slot: number, productId: string) => {
    setSlots(prev => {
      let cart = prev[slot].cart.map(ci =>
        ci.productId === productId ? { ...ci, quantity: ci.quantity - 1 } : ci
      );
      cart = cart.filter(ci => ci.quantity > 0);
      return {
        ...prev,
        [slot]: { ...prev[slot], cart },
      };
    });
  };

  // Ürün sil
  const removeFromCart = (slot: number, productId: string) => {
    setSlots(prev => {
      const cart = prev[slot].cart.filter(ci => ci.productId !== productId);
      return {
        ...prev,
        [slot]: { ...prev[slot], cart },
      };
    });
  };

  // Sepeti temizle
  const clearCart = (slot: number) => {
    setSlots(prev => ({
      ...prev,
      [slot]: { ...prev[slot], cart: [] },
    }));
  };

  // Sepet toplamı
  const getCartTotal = (slot: number) => {
    return slots[slot]?.cart.reduce((sum, item) => sum + item.price * item.quantity, 0) || 0;
  };

  return (
    <TableSlotContext.Provider
      value={{
        slots,
        activeSlot,
        setActiveSlot,
        openSlot,
        closeSlot,
        addToCart,
        increaseQuantity,
        decreaseQuantity,
        removeFromCart,
        clearCart,
        getCartTotal,
      }}
    >
      {children}
    </TableSlotContext.Provider>
  );
}; 