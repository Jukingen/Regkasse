import { useEffect, useState } from 'react';
import { Typography, CircularProgress, Alert, Box, Card, CardContent, Grid, Chip } from '@mui/material';
import { useTranslation } from 'react-i18next';
import api from '../../services/api';

export default function Customers() {
  const { t } = useTranslation();
  const [customers, setCustomers] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchCustomers = async () => {
      try {
        setLoading(true);
        setError(null);
        
        // API çağrısı - customers endpoint'inden veri al
        const response = await api.get('/api/customers');
        
        if (response.data && response.data.length > 0) {
          setCustomers(response.data);
        } else {
          setCustomers([]);
        }
      } catch (err) {
        console.error('Customers API error:', err);
        setCustomers([]);
        setError('API bağlantı hatası - Veriler yüklenemedi');
      } finally {
        setLoading(false);
      }
    };

    fetchCustomers();
  }, []);

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
          {t('navigation.customers')}
        </Typography>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      </Box>
    );
  }

  if (customers.length === 0) {
    return (
      <Box>
        <Typography variant="h4" component="h1" gutterBottom>
          {t('navigation.customers')}
        </Typography>
        <Alert severity="info" sx={{ mb: 2 }}>
          Henüz müşteri verisi bulunmuyor.
        </Alert>
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        {t('navigation.customers')}
      </Typography>

      <Grid container spacing={3}>
        {customers.map((customer) => (
          <Grid item xs={12} md={6} lg={4} key={customer.id}>
            <Card>
              <CardContent>
                <Box display="flex" justifyContent="space-between" alignItems="flex-start" mb={2}>
                  <Typography variant="h6" gutterBottom>
                    {customer.firstName} {customer.lastName}
                  </Typography>
                  <Chip 
                    label={customer.isActive ? 'Active' : 'Inactive'} 
                    color={customer.isActive ? 'success' : 'default'}
                    size="small"
                  />
                </Box>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Customer #: {customer.customerNumber}
                </Typography>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Company: {customer.companyName}
                </Typography>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Email: {customer.email}
                </Typography>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Phone: {customer.phone}
                </Typography>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Address: {customer.address}, {customer.postalCode} {customer.city}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Tax #: {customer.taxNumber}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
    </Box>
  );
} 