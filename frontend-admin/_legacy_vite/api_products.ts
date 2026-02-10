import apiClient from './client';

export const getProducts = async () => {
    const response = await apiClient.get('/products');
    return response.data;
};

export const getProductById = async (id: string) => {
    const response = await apiClient.get(`/products/${id}`);
    return response.data;
};
