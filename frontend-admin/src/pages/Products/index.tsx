import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { DataGrid, GridColDef, GridRenderCellParams } from '@mui/x-data-grid';
import { Box, Typography, Paper } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { getProducts, Product } from '@/services/productService';

export default function Products() {
  const { t } = useTranslation();

  const { data, isLoading, error } = useQuery<Product[]>({
    queryKey: ['products'],
    queryFn: getProducts,
  });

  const columns: GridColDef[] = [
    { field: 'name', headerName: t('products.name'), flex: 1 },
    { field: 'code', headerName: t('products.code'), flex: 1 },
    { field: 'category', headerName: t('products.category'), flex: 1 },
    { field: 'price', headerName: t('products.price'), flex: 1, valueFormatter: (params: GridRenderCellParams) => (params.value as number).toFixed(2) },
    { field: 'taxRate', headerName: t('products.taxRate'), flex: 1, valueFormatter: (params: GridRenderCellParams) => (params.value as number) + '%' },
    { field: 'active', headerName: t('common.active'), flex: 1, type: 'boolean' },
  ];

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        {t('navigation.products')}
      </Typography>
      <Paper sx={{ height: 500, width: '100%', mt: 2 }}>
        <DataGrid
          rows={data || []}
          columns={columns}
          getRowId={(row) => row.id}
          loading={isLoading}
          pageSizeOptions={[10, 25, 50]}
          initialState={{ pagination: { paginationModel: { pageSize: 10, page: 0 } } }}
        />
      </Paper>
      {error && (
        <Typography color="error" sx={{ mt: 2 }}>
          {t('errors.serverError')}
        </Typography>
      )}
    </Box>
  );
} 