import { customInstance } from '@/lib/axios';

export type OnlineOrderItemModifier = {
  id: string;
  modifierId?: string | null;
  name: string;
  price: number;
  quantity: number;
};

export type OnlineOrderItem = {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  price: number;
  total: number;
  modifiers: OnlineOrderItemModifier[];
};

export type OnlineOrderStatusChange = {
  id: string;
  fromStatus: string;
  toStatus: string;
  changedAt: string;
};

export type OnlineOrder = {
  id: string;
  orderNumber: string;
  customerName: string;
  customerPhone: string;
  customerEmail?: string | null;
  orderType: string;
  tableNumber?: string | null;
  deliveryAddress?: string | null;
  subtotal: number;
  tax: number;
  total: number;
  paymentMethod: string;
  paymentStatus: string;
  orderStatus: string;
  source: string;
  createdAt: string;
  acceptedAt?: string | null;
  readyAt?: string | null;
  completedAt?: string | null;
  notes?: string | null;
  posCartId?: string | null;
  customerId?: string | null;
  items: OnlineOrderItem[];
  statusHistory?: OnlineOrderStatusChange[];
};

export type OnlineOrderAnalytics = {
  fromUtc: string;
  toUtc: string;
  totalOrders: number;
  pending: number;
  completed: number;
  cancelled: number;
  revenue: number;
  averageOrderValue: number;
  avgAcceptToReadyMinutes?: number | null;
  byStatus: Record<string, number>;
  bySource: Record<string, number>;
  byOrderType: Record<string, number>;
};

export type OnlineOrderListResult = {
  pending: number;
  accepted: number;
  preparing: number;
  ready: number;
  completed: number;
  orders: OnlineOrder[];
};

export type AcceptOnlineOrderResult = {
  succeeded: boolean;
  code?: string | null;
  error?: string | null;
  posCartId?: string | null;
  alreadyPushed: boolean;
  order?: OnlineOrder | null;
};

type ModifierApi = {
  id?: string;
  Id?: string;
  modifierId?: string | null;
  ModifierId?: string | null;
  name?: string;
  Name?: string;
  price?: number;
  Price?: number;
  quantity?: number;
  Quantity?: number;
};

type ItemApi = {
  id?: string;
  Id?: string;
  productId?: string;
  ProductId?: string;
  productName?: string;
  ProductName?: string;
  quantity?: number;
  Quantity?: number;
  price?: number;
  Price?: number;
  total?: number;
  Total?: number;
  modifiers?: ModifierApi[];
  Modifiers?: ModifierApi[];
};

type OrderApi = {
  id?: string;
  Id?: string;
  orderNumber?: string;
  OrderNumber?: string;
  customerName?: string;
  CustomerName?: string;
  customerPhone?: string;
  CustomerPhone?: string;
  customerEmail?: string | null;
  CustomerEmail?: string | null;
  orderType?: string;
  OrderType?: string;
  tableNumber?: string | null;
  TableNumber?: string | null;
  deliveryAddress?: string | null;
  DeliveryAddress?: string | null;
  subtotal?: number;
  Subtotal?: number;
  tax?: number;
  Tax?: number;
  total?: number;
  Total?: number;
  paymentMethod?: string;
  PaymentMethod?: string;
  paymentStatus?: string;
  PaymentStatus?: string;
  orderStatus?: string;
  OrderStatus?: string;
  source?: string;
  Source?: string;
  createdAt?: string;
  CreatedAt?: string;
  acceptedAt?: string | null;
  AcceptedAt?: string | null;
  readyAt?: string | null;
  ReadyAt?: string | null;
  completedAt?: string | null;
  CompletedAt?: string | null;
  notes?: string | null;
  Notes?: string | null;
  posCartId?: string | null;
  PosCartId?: string | null;
  customerId?: string | null;
  CustomerId?: string | null;
  items?: ItemApi[];
  Items?: ItemApi[];
  statusHistory?: StatusChangeApi[];
  StatusHistory?: StatusChangeApi[];
};

type StatusChangeApi = {
  id?: string;
  Id?: string;
  fromStatus?: string;
  FromStatus?: string;
  toStatus?: string;
  ToStatus?: string;
  changedAt?: string;
  ChangedAt?: string;
};

