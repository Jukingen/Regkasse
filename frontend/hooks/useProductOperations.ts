import { useCallback } from 'react';

import { useAsyncState } from './useAsyncState';
import { useAppState } from '../contexts/AppStateContext';
import { apiClient } from '../services/api/config';
import { Product } from '../services/api/productService';
import { ErrorMessages } from '../services/errorService';

// API fonksiyonları
const productService = {
  getProducts: async () => {
    console.log('🔍 getProducts çağrılıyor...');
    console.log('🔍 API endpoint: /product');
    console.log('🔍 API base URL:', 'http://localhost:5183/api');
    
    try {
      const response = await apiClient.get('/product');
      console.log('✅ API Response:', response);
      console.log('✅ Response type:', typeof response);
      console.log('✅ Response keys:', Object.keys(response));
      
      // API response'unu doğru şekilde parse et
      // Backend'den gelen format: { success: true, message: "...", data: { items: [...], pagination: {...} } }
      if (response.data && response.data.success && response.data.data) {
        console.log('✅ Response parsed successfully');
        console.log('✅ Items count:', response.data.data.items?.length || 0);
        return response.data; // Sadece data kısmını döndür
      } else {
        console.warn('⚠️ Unexpected response format:', response);
        return response; // Fallback
      }
    } catch (error) {
      console.error('❌ API Error:', error);
      throw error;
    }
  },
  createProduct: (data: any) => apiClient.post('/product', data),
  updateProduct: (id: string, data: any) => apiClient.put(`/product/${id}`, data),
  deleteProduct: (id: string) => apiClient.delete(`/product/${id}`),
  searchProducts: (query: string) => apiClient.get(`/product/search?q=${encodeURIComponent(query)}`)
};

export function useProductOperations() {
  const { showError, showSuccess, addNotification } = useAppState();

  // Ürün listesi yükleme - Sadece manuel olarak execute edildiğinde çalışır
  const [productsState, productsActions] = useAsyncState(
    productService.getProducts,
    {
      autoExecute: false, // Otomatik çalışmasın
      showErrorAlert: false,
      onError: (error) => {
        showError(error, 'Products Load Error');
        addNotification({
          type: 'error',
          title: 'Products Load Failed',
          message: error,
          duration: 5000
        });
      }
    }
  );

  // Ürün oluşturma
  const [createState, createActions] = useAsyncState(
    productService.createProduct,
    {
      autoExecute: false, // Otomatik çalışmasın
      showErrorAlert: false,
      showSuccessAlert: false,
      onSuccess: (product: any) => {
        showSuccess('Product created successfully');
        addNotification({
          type: 'success',
          title: 'Product Created',
          message: `${product.name} has been created successfully`,
          duration: 3000
        });
        // Ürün listesini yenile
        productsActions.execute();
      },
      onError: (error) => {
        showError(error, 'Create Product Error');
        addNotification({
          type: 'error',
          title: 'Create Product Failed',
          message: error,
          duration: 5000
        });
      }
    }
  );

  // Ürün güncelleme
  const [updateState, updateActions] = useAsyncState(
    productService.updateProduct,
    {
      autoExecute: false, // Otomatik çalışmasın
      showErrorAlert: false,
      showSuccessAlert: false,
      onSuccess: (product: any) => {
        showSuccess('Product updated successfully');
        addNotification({
          type: 'success',
          title: 'Product Updated',
          message: `${product.name} has been updated successfully`,
          duration: 3000
        });
        // Ürün listesini yenile
        productsActions.execute();
      },
      onError: (error) => {
        showError(error, 'Update Product Error');
        addNotification({
          type: 'error',
          title: 'Update Product Failed',
          message: error,
          duration: 5000
        });
      }
    }
  );

  // Ürün silme
  const [deleteState, deleteActions] = useAsyncState(
    productService.deleteProduct,
    {
      autoExecute: false, // Otomatik çalışmasın
      showErrorAlert: false,
      showSuccessAlert: false,
      onSuccess: () => {
        showSuccess('Product deleted successfully');
        addNotification({
          type: 'success',
          title: 'Product Deleted',
          message: 'Product has been deleted successfully',
          duration: 3000
        });
        // Ürün listesini yenile
        productsActions.execute();
      },
      onError: (error) => {
        showError(error, 'Delete Product Error');
        addNotification({
          type: 'error',
          title: 'Delete Product Failed',
          message: error,
          duration: 5000
        });
      }
    }
  );

  // Ürün arama
  const [searchState, searchActions] = useAsyncState(
    productService.searchProducts,
    {
      autoExecute: false, // Otomatik çalışmasın
      showErrorAlert: false,
      onError: (error) => {
        addNotification({
          type: 'error',
          title: 'Search Failed',
          message: error,
          duration: 3000
        });
      }
    }
  );

  // Ürün oluşturma fonksiyonu
  const createProduct = useCallback(async (productData: any) => {
    return await createActions.execute(productData);
  }, [createActions]);

  // Ürün güncelleme fonksiyonu
  const updateProduct = useCallback(async (id: string, productData: any) => {
    return await updateActions.execute(id, productData);
  }, [updateActions]);

  // Ürün silme fonksiyonu
  const deleteProduct = useCallback(async (id: string) => {
    return await deleteActions.execute(id);
  }, [deleteActions]);

  // Ürün arama fonksiyonu
  const searchProducts = useCallback(async (query: string) => {
    return await searchActions.execute(query);
  }, [searchActions]);

  // Ürün listesini yenileme
  const refreshProducts = useCallback(async () => {
    return await productsActions.execute();
  }, [productsActions]);

  return {
    // States
    products: productsState,
    create: createState,
    update: updateState,
    delete: deleteState,
    search: searchState,
    
    // Actions
    createProduct,
    updateProduct,
    deleteProduct,
    searchProducts,
    refreshProducts,
    
    // Raw actions (for advanced usage)
    productsActions,
    createActions,
    updateActions,
    deleteActions,
    searchActions
  };
} 