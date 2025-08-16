// Türkçe Açıklama: CartItem ve Product tipleri backend ile birebir uyumlu olacak şekilde güncellenmiştir.

export interface Product {
  id: string;
  name: string;
  description: string;
  price: number;
  stockQuantity: number;
  unit: string;
  category: string;
  taxType: 'Standard' | 'Reduced' | 'Special';
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface CartItem {
  id: string;
  product: Product;
  quantity: number;
  unitPrice: number;
  taxRate: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  notes?: string;
  isModified: boolean;
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

// Ödeme iptal yanıt tipi
export interface PaymentCancelResponse {
  success: boolean;
  paymentSessionId: string;
  cartId: string;
  cancelledAt: Date;
  cancelledBy: string;
  cancellationReason: string;
  message: string;
} 