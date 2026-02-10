import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { 
  Box, 
  Typography, 
  Paper, 
  Button, 
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Chip,
  Alert,
  IconButton,
  Tooltip,
  Stack,
  Divider,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  CircularProgress,
  Card,
  CardContent,
  Grid
} from '@mui/material';
import { Add as AddIcon, Edit as EditIcon, Delete as DeleteIcon, Category as CategoryIcon } from '@mui/icons-material';
import { useTranslation } from 'react-i18next';
import { getProducts, createProduct, updateProduct, deleteProduct, Product, CreateProductRequest } from '@/services/productService';
import api from '../../services/api';

// Demo data
const demoProducts = [
  {
    id: 'demo-1',
    name: 'Espresso',
    description: 'Tek shot espresso',
    price: 2.50,
    category: 'Kahve',
    stockQuantity: 150,
    taxType: 'Standard',
    barcode: '1234567890',
    unit: 'Adet',
    isActive: true
  },
  {
    id: 'demo-2',
    name: 'Cappuccino',
    description: 'Espresso ve buharla ısıtılmış süt',
    price: 3.50,
    category: 'Kahve',
    stockQuantity: 25,
    taxType: 'Standard',
    barcode: '1234567891',
    unit: 'Adet',
    isActive: true
  },
  {
    id: 'demo-3',
    name: 'Wiener Schnitzel',
    description: 'Geleneksel Viyana usulü dana eti',
    price: 18.90,
    category: 'Hauptgerichte',
    stockQuantity: 80,
    taxType: 'Standard',
    barcode: '9001234567890',
    unit: 'Stück',
    isActive: true
  },
  {
    id: 'demo-4',
    name: 'Apfelstrudel',
    description: 'Elmalı geleneksel Avusturya tatlısı',
    price: 6.50,
    category: 'Desserts',
    stockQuantity: 45,
    taxType: 'Reduced',
    barcode: '9001234567891',
    unit: 'Stück',
    isActive: true
  }
];

