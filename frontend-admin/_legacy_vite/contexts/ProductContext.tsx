import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import api from '../services/api';

export interface Product {
  id: string;
  name: string;
  price: number;
  stock: number;
  image?: string;
  category: string;
  description?: string;
  isFavorite?: boolean;
}

export interface Category {
  id: string;
  name: string;
  color?: string;
}

interface ProductContextType {
  products: Product[];
  categories: Category[];
  selectedCategory: string;
  setSelectedCategory: (cat: string) => void;
  search: string;
  setSearch: (s: string) => void;
  filteredProducts: Product[];
  loading: boolean;
  favorites: Product[];
  toggleFavorite: (productId: string) => void;
}

const ProductContext = createContext<ProductContextType | undefined>(undefined);

export const useProduct = () => {
  const ctx = useContext(ProductContext);
  if (!ctx) throw new Error('useProduct must be used within ProductProvider');
  return ctx;
};

export const ProductProvider = ({ children }: { children: ReactNode }) => {
  const [products, setProducts] = useState<Product[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [favorites, setFavorites] = useState<Product[]>([]);

  useEffect(() => {
    const fetchData = async () => {
      setLoading(true);
      try {
        const [prodRes, catRes] = await Promise.all([
          api.get('/api/products'),
          api.get('/api/categories'),
        ]);
        
        // Favorileri localStorage'dan yükle
        const savedFavorites = localStorage.getItem('favorites');
        const favoriteIds = savedFavorites ? JSON.parse(savedFavorites) : [];
        
        const productsWithFavorites = prodRes.data.map((product: Product) => ({
          ...product,
          isFavorite: favoriteIds.includes(product.id)
        }));
        
        setProducts(productsWithFavorites);
        setCategories([
          { id: 'all', name: 'all' }, 
          { id: 'favorites', name: 'favorites' },
          ...catRes.data
        ]);
        
        // Favori ürünleri ayarla
        setFavorites(productsWithFavorites.filter(p => p.isFavorite));
      } catch (e) {
        setProducts([]);
        setCategories([{ id: 'all', name: 'Tümü' }]);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  const toggleFavorite = (productId: string) => {
    const updatedProducts = products.map(product => 
      product.id === productId 
        ? { ...product, isFavorite: !product.isFavorite }
        : product
    );
    
    setProducts(updatedProducts);
    
    const newFavorites = updatedProducts.filter(p => p.isFavorite);
    setFavorites(newFavorites);
    
    // localStorage'a kaydet
    const favoriteIds = newFavorites.map(p => p.id);
    localStorage.setItem('favorites', JSON.stringify(favoriteIds));
  };

  const filteredProducts = products.filter(p => {
    const categoryMatch = selectedCategory === 'all' || 
                         selectedCategory === 'favorites' ? p.isFavorite : 
                         p.category === selectedCategory;
    const searchMatch = search === '' || p.name.toLowerCase().includes(search.toLowerCase());
    return categoryMatch && searchMatch;
  });

  return (
    <ProductContext.Provider value={{ 
      products, 
      categories, 
      selectedCategory, 
      setSelectedCategory, 
      search, 
      setSearch, 
      filteredProducts, 
      loading,
      favorites,
      toggleFavorite
    }}>
      {children}
    </ProductContext.Provider>
  );
}; 