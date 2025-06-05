import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { DataGrid, GridColDef } from '@mui/x-data-grid';
import { Box, Typography, Paper } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { getInvoices, Invoice } from '../../services/invoiceService';

export default function Invoices() {
  const { t } = useTranslation();

  const { data, isLoading, error } = useQuery<Invoice[]>({
    queryKey: ['invoices'],
    queryFn: getInvoices,
  });

  const columns: GridColDef[] = [
    { field: 'receiptNumber', headerName: t('invoices.invoiceNumber'), flex: 1 },
    { field: 'createdAt', headerName: t('common.date'), flex: 1, valueGetter: (params) => new Date(params.value).toLocaleString() },
    { field: 'customerId', headerName: t('customers.customerNumber'), flex: 1 },
    { field: 'paymentMethod', headerName: t('orders.paymentMethod'), flex: 1 },
    { field: 'status', headerName: t('common.status'), flex: 1 },
    { field: 'discountAmount', headerName: t('invoices.invoiceTotal'), flex: 1 },
  ];

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        {t('navigation.invoices')}
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