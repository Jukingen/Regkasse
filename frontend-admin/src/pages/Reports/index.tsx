import { useEffect, useState } from 'react';
import { Typography, CircularProgress, Alert, Box, Card, CardContent, Grid } from '@mui/material';
import { useTranslation } from 'react-i18next';
import api from '../../services/api';

export default function Reports() {
  const { t } = useTranslation();
  const [reports, setReports] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchReports = async () => {
      try {
        setLoading(true);
        setError(null);
        
        // API çağrısı - dashboard summary'den rapor verisi al
        const response = await api.get('/api/reports/dashboard-summary');
        
        if (response.data) {
          // API'den gelen veriyi rapor formatına çevir
          const dashboardData = response.data;
          setReports([
            {
              id: 'dashboard-summary',
              type: 'Dashboard Summary',
              period: 'Current Period',
              totalSales: dashboardData.todaySales || 0,
              totalInvoices: dashboardData.pendingInvoices || 0,
              averageTicket: dashboardData.todaySales > 0 ? dashboardData.todaySales / (dashboardData.pendingInvoices || 1) : 0,
              generatedAt: new Date().toISOString()
            }
          ]);
        } else {
          setReports([]);
        }
      } catch (err) {
        console.error('Reports API error:', err);
        setReports([]);
        setError('API bağlantı hatası - Veriler yüklenemedi');
      } finally {
        setLoading(false);
      }
    };

    fetchReports();
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
          {t('navigation.reports')}
        </Typography>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      </Box>
    );
  }

  if (reports.length === 0) {
    return (
      <Box>
        <Typography variant="h4" component="h1" gutterBottom>
          {t('navigation.reports')}
        </Typography>
        <Alert severity="info" sx={{ mb: 2 }}>
          Henüz rapor verisi bulunmuyor.
        </Alert>
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        {t('navigation.reports')}
      </Typography>

      <Grid container spacing={3}>
        {reports.map((report) => (
          <Grid item xs={12} md={6} key={report.id}>
            <Card>
              <CardContent>
                <Typography variant="h6" gutterBottom>
                  {report.type}
                </Typography>
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  Period: {report.period}
                </Typography>
                <Typography variant="body1">
                  Total Sales: €{report.totalSales?.toFixed(2) || 'N/A'}
                </Typography>
                <Typography variant="body1">
                  Total Invoices: {report.totalInvoices || 'N/A'}
                </Typography>
                <Typography variant="body1">
                  Average Ticket: €{report.averageTicket?.toFixed(2) || 'N/A'}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Generated: {new Date(report.generatedAt).toLocaleString()}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
    </Box>
  );
} 