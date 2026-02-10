// Wrapper to replace Zustand with Context API
// keeping the same API surface for the rest of the app.

import { useCart, CartItem, Cart, CartsByTable } from '../contexts/CartContext';

// Re-export types so imports don't break
export type { CartItem, Cart, CartsByTable };

// Re-export the hook as useCartStore
export const useCartStore = useCart;