// Kategori yönetimi için ayrı dialog
const CategoryManagementDialog: React.FC<{
  open: boolean;
  onClose: () => void;
  categories: string[];
  onAddCategory: (category: string) => void;
  onDeleteCategory: (category: string) => void;
}> = ({ open, onClose, categories, onAddCategory, onDeleteCategory }) => {
  const { t } = useTranslation();
  const [newCategory, setNewCategory] = useState('');

  const handleAddCategory = () => {
    if (newCategory.trim() && !categories.includes(newCategory.trim())) {
      onAddCategory(newCategory.trim());
      setNewCategory('');
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{t('products.manageCategories')}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Box>
            <Typography variant="subtitle2" gutterBottom>
              {t('products.addNewCategory')}
            </Typography>
            <Stack direction="row" spacing={1}>
              <TextField
                fullWidth
                size="small"
                value={newCategory}
                onChange={(e) => setNewCategory(e.target.value)}
                placeholder={t('products.categoryName')}
                onKeyPress={(e) => e.key === 'Enter' && handleAddCategory()}
              />
              <Button 
                variant="contained" 
                onClick={handleAddCategory}
                disabled={!newCategory.trim()}
              >
                {t('common.add')}
              </Button>
            </Stack>
          </Box>
          
          <Divider />
          
          <Box>
            <Typography variant="subtitle2" gutterBottom>
              {t('products.existingCategories')}
            </Typography>
            <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
              {categories.map((category) => (
                <Chip
                  key={category}
                  label={category}
                  onDelete={() => onDeleteCategory(category)}
                  color="primary"
                  variant="outlined"
                />
              ))}
            </Stack>
          </Box>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{t('common.close')}</Button>
      </DialogActions>
    </Dialog>
  );
};

// Ürün ekleme/düzenleme dialog
const ProductDialog: React.FC<{
  open: boolean;
  onClose: () => void;
  product?: Product;
  categories: string[];
  onSubmit: (data: CreateProductRequest) => void;
  loading: boolean;
}> = ({ open, onClose, product, categories, onSubmit, loading }) => {
  const { t } = useTranslation();
  const [formData, setFormData] = useState<CreateProductRequest>({
    name: '',
    code: '',
    category: '',
    price: 0,
    taxRate: 20,
    description: '',
    active: true
  });

  useEffect(() => {
    if (product) {
      setFormData({
        name: product.name,
        code: product.code,
        category: product.category,
        price: product.price,
        taxRate: product.taxRate,
        description: product.description || '',
        active: product.active
      });
    } else {
      setFormData({
        name: '',
        code: '',
        category: '',
        price: 0,
        taxRate: 20,
        description: '',
        active: true
      });
    }
  }, [product]);

  const handleSubmit = () => {
    if (formData.name && formData.code && formData.category && formData.price > 0) {
      onSubmit(formData);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>
        {product ? t('products.editProduct') : t('products.addProduct')}
      </DialogTitle>
      <DialogContent>
        <Stack spacing={3} sx={{ mt: 1 }}>
          <TextField
            fullWidth
            label={t('products.name')}
            value={formData.name}
            onChange={(e) => setFormData({ ...formData, name: e.target.value })}
            required
          />
          
          <TextField
            fullWidth
            label={t('products.code')}
            value={formData.code}
            onChange={(e) => setFormData({ ...formData, code: e.target.value })}
            required
          />
          
          <FormControl fullWidth required>
            <InputLabel>{t('products.category')}</InputLabel>
            <Select
              value={formData.category}
              onChange={(e) => setFormData({ ...formData, category: e.target.value })}
              label={t('products.category')}
            >
              {categories.map((category) => (
                <MenuItem key={category} value={category}>
                  {category}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          
          <TextField
            fullWidth
            type="number"
            label={t('products.price')}
            value={formData.price}
            onChange={(e) => setFormData({ ...formData, price: parseFloat(e.target.value) || 0 })}
            required
            inputProps={{ min: 0, step: 0.01 }}
          />
          
          <FormControl fullWidth>
            <InputLabel>{t('products.taxRate')}</InputLabel>
            <Select
              value={formData.taxRate}
              onChange={(e) => setFormData({ ...formData, taxRate: e.target.value as number })}
              label={t('products.taxRate')}
            >
              <MenuItem value={20}>20% - {t('tax.standard')}</MenuItem>
              <MenuItem value={10}>10% - {t('tax.reduced')}</MenuItem>
              <MenuItem value={13}>13% - {t('tax.special')}</MenuItem>
            </Select>
          </FormControl>
          
          <TextField
            fullWidth
            multiline
            rows={3}
            label={t('products.description')}
            value={formData.description}
            onChange={(e) => setFormData({ ...formData, description: e.target.value })}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={loading}>
          {t('common.cancel')}
        </Button>
        <Button 
          onClick={handleSubmit} 
          variant="contained" 
          disabled={loading || !formData.name || !formData.code || !formData.category || formData.price <= 0}
        >
          {loading ? t('common.saving') : (product ? t('common.update') : t('common.save'))}
        </Button>
      </DialogActions>
    </Dialog>
  );
};

export default function Products() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  
  // State
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);
  const [productDialogOpen, setProductDialogOpen] = useState(false);
  const [categoryDialogOpen, setCategoryDialogOpen] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [categories, setCategories] = useState<string[]>([]);
  const [products, setProducts] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Queries
  const { data: productsData = [], isLoading, error: queryError } = useQuery<Product[]>({
    queryKey: ['products'],
    queryFn: getProducts,
  });

  // Mutations
  const createMutation = useMutation({
    mutationFn: createProduct,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] });
      setProductDialogOpen(false);
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: number; data: CreateProductRequest }) => 
      updateProduct(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] });
      setProductDialogOpen(false);
      setSelectedProduct(null);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: deleteProduct,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] });
    },
  });

  // Kategorileri güncelle
  useEffect(() => {
    const uniqueCategories = [...new Set(productsData.map(p => p.category))];
    setCategories(uniqueCategories);
  }, [productsData]);

  // Filtrelenmiş ürünler
  const filteredProducts = selectedCategory === 'all' 
    ? productsData 
    : productsData.filter(p => p.category === selectedCategory);

  // Event handlers
  const handleAddProduct = () => {
    setSelectedProduct(null);
    setProductDialogOpen(true);
  };

  const handleEditProduct = (product: Product) => {
    setSelectedProduct(product);
    setProductDialogOpen(true);
  };

  const handleDeleteProduct = (id: number) => {
    if (window.confirm(t('products.confirmDelete'))) {
      deleteMutation.mutate(id);
    }
  };

  const handleSubmitProduct = (data: CreateProductRequest) => {
    if (selectedProduct) {
      updateMutation.mutate({ id: selectedProduct.id, data });
    } else {
      createMutation.mutate(data);
    }
  };

  const handleAddCategory = (category: string) => {
    setCategories(prev => [...prev, category]);
  };

  const handleDeleteCategory = (category: string) => {
    if (window.confirm(t('products.confirmDeleteCategory'))) {
      setCategories(prev => prev.filter(c => c !== category));
    }
  };

  useEffect(() => {
    const fetchProducts = async () => {
      try {
        setLoading(true);
        setError(null);
        
        // API çağrısı - products endpoint'inden veri al
        const response = await api.get('/api/products');
        
        // Eğer veri varsa kullan, yoksa demo data göster
        if (response.data && response.data.length > 0) {
          setProducts(response.data);
        } else {
          setProducts(demoProducts);
        }
      } catch (err) {
        console.error('Products API error:', err);
        // API hatası durumunda demo data göster
        setProducts(demoProducts);
        setError('API bağlantı hatası - Demo veriler gösteriliyor');
      } finally {
        setLoading(false);
      }
    };

    fetchProducts();
  }, []);

  const getTaxTypeColor = (taxType: string) => {
    switch (taxType.toLowerCase()) {
      case 'standard': return 'primary';
      case 'reduced': return 'secondary';
      case 'special': return 'warning';
      default: return 'default';
    }
  };

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="400px">
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h4" component="h1">
          {t('navigation.products')}
        </Typography>
        
        <Stack direction="row" spacing={2}>
          <FormControl size="small" sx={{ minWidth: 150 }}>
            <InputLabel>{t('products.filterByCategory')}</InputLabel>
            <Select
              value={selectedCategory}
              onChange={(e) => setSelectedCategory(e.target.value)}
              label={t('products.filterByCategory')}
            >
              <MenuItem value="all">{t('products.allCategories')}</MenuItem>
              {categories.map((category) => (
                <MenuItem key={category} value={category}>
                  {category}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          
          <Tooltip title={t('products.manageCategories')}>
            <IconButton onClick={() => setCategoryDialogOpen(true)}>
              <CategoryIcon />
            </IconButton>
          </Tooltip>
          
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={handleAddProduct}
          >
            {t('products.addProduct')}
          </Button>
        </Stack>
      </Box>

      {error && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Grid container spacing={3}>
        {products.map((product) => (
          <Grid item xs={12} md={6} lg={4} key={product.id}>
            <Card>
              <CardContent>
                <Box display="flex" justifyContent="space-between" alignItems="flex-start" mb={2}>
                  <Typography variant="h6" gutterBottom>
                    {product.name}
                  </Typography>
                  <Chip 
                    label={product.taxType} 
                    color={getTaxTypeColor(product.taxType) as any}
                    size="small"
                  />
                </Box>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  {product.description}
                </Typography>
                <Typography variant="body1" fontWeight="bold" color="primary">
                  €{product.price?.toFixed(2)}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Category: {product.category}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Stock: {product.stockQuantity} {product.unit}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Barcode: {product.barcode}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Status: {product.isActive ? 'Active' : 'Inactive'}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      {/* Dialogs */}
      <ProductDialog
        open={productDialogOpen}
        onClose={() => {
          setProductDialogOpen(false);
          setSelectedProduct(null);
        }}
        product={selectedProduct || undefined}
        categories={categories}
        onSubmit={handleSubmitProduct}
        loading={createMutation.isPending || updateMutation.isPending}
      />

      <CategoryManagementDialog
        open={categoryDialogOpen}
        onClose={() => setCategoryDialogOpen(false)}
        categories={categories}
        onAddCategory={handleAddCategory}
        onDeleteCategory={handleDeleteCategory}
      />
    </Box>
  );
} 