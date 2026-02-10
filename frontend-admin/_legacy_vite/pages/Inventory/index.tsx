import { useEffect, useState } from 'react';
import { Typography, CircularProgress, Alert, Box, Card, CardContent, Grid, Chip } from '@mui/material';
import { useTranslation } from 'react-i18next';
import api from '../../services/api';

export default function Inventory() {
  const { t } = useTranslation();
  const [inventory, setInventory] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchInventory = async () => {
      try {
        setLoading(true);
        setError(null);
        
        // API çağrısı - inventory analytics'den veri al
        const response = await api.get('/api/reports/inventory-analytics');
        
        if (response.data && response.data.lowStockProducts && response.data.lowStockProducts.length > 0) {
          const apiInventory = response.data.lowStockProducts.map((item: any, index: number) => ({
            id: `api-${index}`,
            productName: item.productName,
            currentStock: item.currentStock,
            minimumStock: item.minQuantity,
            maximumStock: item.reorderLevel,
            location: 'Hauptlager',
            lastUpdate: new Date().toISOString(),
            status: item.currentStock <= item.minQuantity ? 'Low Stock' : 'Normal'
          }));
          setInventory(apiInventory);
        } else {
          setInventory([]);
        }
      } catch (err) {
        console.error('Inventory API error:', err);
        setInventory([]);
        setError('API bağlantı hatası - Veriler yüklenemedi');
      } finally {
        setLoading(false);
      }
    };

    fetchInventory();
  }, []);

  const getStatusColor = (status: string) => {
    return status === 'Low Stock' ? 'error' : 'success';
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
          {t('navigation.inventory')}
        </Typography>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      </Box>
    );
  }

  if (inventory.length === 0) {
    return (
      <Box>
        <Typography variant="h4" component="h1" gutterBottom>
          {t('navigation.inventory')}
        </Typography>
        <Alert severity="info" sx={{ mb: 2 }}>
          Henüz stok verisi bulunmuyor.
        </Alert>
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        {t('navigation.inventory')}
      </Typography>

      <Grid container spacing={3}>
        {inventory.map((item) => (
          <Grid item xs={12} md={6} lg={4} key={item.id}>
            <Card>
              <CardContent>
                <Box display="flex" justifyContent="space-between" alignItems="flex-start" mb={2}>
                  <Typography variant="h6" gutterBottom>
                    {item.productName}
                  </Typography>
                  <Chip 
                    label={item.status} 
                    color={getStatusColor(item.status) as any}
                    size="small"
                  />
                </Box>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Location: {item.location}
                </Typography>
                <Typography variant="body1">
                  Current Stock: {item.currentStock}
                </Typography>
                <Typography variant="body1">
                  Min Stock: {item.minimumStock}
                </Typography>
                <Typography variant="body1">
                  Max Stock: {item.maximumStock}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Last Update: {new Date(item.lastUpdate).toLocaleString()}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
    </Box>
  );
} 