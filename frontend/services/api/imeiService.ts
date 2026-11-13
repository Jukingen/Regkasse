import { apiClient } from './config';
import { API_PATHS } from './apiPaths';

export type ProductImeiStatus = 'InStock' | 'Sold' | 'Returned';

export type ProductImeiDto = {
  id: string;
  productId: string;
  imei: string;
  status: ProductImeiStatus;
  soldPaymentId?: string | null;
  warrantyMonths: number;
  createdAtUtc: string;
  soldAtUtc?: string | null;
};

export type AddProductImeiRequest = {
  imei: string;
  warrantyMonths?: number;
};

function mapImei(raw: Record<string, unknown>): ProductImeiDto {
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    productId: String(raw.productId ?? raw.ProductId ?? ''),
    imei: String(raw.imei ?? raw.Imei ?? ''),
    status: (raw.status ?? raw.Status ?? 'InStock') as ProductImeiStatus,
    soldPaymentId: (raw.soldPaymentId ?? raw.SoldPaymentId ?? null) as string | null,
    warrantyMonths: Number(raw.warrantyMonths ?? raw.WarrantyMonths ?? 0),
    createdAtUtc: String(raw.createdAtUtc ?? raw.CreatedAtUtc ?? ''),
    soldAtUtc: (raw.soldAtUtc ?? raw.SoldAtUtc ?? null) as string | null,
  };
}

export async function listProductImeis(
  productId: string,
  status: ProductImeiStatus = 'InStock'
): Promise<ProductImeiDto[]> {
  const payload = await apiClient.get<unknown>(API_PATHS.PRODUCT.IMEIS(productId), {
    params: { status },
  });
  const wrapped = payload !== null && typeof payload === 'object' ? (payload as { data?: unknown }).data : undefined;
  const rows = Array.isArray(payload) ? payload : Array.isArray(wrapped) ? wrapped : [];
  return rows.map((row: Record<string, unknown>) => mapImei(row));
}

export async function addProductImei(
  productId: string,
  body: AddProductImeiRequest
): Promise<ProductImeiDto> {
  const response = await apiClient.post<unknown>(API_PATHS.PRODUCT.IMEIS(productId), body);
  const wrapped =
    response !== null && typeof response === 'object' ? (response as { data?: unknown }).data : undefined;
  const payload = wrapped !== undefined && wrapped !== null ? wrapped : response;
  return mapImei(payload as Record<string, unknown>);
}

export function productRequiresImeiPicker(product: { imeiTracked?: boolean | null }): boolean {
  return product.imeiTracked === true;
}
