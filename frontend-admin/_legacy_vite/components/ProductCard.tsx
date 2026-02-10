import React from 'react';
import { Card, CardContent, Typography, CardActions, Button, Box, Chip, Avatar, IconButton } from '@mui/material';
import { 
  Add as AddIcon, 
  LocalOffer as CategoryIcon,
  Favorite as FavoriteIcon,
  FavoriteBorder as FavoriteBorderIcon
} from '@mui/icons-material';
import { Product, useProduct } from '../contexts/ProductContext';
import { useCart } from '../contexts/CartContext';

const ProductCard: React.FC<{ product: Product }> = ({ product }) => {
  const { addToCart } = useCart();
  const { toggleFavorite } = useProduct();

  // Kategori renkleri
  const getCategoryColor = (category: string) => {
    const colors: { [key: string]: string } = {
      'yiyecek': '#4caf50',
      'içecek': '#2196f3',
      'alkollü': '#f44336',
      'alkolsüz': '#ff9800',
      'tatlı': '#9c27b0',
      'kahve': '#795548'
    };
    return colors[category.toLowerCase()] || '#757575';
  };

  return (
    <Card 
      sx={{ 
        minHeight: 200, 
        display: 'flex', 
        flexDirection: 'column', 
        justifyContent: 'space-between',
        transition: 'all 0.3s ease',
        '&:hover': {
          transform: 'translateY(-4px)',
          boxShadow: 4
        },
        position: 'relative',
        overflow: 'visible'
      }}
    >
      {/* Kategori etiketi */}
      <Box sx={{ position: 'absolute', top: 8, right: 8, zIndex: 1 }}>
        <Chip
          icon={<CategoryIcon />}
          label={product.category}
          size="small"
          sx={{
            bgcolor: getCategoryColor(product.category),
            color: 'white',
            fontWeight: 600
          }}
        />
      </Box>

      {/* Favori butonu */}
      <Box sx={{ position: 'absolute', top: 8, left: 8, zIndex: 1 }}>
        <IconButton
          size="small"
          onClick={() => toggleFavorite(product.id)}
          sx={{
            bgcolor: 'rgba(255,255,255,0.9)',
            '&:hover': {
              bgcolor: 'rgba(255,255,255,1)',
            }
          }}
        >
          {product.isFavorite ? (
            <FavoriteIcon sx={{ color: 'error.main' }} />
          ) : (
            <FavoriteBorderIcon />
          )}
        </IconButton>
      </Box>

      {/* Ürün resmi */}
      <Box sx={{ height: 120, bgcolor: 'grey.100', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        {product.image ? (
          <img 
            src={product.image} 
            alt={product.name}
            style={{ width: '100%', height: '100%', objectFit: 'cover' }}
          />
        ) : (
          <Avatar sx={{ width: 60, height: 60, bgcolor: 'primary.main' }}>
            {product.name.charAt(0).toUpperCase()}
          </Avatar>
        )}
      </Box>

      <CardContent sx={{ flexGrow: 1, pb: 1 }}>
        <Typography 
          variant="subtitle1" 
          fontWeight={700} 
          sx={{ 
            fontSize: '0.9rem',
            lineHeight: 1.2,
            mb: 1,
            height: 40,
            overflow: 'hidden',
            display: '-webkit-box',
            WebkitLineClamp: 2,
            WebkitBoxOrient: 'vertical'
          }}
        >
          {product.name}
        </Typography>
        
        <Typography 
          variant="h6" 
          color="primary" 
          fontWeight={700}
          sx={{ fontSize: '1.1rem' }}
        >
          {product.price.toFixed(2)} €
        </Typography>
        
        <Box sx={{ display: 'flex', alignItems: 'center', mt: 1 }}>
          <Box
            sx={{
              width: 8,
              height: 8,
              borderRadius: '50%',
              bgcolor: product.stock > 0 ? 'success.main' : 'error.main',
              mr: 1
            }}
          />
          <Typography 
            variant="caption" 
            color={product.stock > 0 ? 'success.main' : 'error.main'}
            fontWeight={600}
          >
            {product.stock > 0 ? `Stok: ${product.stock}` : 'Stokta yok'}
          </Typography>
        </Box>
      </CardContent>

      <CardActions sx={{ p: 1.5 }}>
        <Button
          fullWidth
          variant="contained"
          color="primary"
          size="small"
          startIcon={<AddIcon />}
          onClick={() => addToCart(product)}
          disabled={product.stock === 0}
          sx={{
            fontWeight: 600,
            textTransform: 'none',
            borderRadius: 2
          }}
        >
          Sepete Ekle
        </Button>
      </CardActions>
    </Card>
  );
};

export default ProductCard; 