import { useCallback } from 'react';

import { useAsyncState } from './useAsyncState';
import { useAppState } from '../contexts/AppStateContext';
import { apiClient } from '../services/api/config';
import { Product } from '../services/api/productService';
import { ErrorMessages } from '../services/errorService';

// API fonksiyonları
const productService = {
  getProducts: () => apiClient.get('/api/products').then(res => res.data),
  createProduct: (data: any) => apiClient.post('/api/products', data).then(res => res.data),
  updateProduct: (id: string, data: any) => apiClient.put(`/api/products/${id}`, data).then(res => res.data),
  deleteProduct: (id: string) => apiClient.delete(`/api/products/${id}`).then(res => res.data),
  searchProducts: (query: string) => apiClient.get(`/api/products/search?q=${encodeURIComponent(query)}`).then(res => res.data)
};

export function useProductOperations() {
  const { showError, showSuccess, addNotification } = useAppState();

  // Ürün listesi yükleme
  const [productsState, productsActions] = useAsyncState(
    productService.getProducts,
    {
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
      showErrorAlert: false,
      showSuccessAlert: false,
      onSuccess: (product) => {
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
      showErrorAlert: false,
      showSuccessAlert: false,
      onSuccess: (product) => {
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