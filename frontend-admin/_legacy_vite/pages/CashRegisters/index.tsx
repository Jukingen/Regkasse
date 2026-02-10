import { useEffect, useState } from 'react';
import { Typography, CircularProgress, Alert, Box, Card, CardContent, Grid, Chip } from '@mui/material';
import { useTranslation } from 'react-i18next';
import api from '../../services/api';

export default function CashRegisters() {
  const { t } = useTranslation();
  const [cashRegisters, setCashRegisters] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchCashRegisters = async () => {
      try {
        setLoading(true);
        setError(null);
        
        // API çağrısı - cash registers endpoint'inden veri al
        const response = await api.get('/api/cash-registers');
        
        if (response.data && response.data.length > 0) {
          setCashRegisters(response.data);
        } else {
          setCashRegisters([]);
        }
      } catch (err) {
        console.error('Cash Registers API error:', err);
        setCashRegisters([]);
        setError('API bağlantı hatası - Veriler yüklenemedi');
      } finally {
        setLoading(false);
      }
    };

    fetchCashRegisters();
  }, []);

  const getStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'open': return 'success';
      case 'closed': return 'error';
      case 'maintenance': return 'warning';
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
          {t('navigation.cashRegisters')}
        </Typography>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      </Box>
    );
  }

  if (cashRegisters.length === 0) {
    return (
      <Box>
        <Typography variant="h4" component="h1" gutterBottom>
          {t('navigation.cashRegisters')}
        </Typography>
        <Alert severity="info" sx={{ mb: 2 }}>
          Henüz kasa verisi bulunmuyor.
        </Alert>
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        {t('navigation.cashRegisters')}
      </Typography>

      <Grid container spacing={3}>
        {cashRegisters.map((register) => (
          <Grid item xs={12} md={6} lg={4} key={register.id}>
            <Card>
              <CardContent>
                <Box display="flex" justifyContent="space-between" alignItems="flex-start" mb={2}>
                  <Typography variant="h6" gutterBottom>
                    {register.registerNumber}
                  </Typography>
                  <Chip 
                    label={register.status} 
                    color={getStatusColor(register.status) as any}
                    size="small"
                  />
                </Box>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Location: {register.location}
                </Typography>
                <Typography variant="body1" fontWeight="bold" color="primary">
                  Current Balance: €{register.currentBalance?.toFixed(2)}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Starting Balance: €{register.startingBalance?.toFixed(2)}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  TSE ID: {register.tseId}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Kassen ID: {register.kassenId}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Last Update: {new Date(register.lastBalanceUpdate).toLocaleString()}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
    </Box>
  );
} 