type ListApi = {
  pending?: number;
  Pending?: number;
  accepted?: number;
  Accepted?: number;
  preparing?: number;
  Preparing?: number;
  ready?: number;
  Ready?: number;
  completed?: number;
  Completed?: number;
  orders?: OrderApi[];
  Orders?: OrderApi[];
};

type AcceptApi = {
  succeeded?: boolean;
  Succeeded?: boolean;
  code?: string | null;
  Code?: string | null;
  error?: string | null;
  Error?: string | null;
  posCartId?: string | null;
  PosCartId?: string | null;
  alreadyPushed?: boolean;
  AlreadyPushed?: boolean;
  order?: OrderApi | null;
  Order?: OrderApi | null;
};

function mapModifier(dto: ModifierApi): OnlineOrderItemModifier {
  return {
    id: dto.id ?? dto.Id ?? '',
    modifierId: dto.modifierId ?? dto.ModifierId,
    name: dto.name ?? dto.Name ?? '',
    price: dto.price ?? dto.Price ?? 0,
    quantity: dto.quantity ?? dto.Quantity ?? 1,
  };
}

function mapItem(dto: ItemApi): OnlineOrderItem {
  const mods = dto.modifiers ?? dto.Modifiers ?? [];
  return {
    id: dto.id ?? dto.Id ?? '',
    productId: dto.productId ?? dto.ProductId ?? '',
    productName: dto.productName ?? dto.ProductName ?? '',
    quantity: dto.quantity ?? dto.Quantity ?? 0,
    price: dto.price ?? dto.Price ?? 0,
    total: dto.total ?? dto.Total ?? 0,
    modifiers: mods.map(mapModifier),
  };
}

function mapStatusChange(dto: StatusChangeApi): OnlineOrderStatusChange {
  return {
    id: dto.id ?? dto.Id ?? '',
    fromStatus: dto.fromStatus ?? dto.FromStatus ?? '',
    toStatus: dto.toStatus ?? dto.ToStatus ?? '',
    changedAt: dto.changedAt ?? dto.ChangedAt ?? '',
  };
}

function mapOrder(dto: OrderApi): OnlineOrder {
  const items = dto.items ?? dto.Items ?? [];
  const history = dto.statusHistory ?? dto.StatusHistory ?? [];
  return {
    id: dto.id ?? dto.Id ?? '',
    orderNumber: dto.orderNumber ?? dto.OrderNumber ?? '',
    customerName: dto.customerName ?? dto.CustomerName ?? '',
    customerPhone: dto.customerPhone ?? dto.CustomerPhone ?? '',
    customerEmail: dto.customerEmail ?? dto.CustomerEmail,
    orderType: dto.orderType ?? dto.OrderType ?? '',
    tableNumber: dto.tableNumber ?? dto.TableNumber,
    deliveryAddress: dto.deliveryAddress ?? dto.DeliveryAddress,
    subtotal: dto.subtotal ?? dto.Subtotal ?? 0,
    tax: dto.tax ?? dto.Tax ?? 0,
    total: dto.total ?? dto.Total ?? 0,
    paymentMethod: dto.paymentMethod ?? dto.PaymentMethod ?? '',
    paymentStatus: dto.paymentStatus ?? dto.PaymentStatus ?? '',
    orderStatus: dto.orderStatus ?? dto.OrderStatus ?? '',
    source: dto.source ?? dto.Source ?? '',
    createdAt: dto.createdAt ?? dto.CreatedAt ?? '',
    acceptedAt: dto.acceptedAt ?? dto.AcceptedAt,
    readyAt: dto.readyAt ?? dto.ReadyAt,
    completedAt: dto.completedAt ?? dto.CompletedAt,
    notes: dto.notes ?? dto.Notes,
    posCartId: dto.posCartId ?? dto.PosCartId,
    customerId: dto.customerId ?? dto.CustomerId,
    items: items.map(mapItem),
    statusHistory: history.map(mapStatusChange).filter((h) => h.id.length > 0),
  };
}

export async function fetchOnlineOrders(status?: string): Promise<OnlineOrderListResult> {
  const res = await customInstance<ListApi>({
    url: '/api/admin/online-orders',
    method: 'GET',
    params: status ? { status } : undefined,
  });
  const orders = (res?.orders ?? res?.Orders ?? []).map(mapOrder).filter((o) => o.id.length > 0);
  return {
    pending: res?.pending ?? res?.Pending ?? 0,
    accepted: res?.accepted ?? res?.Accepted ?? 0,
    preparing: res?.preparing ?? res?.Preparing ?? 0,
    ready: res?.ready ?? res?.Ready ?? 0,
    completed: res?.completed ?? res?.Completed ?? 0,
    orders,
  };
}

