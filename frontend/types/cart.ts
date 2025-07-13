import { Product } from '../services/api/productService';

export interface CartItem {
  product: Product;
  quantity: number;
  notes?: string;
  discount?: number;
}

export interface Order {
  id: string;
  items: CartItem[];
  customer?: any;
  customerName?: string;
  tableNumber?: string;
  notes?: string;
  status: 'pending' | 'preparing' | 'ready' | 'served' | 'cancelled';
  createdAt: Date;
} 