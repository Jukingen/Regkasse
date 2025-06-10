import React from 'react';
import { Grid, Typography } from '@mui/material';
import { useProduct } from '../contexts/ProductContext';
import ProductCard from './ProductCard';

const ProductGrid: React.FC = () => {
  const { filteredProducts, loading } = useProduct();

  if (loading) return <Typography>Yükleniyor...</Typography>;
  if (filteredProducts.length === 0) return <Typography>Ürün bulunamadı.</Typography>;

  return (
    <Grid container spacing={2}>
      {filteredProducts.map(product => (
        <Grid item xs={6} sm={4} md={3} key={product.id}>
          <ProductCard product={product} />
        </Grid>
      ))}
    </Grid>
  );
};

export default ProductGrid; 