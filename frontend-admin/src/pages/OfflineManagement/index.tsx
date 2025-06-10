import React, { useEffect, useState } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  Button,
  Alert,
  CircularProgress,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
  LinearProgress,
} from '@mui/material';
import { useTranslation } from 'react-i18next';
import { offlineService, OfflineInvoice, OfflineDailyReport, OfflineProduct } from '../../services/offlineService';

interface SyncStatus {
  isOnline: boolean;
  unsyncedInvoices: number;
  unsyncedReports: number;
  totalDocuments: number;
}

const OfflineManagement: React.FC = () => {
  const { t } = useTranslation();
  const [syncStatus, setSyncStatus] = useState<SyncStatus | null>(null);
  const [invoices, setInvoices] = useState<OfflineInvoice[]>([]);
  const [dailyReports, setDailyReports] = useState<OfflineDailyReport[]>([]);
  const [products, setProducts] = useState<OfflineProduct[]>([]);
  const [loading, setLoading] = useState(true);
  const [syncing, setSyncing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const fetchData = async () => {
    try {
      setLoading(true);
      setError(null);

      const [status, invoicesData, reportsData, productsData] = await Promise.all([
        offlineService.getSyncStatus(),
        offlineService.getInvoices(),
        offlineService.getDailyReports(),
        offlineService.getProducts(),
      ]);

      setSyncStatus(status);
      setInvoices(invoicesData);
      setDailyReports(reportsData);
      setProducts(productsData);
    } catch (err) {
      setError(t('errors.offline_data_load_failed'));
      console.error('Offline veri yükleme hatası:', err);
    } finally {
      setLoading(false);
    }
  };

  const syncData = async () => {
    try {
      setSyncing(true);
      setError(null);
      setSuccess(null);

      await offlineService.syncData();
      setSuccess(t('offline.sync_success'));
      await fetchData();
    } catch (err) {
      setError(t('errors.sync_failed'));
      console.error('Senkronizasyon hatası:', err);
    } finally {
      setSyncing(false);
    }
  };

  const clearDatabase = async () => {
    try {
      setError(null);
      setSuccess(null);

      await offlineService.clearDatabase();
      setSuccess(t('offline.database_cleared'));
      await fetchData();
    } catch (err) {
      setError(t('errors.clear_database_failed'));
      console.error('Veritabanı temizleme hatası:', err);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="400px">
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box p={3}>
      <Typography variant="h4" gutterBottom>
        {t('offline.management_title')}
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {success && (
        <Alert severity="success" sx={{ mb: 2 }}>
          {success}
        </Alert>
      )}

      {/* Senkronizasyon Durumu */}
      {syncStatus && (
        <Card sx={{ mb: 3 }}>
          <CardContent>
            <Typography variant="h6" gutterBottom>
              {t('offline.sync_status')}
            </Typography>
            
            <Box sx={{ display: 'flex', flexDirection: { xs: 'column', md: 'row' }, gap: 2 }}>
              <Box sx={{ flex: 1 }}>
                <Alert severity={syncStatus.isOnline ? 'success' : 'warning'} sx={{ mb: 2 }}>
                  {syncStatus.isOnline ? t('offline.online') : t('offline.offline')}
                </Alert>
              </Box>

              <Box sx={{ flex: 1 }}>
                <Typography variant="body2" sx={{ mb: 1 }}>
                  <strong>{t('offline.total_documents')}:</strong> {syncStatus.totalDocuments}
                </Typography>
                <Typography variant="body2" sx={{ mb: 1 }}>
                  <strong>{t('offline.unsynced_invoices')}:</strong> {syncStatus.unsyncedInvoices}
                </Typography>
                <Typography variant="body2" sx={{ mb: 1 }}>
                  <strong>{t('offline.unsynced_reports')}:</strong> {syncStatus.unsyncedReports}
                </Typography>
              </Box>
            </Box>

            {syncing && (
              <Box sx={{ mt: 2 }}>
                <LinearProgress />
                <Typography variant="body2" sx={{ mt: 1 }}>
                  {t('offline.syncing')}...
                </Typography>
              </Box>
            )}
          </CardContent>
        </Card>
      )}

      <Box sx={{ display: 'flex', flexDirection: { xs: 'column', md: 'row' }, gap: 3 }}>
        {/* Faturalar */}
        <Box sx={{ flex: 1 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('offline.invoices')} ({invoices.length})
              </Typography>
              
              {invoices.length > 0 ? (
                <TableContainer component={Paper}>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>{t('offline.receipt_number')}</TableCell>
                        <TableCell>{t('offline.synced')}</TableCell>
                        <TableCell>{t('offline.created')}</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {invoices.slice(0, 5).map((invoice) => (
                        <TableRow key={invoice._id}>
                          <TableCell>{invoice.receiptNumber}</TableCell>
                          <TableCell>
                            <Chip 
                              label={invoice.synced ? t('offline.yes') : t('offline.no')}
                              color={invoice.synced ? 'success' : 'warning'}
                              size="small"
                            />
                          </TableCell>
                          <TableCell>{new Date(invoice.createdAt).toLocaleDateString()}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              ) : (
                <Typography variant="body2" color="textSecondary">
                  {t('offline.no_invoices')}
                </Typography>
              )}
            </CardContent>
          </Card>
        </Box>

        {/* Günlük Raporlar */}
        <Box sx={{ flex: 1 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('offline.daily_reports')} ({dailyReports.length})
              </Typography>
              
              {dailyReports.length > 0 ? (
                <TableContainer component={Paper}>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>{t('offline.date')}</TableCell>
                        <TableCell>{t('offline.synced')}</TableCell>
                        <TableCell>{t('offline.receipt_count')}</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {dailyReports.slice(0, 5).map((report) => (
                        <TableRow key={report._id}>
                          <TableCell>{new Date(report.date).toLocaleDateString()}</TableCell>
                          <TableCell>
                            <Chip 
                              label={report.synced ? t('offline.yes') : t('offline.no')}
                              color={report.synced ? 'success' : 'warning'}
                              size="small"
                            />
                          </TableCell>
                          <TableCell>{report.receiptCount}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              ) : (
                <Typography variant="body2" color="textSecondary">
                  {t('offline.no_reports')}
                </Typography>
              )}
            </CardContent>
          </Card>
        </Box>
      </Box>

      {/* Ürünler */}
      <Card sx={{ mt: 3 }}>
        <CardContent>
          <Typography variant="h6" gutterBottom>
            {t('offline.products')} ({products.length})
          </Typography>
          
          {products.length > 0 ? (
            <TableContainer component={Paper}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>{t('offline.product_name')}</TableCell>
                    <TableCell>{t('offline.price')}</TableCell>
                    <TableCell>{t('offline.stock')}</TableCell>
                    <TableCell>{t('offline.synced')}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {products.slice(0, 10).map((product) => (
                    <TableRow key={product._id}>
                      <TableCell>{product.name}</TableCell>
                      <TableCell>{product.price.toFixed(2)} €</TableCell>
                      <TableCell>{product.stock}</TableCell>
                      <TableCell>
                        <Chip 
                          label={product.synced ? t('offline.yes') : t('offline.no')}
                          color={product.synced ? 'success' : 'warning'}
                          size="small"
                        />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          ) : (
            <Typography variant="body2" color="textSecondary">
              {t('offline.no_products')}
            </Typography>
          )}
        </CardContent>
      </Card>

      {/* Aksiyon Butonları */}
      <Box sx={{ mt: 3, display: 'flex', gap: 2, flexWrap: 'wrap' }}>
        <Button
          variant="contained"
          color="primary"
          onClick={syncData}
          disabled={syncing || !syncStatus?.isOnline}
        >
          {syncing ? <CircularProgress size={24} /> : t('offline.sync_now')}
        </Button>

        <Button
          variant="outlined"
          onClick={fetchData}
        >
          {t('offline.refresh')}
        </Button>

        <Button
          variant="outlined"
          color="warning"
          onClick={clearDatabase}
        >
          {t('offline.clear_database')}
        </Button>
      </Box>
    </Box>
  );
};

export default OfflineManagement; 