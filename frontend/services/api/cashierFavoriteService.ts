import { apiClient } from './config';
import type { Product } from './productService';

export interface CashierFavoriteDto {
  id: string;
  productId: string;
  productName: string;
  productPrice: number;
  sortOrder: number;
}

export interface CashierFavoriteEntry {
  favoriteId: string;
  product: Product;
}

function normalizeDto(raw: Record<string, unknown>): CashierFavoriteDto {
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    productId: String(raw.productId ?? raw.ProductId ?? ''),
    productName: String(raw.productName ?? raw.ProductName ?? ''),
    productPrice: Number(raw.productPrice ?? raw.ProductPrice ?? 0),
    sortOrder: Number(raw.sortOrder ?? raw.SortOrder ?? 0),
  };
}

function toEntry(dto: CashierFavoriteDto): CashierFavoriteEntry {
  return {
    favoriteId: dto.id,
    product: {
      id: dto.productId,
      name: dto.productName,
      price: dto.productPrice,
      category: '',
      stockQuantity: 0,
      isActive: true,
    } as Product,
  };
}

class CashierFavoriteService {
  private baseUrl = '/pos/favorites';

  async listDtos(): Promise<CashierFavoriteDto[]> {
    const rows = await apiClient.get<unknown[]>(this.baseUrl);
    return (rows ?? []).map((row) => normalizeDto(row as Record<string, unknown>));
  }

  async list(): Promise<CashierFavoriteEntry[]> {
    const rows = await this.listDtos();
    return rows.map((dto) => toEntry(dto));
  }

  async add(productId: string): Promise<void> {
    await apiClient.post(this.baseUrl, { productId });
  }

  async remove(favoriteId: string): Promise<void> {
    await apiClient.delete(`${this.baseUrl}/${favoriteId}`);
  }

  async reorder(orderIds: string[]): Promise<void> {
    await apiClient.put(`${this.baseUrl}/reorder`, { orderIds });
  }
}

export const cashierFavoriteService = new CashierFavoriteService();
