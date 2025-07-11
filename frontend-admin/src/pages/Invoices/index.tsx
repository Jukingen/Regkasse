import { useEffect, useState } from 'react';
import { Typography, CircularProgress, Alert, Box, Card, CardContent, Grid, Chip } from '@mui/material';
import { useTranslation } from 'react-i18next';
import api from '../../services/api';

export default function Invoices() {
  const { t } = useTranslation();
  const [invoices, setInvoices] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchInvoices = async () => {
      try {
        setLoading(true);
        setError(null);
        
        // API çağrısı - invoices endpoint'inden veri al
        const response = await api.get('/api/invoices');
        
        if (response.data && response.data.length > 0) {
          setInvoices(response.data);
        } else {
          setInvoices([]);
        }
      } catch (err) {
        console.error('Invoices API error:', err);
        setInvoices([]);
        setError('API bağlantı hatası - Veriler yüklenemedi');
      } finally {
        setLoading(false);
      }
    };

    fetchInvoices();
  }, []);

  const getPaymentStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'paid': return 'success';
      case 'pending': return 'warning';
      case 'overdue': return 'error';
      default: return 'default';
    }
  };

  const getPaymentMethodColor = (method: string) => {
    switch (method.toLowerCase()) {
      case 'card': return 'primary';
      case 'cash': return 'secondary';
      case 'voucher': return 'info';
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

  if (error) {
    return (
      <Box>
        <Typography variant="h4" component="h1" gutterBottom>
          {t('navigation.invoices')}
        </Typography>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      </Box>
    );
  }

  if (invoices.length === 0) {
    return (
      <Box>
        <Typography variant="h4" component="h1" gutterBottom>
          {t('navigation.invoices')}
        </Typography>
        <Alert severity="info" sx={{ mb: 2 }}>
          Henüz fatura verisi bulunmuyor.
        </Alert>
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        {t('navigation.invoices')}
      </Typography>

      <Grid container spacing={3}>
        {invoices.map((invoice) => (
          <Grid item xs={12} md={6} lg={4} key={invoice.id}>
            <Card>
              <CardContent>
                <Box display="flex" justifyContent="space-between" alignItems="flex-start" mb={2}>
                  <Typography variant="h6" gutterBottom>
                    {invoice.receiptNumber}
                  </Typography>
                  <Box>
                    <Chip 
                      label={invoice.paymentStatus} 
                      color={getPaymentStatusColor(invoice.paymentStatus) as any}
                      size="small"
                      sx={{ mr: 1 }}
                    />
                    <Chip 
                      label={invoice.paymentMethod} 
                      color={getPaymentMethodColor(invoice.paymentMethod) as any}
                      size="small"
                    />
                  </Box>
                </Box>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Customer: {invoice.customer?.firstName} {invoice.customer?.lastName}
                </Typography>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Date: {new Date(invoice.invoiceDate).toLocaleDateString()}
                </Typography>
                <Typography variant="body1" fontWeight="bold" color="primary">
                  Total: €{invoice.totalAmount?.toFixed(2)}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Tax: €{invoice.taxAmount?.toFixed(2)}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Status: {invoice.status}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Printed: {invoice.isPrinted ? 'Yes' : 'No'}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  TSE: {invoice.tseSignature?.substring(0, 20)}...
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
    </Box>
  );
} 