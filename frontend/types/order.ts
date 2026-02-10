// Order ve OrderItem için TypeScript interface'leri
// Backend'deki Order ve OrderItem modelleri ile uyumlu

export interface OrderItem {
  id?: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
  taxAmount: number;
  discountAmount: number;
  totalAmount: number;
  specialNotes?: string;
  productDescription?: string;
  productCategory?: string;
}

export interface Order {
  orderId: string;
  tableNumber: string;
  waiterName: string;
  customerName?: string;
  customerPhone?: string;
  notes?: string;
  orderDate: string;
  status: OrderStatus;
  subtotal: number;
  taxAmount: number;
  discountAmount: number;
  totalAmount: number;
  completedDate?: string;
  cancelledDate?: string;
  cancellationReason?: string;
  cartId?: string;
}

export enum OrderStatus {
  Pending = 'Pending',
  Confirmed = 'Confirmed',
  InProgress = 'InProgress',
  Ready = 'Ready',
  Served = 'Served',
  Completed = 'Completed',
  Cancelled = 'Cancelled'
}

// API Request/Response tipleri
export interface CreateOrderRequest {
  tableNumber: string;
  waiterName: string;
  customerName?: string;
  customerPhone?: string;
  notes?: string;
  items: CreateOrderItemRequest[];
  cartId?: string;
}

export interface CreateOrderItemRequest {
  productId: string;
  quantity: number;
  specialNotes?: string;
}

export interface OrderResponse {
  orderId: string;
  tableNumber: string;
  waiterName: string;
  status: string;
  orderDate: string;
  subtotal: number;
  taxAmount: number;
  discountAmount: number;
  totalAmount: number;
  items: OrderItemResponse[];
  message: string;
}

export interface OrderItemResponse {
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  totalAmount: number;
  specialNotes?: string;
}
