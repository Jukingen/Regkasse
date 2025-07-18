import { useEffect, useState } from 'react';
import {
  Typography,
  CircularProgress,
  Alert,
  Box,
  Card,
  CardContent,
  Grid,
  Chip,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  IconButton,
  Tooltip,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Fab,
  Snackbar
} from '@mui/material';
import {
  Add as AddIcon,
  Edit as EditIcon,
  Delete as DeleteIcon,
  Download as DownloadIcon,
  Email as EmailIcon,
  Cancel as CancelIcon,
  CloudUpload as CloudUploadIcon,
  Verified as VerifiedIcon,
  Visibility as ViewIcon
} from '@mui/icons-material';
import { useTranslation } from 'react-i18next';
import * as InvoiceService from '../../services/invoiceService';
import { Invoice, InvoiceCreateRequest, InvoiceUpdateRequest } from '../../services/invoiceService';

export default function Invoices() {
  const { t } = useTranslation();
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [statistics, setStatistics] = useState<any>(null);
  const [selectedInvoice, setSelectedInvoice] = useState<Invoice | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogType, setDialogType] = useState<'create' | 'edit' | 'payment' | 'email' | 'view'>('create');
  const [snackbar, setSnackbar] = useState<{ open: boolean; message: string; severity: 'success' | 'error' }>({
    open: false,
    message: '',
    severity: 'success'
  });

  // Form state'leri
  const [formData, setFormData] = useState<Partial<InvoiceCreateRequest>>({
    customerId: '',
    items: [],
    notes: ''
  });
  const [paymentData, setPaymentData] = useState({
    paymentMethod: 'cash' as 'cash' | 'card' | 'voucher',
    amount: 0,
    tseRequired: true
  });
  const [emailData, setEmailData] = useState({
    email: '',
    subject: '',
    message: ''
  });

  useEffect(() => {
    loadInvoices();
    loadStatistics();
  }, []);

  const loadInvoices = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await InvoiceService.getInvoices();
      setInvoices(data);
    } catch (err) {
      console.error('Invoices API error:', err);
      setError('API bağlantı hatası - Veriler yüklenemedi');
    } finally {
      setLoading(false);
    }
  };

  const loadStatistics = async () => {
    try {
      const stats = await InvoiceService.getInvoiceStatistics();
      setStatistics(stats);
    } catch (error) {
      console.error('İstatistikler yüklenirken hata:', error);
    }
  };

  const handleCreateInvoice = async () => {
    try {
      if (!formData.customerId || !formData.items || formData.items.length === 0) {
        showSnackbar('Müşteri ve ürün bilgileri zorunludur', 'error');
        return;
      }

      const invoice = await InvoiceService.createInvoice(formData as InvoiceCreateRequest);
      setInvoices(prev => [invoice, ...prev]);
      setDialogOpen(false);
      resetForm();
      showSnackbar('Fatura başarıyla oluşturuldu', 'success');
    } catch (error) {
      console.error('Fatura oluşturulurken hata:', error);
      showSnackbar('Fatura oluşturulurken bir hata oluştu', 'error');
    }
  };

  const handleUpdateInvoice = async () => {
    if (!selectedInvoice) return;

    try {
      const updatedInvoice = await InvoiceService.updateInvoice(selectedInvoice.id, formData as InvoiceUpdateRequest);
      setInvoices(prev => prev.map(inv => inv.id === selectedInvoice.id ? updatedInvoice : inv));
      setDialogOpen(false);
      resetForm();
      showSnackbar('Fatura başarıyla güncellendi', 'success');
    } catch (error) {
      console.error('Fatura güncellenirken hata:', error);
      showSnackbar('Fatura güncellenirken bir hata oluştu', 'error');
    }
  };

  const handleDeleteInvoice = async (id: string) => {
    if (!window.confirm('Bu faturayı silmek istediğinizden emin misiniz?')) return;

    try {
      await InvoiceService.deleteInvoice(id);
      setInvoices(prev => prev.filter(inv => inv.id !== id));
      showSnackbar('Fatura başarıyla silindi', 'success');
    } catch (error) {
      console.error('Fatura silinirken hata:', error);
      showSnackbar('Fatura silinirken bir hata oluştu', 'error');
    }
  };

  const handleSavePayment = async () => {
    if (!selectedInvoice) return;

    try {
      const updatedInvoice = await InvoiceService.savePayment(selectedInvoice.id, paymentData);
      setInvoices(prev => prev.map(inv => inv.id === selectedInvoice.id ? updatedInvoice : inv));
      setDialogOpen(false);
      setSelectedInvoice(null);
      showSnackbar('Ödeme başarıyla kaydedildi', 'success');
    } catch (error) {
      console.error('Ödeme kaydedilirken hata:', error);
      showSnackbar('Ödeme kaydedilirken bir hata oluştu', 'error');
    }
  };

  const handleDownloadPdf = async (id: string) => {
    try {
      await InvoiceService.downloadAndSavePdf(id);
      showSnackbar('PDF başarıyla indirildi', 'success');
    } catch (error) {
      console.error('PDF indirilirken hata:', error);
      showSnackbar('PDF indirilirken bir hata oluştu', 'error');
    }
  };

  const handleSendEmail = async () => {
    if (!selectedInvoice) return;

    try {
      const result = await InvoiceService.sendInvoiceEmail(selectedInvoice.id, emailData);
      setDialogOpen(false);
      setSelectedInvoice(null);
      showSnackbar(result.message, 'success');
    } catch (error) {
      console.error('Email gönderilirken hata:', error);
      showSnackbar('Email gönderilirken bir hata oluştu', 'error');
    }
  };

  const handleCancelInvoice = async (id: string) => {
    const reason = window.prompt('İptal sebebini girin:');
    if (!reason) return;

    try {
      const updatedInvoice = await InvoiceService.cancelInvoice(id, reason);
      setInvoices(prev => prev.map(inv => inv.id === id ? updatedInvoice : inv));
      showSnackbar('Fatura başarıyla iptal edildi', 'success');
    } catch (error) {
      console.error('Fatura iptal edilirken hata:', error);
      showSnackbar('Fatura iptal edilirken bir hata oluştu', 'error');
    }
  };

  const handleSendToFinanzOnline = async (id: string) => {
    try {
      const result = await InvoiceService.sendToFinanzOnline(id);
      showSnackbar(result.message, 'success');
    } catch (error) {
      console.error('FinanzOnline\'a gönderilirken hata:', error);
      showSnackbar('FinanzOnline\'a gönderilirken bir hata oluştu', 'error');
    }
  };

  const handleVerifyTse = async (id: string) => {
    try {
      const result = await InvoiceService.verifyTseSignature(id);
      showSnackbar(result.message, result.isValid ? 'success' : 'error');
    } catch (error) {
      console.error('TSE doğrulama hatası:', error);
      showSnackbar('TSE doğrulama hatası', 'error');
    }
  };

  const openDialog = (type: 'create' | 'edit' | 'payment' | 'email' | 'view', invoice?: Invoice) => {
    setDialogType(type);
    setSelectedInvoice(invoice || null);
    
    if (type === 'edit' && invoice) {
      setFormData({
        customerId: invoice.customerId,
        items: invoice.items.map(item => ({
          productId: item.productId,
          quantity: item.quantity,
          taxType: item.taxType
        })),
        notes: invoice.notes
      });
    } else if (type === 'payment' && invoice) {
      setPaymentData({
        paymentMethod: invoice.paymentMethod || 'cash',
        amount: invoice.totalAmount,
        tseRequired: true
      });
    } else if (type === 'email' && invoice) {
      setEmailData({
        email: invoice.customer?.email || '',
        subject: `Fatura: ${invoice.receiptNumber}`,
        message: ''
      });
    } else {
      resetForm();
    }
    
    setDialogOpen(true);
  };

  const resetForm = () => {
    setFormData({
      customerId: '',
      items: [],
      notes: ''
    });
    setPaymentData({
      paymentMethod: 'cash',
      amount: 0,
      tseRequired: true
    });
    setEmailData({
      email: '',
      subject: '',
      message: ''
    });
  };

  const showSnackbar = (message: string, severity: 'success' | 'error') => {
    setSnackbar({ open: true, message, severity });
  };

  const getStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'paid': return 'success';
      case 'pending': return 'warning';
      case 'overdue': return 'error';
      case 'cancelled': return 'error';
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

  return (
    <Box>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={3}>
        <Typography variant="h4" component="h1">
          {t('navigation.invoices')}
        </Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={() => openDialog('create')}
        >
          Yeni Fatura
        </Button>
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {/* İstatistikler */}
      {statistics && (
        <Grid container spacing={3} sx={{ mb: 3 }}>
          <Grid item xs={12} sm={6} md={3}>
            <Card>
              <CardContent>
                <Typography color="textSecondary" gutterBottom>
                  Toplam Fatura
                </Typography>
                <Typography variant="h4">
                  {statistics.totalInvoices}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <Card>
              <CardContent>
                <Typography color="textSecondary" gutterBottom>
                  Toplam Gelir
                </Typography>
                <Typography variant="h4">
                  €{statistics.totalRevenue?.toFixed(2)}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <Card>
              <CardContent>
                <Typography color="textSecondary" gutterBottom>
                  Bekleyen
                </Typography>
                <Typography variant="h4">
                  {statistics.pendingInvoices}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <Card>
              <CardContent>
                <Typography color="textSecondary" gutterBottom>
                  Gecikmiş
                </Typography>
                <Typography variant="h4">
                  {statistics.overdueInvoices}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        </Grid>
      )}

      {/* Fatura Tablosu */}
      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Fatura No</TableCell>
              <TableCell>Müşteri</TableCell>
              <TableCell>Tarih</TableCell>
              <TableCell>Toplam</TableCell>
              <TableCell>Durum</TableCell>
              <TableCell>Ödeme</TableCell>
              <TableCell>İşlemler</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {invoices.map((invoice) => (
              <TableRow key={invoice.id}>
                <TableCell>{invoice.receiptNumber}</TableCell>
                <TableCell>
                  {invoice.customer?.firstName} {invoice.customer?.lastName}
                </TableCell>
                <TableCell>
                  {new Date(invoice.invoiceDate).toLocaleDateString('tr-TR')}
                </TableCell>
                <TableCell>€{invoice.totalAmount?.toFixed(2)}</TableCell>
                <TableCell>
                  <Chip 
                    label={invoice.status} 
                    color={getStatusColor(invoice.status) as any}
                    size="small"
                  />
                </TableCell>
                <TableCell>
                  {invoice.paymentMethod && (
                    <Chip 
                      label={invoice.paymentMethod} 
                      color={getPaymentMethodColor(invoice.paymentMethod) as any}
                      size="small"
                    />
                  )}
                </TableCell>
                <TableCell>
                  <Box display="flex" gap={1}>
                    <Tooltip title="Görüntüle">
                      <IconButton
                        size="small"
                        onClick={() => openDialog('view', invoice)}
                      >
                        <ViewIcon />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Düzenle">
                      <IconButton
                        size="small"
                        onClick={() => openDialog('edit', invoice)}
                      >
                        <EditIcon />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="PDF İndir">
                      <IconButton
                        size="small"
                        onClick={() => handleDownloadPdf(invoice.id)}
                      >
                        <DownloadIcon />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Email Gönder">
                      <IconButton
                        size="small"
                        onClick={() => openDialog('email', invoice)}
                      >
                        <EmailIcon />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Ödeme Kaydet">
                      <IconButton
                        size="small"
                        onClick={() => openDialog('payment', invoice)}
                      >
                        <AddIcon />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="FinanzOnline'a Gönder">
                      <IconButton
                        size="small"
                        onClick={() => handleSendToFinanzOnline(invoice.id)}
                      >
                        <CloudUploadIcon />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="TSE Doğrula">
                      <IconButton
                        size="small"
                        onClick={() => handleVerifyTse(invoice.id)}
                      >
                        <VerifiedIcon />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="İptal Et">
                      <IconButton
                        size="small"
                        onClick={() => handleCancelInvoice(invoice.id)}
                      >
                        <CancelIcon />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Sil">
                      <IconButton
                        size="small"
                        color="error"
                        onClick={() => handleDeleteInvoice(invoice.id)}
                      >
                        <DeleteIcon />
                      </IconButton>
                    </Tooltip>
                  </Box>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Dialog'lar */}
      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>
          {dialogType === 'create' && 'Yeni Fatura Oluştur'}
          {dialogType === 'edit' && 'Fatura Düzenle'}
          {dialogType === 'payment' && 'Ödeme Kaydet'}
          {dialogType === 'email' && 'Email Gönder'}
          {dialogType === 'view' && 'Fatura Detayı'}
        </DialogTitle>
        <DialogContent>
          {dialogType === 'create' || dialogType === 'edit' ? (
            <Box sx={{ pt: 2 }}>
              <TextField
                fullWidth
                label="Müşteri ID"
                value={formData.customerId}
                onChange={(e) => setFormData(prev => ({ ...prev, customerId: e.target.value }))}
                sx={{ mb: 2 }}
              />
              <TextField
                fullWidth
                label="Notlar"
                value={formData.notes}
                onChange={(e) => setFormData(prev => ({ ...prev, notes: e.target.value }))}
                multiline
                rows={3}
                sx={{ mb: 2 }}
              />
              {/* Ürün listesi burada eklenebilir */}
            </Box>
          ) : dialogType === 'payment' ? (
            <Box sx={{ pt: 2 }}>
              <FormControl fullWidth sx={{ mb: 2 }}>
                <InputLabel>Ödeme Yöntemi</InputLabel>
                <Select
                  value={paymentData.paymentMethod}
                  onChange={(e) => setPaymentData(prev => ({ ...prev, paymentMethod: e.target.value as any }))}
                >
                  <MenuItem value="cash">Nakit</MenuItem>
                  <MenuItem value="card">Kart</MenuItem>
                  <MenuItem value="voucher">Kupon</MenuItem>
                </Select>
              </FormControl>
              <TextField
                fullWidth
                label="Tutar"
                type="number"
                value={paymentData.amount}
                onChange={(e) => setPaymentData(prev => ({ ...prev, amount: parseFloat(e.target.value) || 0 }))}
                sx={{ mb: 2 }}
              />
            </Box>
          ) : dialogType === 'email' ? (
            <Box sx={{ pt: 2 }}>
              <TextField
                fullWidth
                label="Email Adresi"
                value={emailData.email}
                onChange={(e) => setEmailData(prev => ({ ...prev, email: e.target.value }))}
                sx={{ mb: 2 }}
              />
              <TextField
                fullWidth
                label="Konu"
                value={emailData.subject}
                onChange={(e) => setEmailData(prev => ({ ...prev, subject: e.target.value }))}
                sx={{ mb: 2 }}
              />
              <TextField
                fullWidth
                label="Mesaj"
                value={emailData.message}
                onChange={(e) => setEmailData(prev => ({ ...prev, message: e.target.value }))}
                multiline
                rows={4}
                sx={{ mb: 2 }}
              />
            </Box>
          ) : dialogType === 'view' && selectedInvoice ? (
            <Box sx={{ pt: 2 }}>
              <Typography variant="h6" gutterBottom>
                Fatura No: {selectedInvoice.receiptNumber}
              </Typography>
              <Typography>
                Müşteri: {selectedInvoice.customer?.firstName} {selectedInvoice.customer?.lastName}
              </Typography>
              <Typography>
                Tarih: {new Date(selectedInvoice.invoiceDate).toLocaleDateString('tr-TR')}
              </Typography>
              <Typography>
                Toplam: €{selectedInvoice.totalAmount?.toFixed(2)}
              </Typography>
              <Typography>
                Durum: {selectedInvoice.status}
              </Typography>
              <Typography>
                Ödeme Yöntemi: {selectedInvoice.paymentMethod}
              </Typography>
              {selectedInvoice.notes && (
                <Typography>
                  Notlar: {selectedInvoice.notes}
                </Typography>
              )}
            </Box>
          ) : null}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>İptal</Button>
          {dialogType === 'create' && (
            <Button onClick={handleCreateInvoice} variant="contained">
              Oluştur
            </Button>
          )}
          {dialogType === 'edit' && (
            <Button onClick={handleUpdateInvoice} variant="contained">
              Güncelle
            </Button>
          )}
          {dialogType === 'payment' && (
            <Button onClick={handleSavePayment} variant="contained">
              Kaydet
            </Button>
          )}
          {dialogType === 'email' && (
            <Button onClick={handleSendEmail} variant="contained">
              Gönder
            </Button>
          )}
        </DialogActions>
      </Dialog>

      {/* Snackbar */}
      <Snackbar
        open={snackbar.open}
        autoHideDuration={6000}
        onClose={() => setSnackbar(prev => ({ ...prev, open: false }))}
      >
        <Alert
          onClose={() => setSnackbar(prev => ({ ...prev, open: false }))}
          severity={snackbar.severity}
        >
          {snackbar.message}
        </Alert>
      </Snackbar>
    </Box>
  );
} 