export async function acceptOnlineOrder(id: string): Promise<AcceptOnlineOrderResult> {
  const res = await customInstance<AcceptApi>({
    url: `/api/admin/online-orders/${id}/accept`,
    method: 'POST',
  });
  const orderDto = res?.order ?? res?.Order;
  return {
    succeeded: res?.succeeded ?? res?.Succeeded ?? false,
    code: res?.code ?? res?.Code,
    error: res?.error ?? res?.Error,
    posCartId: res?.posCartId ?? res?.PosCartId,
    alreadyPushed: res?.alreadyPushed ?? res?.AlreadyPushed ?? false,
    order: orderDto ? mapOrder(orderDto) : null,
  };
}

export type UpdateOnlineOrderStatusResult = {
  succeeded: boolean;
  code?: string;
  error?: string;
  order?: OnlineOrder | null;
};

type StatusUpdateApi = {
  succeeded?: boolean;
  Succeeded?: boolean;
  code?: string;
  Code?: string;
  error?: string;
  Error?: string;
  order?: OrderApi | null;
  Order?: OrderApi | null;
};

export async function updateOnlineOrderStatus(
  id: string,
  status: string
): Promise<UpdateOnlineOrderStatusResult> {
  const res = await customInstance<StatusUpdateApi>({
    url: `/api/admin/online-orders/${id}/status`,
    method: 'PATCH',
    data: { status },
  });
  const orderDto = res?.order ?? res?.Order;
  return {
    succeeded: res?.succeeded ?? res?.Succeeded ?? false,
    code: res?.code ?? res?.Code,
    error: res?.error ?? res?.Error,
    order: orderDto ? mapOrder(orderDto) : null,
  };
}

type AnalyticsApi = {
  fromUtc?: string;
  FromUtc?: string;
  toUtc?: string;
  ToUtc?: string;
  totalOrders?: number;
  TotalOrders?: number;
  pending?: number;
  Pending?: number;
  completed?: number;
  Completed?: number;
  cancelled?: number;
  Cancelled?: number;
  revenue?: number;
  Revenue?: number;
  averageOrderValue?: number;
  AverageOrderValue?: number;
  avgAcceptToReadyMinutes?: number | null;
  AvgAcceptToReadyMinutes?: number | null;
  byStatus?: Record<string, number>;
  ByStatus?: Record<string, number>;
  bySource?: Record<string, number>;
  BySource?: Record<string, number>;
  byOrderType?: Record<string, number>;
  ByOrderType?: Record<string, number>;
};

export async function fetchOnlineOrderAnalytics(): Promise<OnlineOrderAnalytics> {
  const res = await customInstance<AnalyticsApi>({
    url: '/api/admin/online-orders/analytics',
    method: 'GET',
  });
  return {
    fromUtc: res?.fromUtc ?? res?.FromUtc ?? '',
    toUtc: res?.toUtc ?? res?.ToUtc ?? '',
    totalOrders: res?.totalOrders ?? res?.TotalOrders ?? 0,
    pending: res?.pending ?? res?.Pending ?? 0,
    completed: res?.completed ?? res?.Completed ?? 0,
    cancelled: res?.cancelled ?? res?.Cancelled ?? 0,
    revenue: res?.revenue ?? res?.Revenue ?? 0,
    averageOrderValue: res?.averageOrderValue ?? res?.AverageOrderValue ?? 0,
    avgAcceptToReadyMinutes: res?.avgAcceptToReadyMinutes ?? res?.AvgAcceptToReadyMinutes ?? null,
    byStatus: res?.byStatus ?? res?.ByStatus ?? {},
    bySource: res?.bySource ?? res?.BySource ?? {},
    byOrderType: res?.byOrderType ?? res?.ByOrderType ?? {},
  };
}

export async function fetchOnlineOrderById(id: string): Promise<OnlineOrder | null> {
  const res = await customInstance<OrderApi>({
    url: `/api/admin/online-orders/${id}`,
    method: 'GET',
  });
  const mapped = mapOrder(res);
  return mapped.id ? mapped : null;
}
