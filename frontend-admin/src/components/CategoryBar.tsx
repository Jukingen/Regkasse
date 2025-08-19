import React from 'react';
import { Box, Chip, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { useProduct } from '../contexts/ProductContext';

const CategoryBar: React.FC = () => {
  const { categories, selectedCategory, setSelectedCategory } = useProduct();
  const { t } = useTranslation();

  // Kategori renkleri
  const getCategoryColor = (categoryName: string) => {
    const colors: { [key: string]: string } = {
      'tümü': '#757575',
      'yiyecek': '#4caf50',
      'içecek': '#2196f3',
      'alkollü': '#f44336',
      'alkolsüz': '#ff9800',
      'tatlı': '#9c27b0',
      'kahve': '#795548'
    };
    return colors[categoryName.toLowerCase()] || '#757575';
  };

  return (
    <Box sx={{ mb: 2 }}>
      <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1, fontWeight: 600 }}>
        {t('categories.title', 'Kategoriler')}
      </Typography>
      <Box sx={{ 
        display: 'flex', 
        gap: 1,
        overflowX: 'auto', 
        pb: 1,
        '&::-webkit-scrollbar': {
          height: 4,
        },
        '&::-webkit-scrollbar-track': {
          bgcolor: 'grey.100',
          borderRadius: 2,
        },
        '&::-webkit-scrollbar-thumb': {
          bgcolor: 'grey.400',
          borderRadius: 2,
        }
      }}>
        {categories.map(cat => (
          <Chip
            key={cat.id}
            label={cat.id === 'all' ? t('categories.all') : cat.id === 'favorites' ? t('categories.favorites') : t(`categories.${cat.name}`)}
            color={selectedCategory === cat.id ? 'primary' : 'default'}
            onClick={() => setSelectedCategory(cat.id)}
            sx={{ 
              minWidth: 100,
              fontWeight: selectedCategory === cat.id ? 700 : 500,
              bgcolor: selectedCategory === cat.id 
                ? 'primary.main' 
                : getCategoryColor(cat.name),
              color: selectedCategory === cat.id ? 'white' : 'white',
              '&:hover': {
                bgcolor: selectedCategory === cat.id 
                  ? 'primary.dark' 
                  : getCategoryColor(cat.name),
                transform: 'translateY(-1px)',
                boxShadow: 2
              },
              transition: 'all 0.2s ease',
              cursor: 'pointer'
            }}
          />
        ))}
      </Box>
    </Box>
  );
};

export default CategoryBar; 