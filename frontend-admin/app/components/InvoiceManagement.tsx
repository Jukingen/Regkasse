import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Box,
  Button,
  Card,
  CardContent,
  Grid,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
  IconButton,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Alert,
} from '@mui/material';
import {
  Print as PrintIcon,
  Receipt as ReceiptIcon,
  Cancel as CancelIcon,
  Refresh as RefreshIcon,
  Search as SearchIcon,
  FilterList as FilterListIcon,
} from '@mui/icons-material';
import { Invoice, InvoiceType, InvoiceStatus, TaxType } from '../../types/invoice';

interface InvoiceManagementProps {
  onPrint: (invoice: Invoice) => void;
  onVoid: (invoice: Invoice, reason: string) => void;
  onRefresh: () => void;
}

export const InvoiceManagement: React.FC<InvoiceManagementProps> = ({
  onPrint,
  onVoid,
  onRefresh,
}) => {
  const { t } = useTranslation();
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [selectedInvoice, setSelectedInvoice] = useState<Invoice | null>(null);
  const [voidDialogOpen, setVoidDialogOpen] = useState(false);
  const [voidReason, setVoidReason] = useState('');
  const [filterType, setFilterType] = useState<InvoiceType | 'ALL'>('ALL');
  const [filterStatus, setFilterStatus] = useState<InvoiceStatus | 'ALL'>('ALL');
  const [searchQuery, setSearchQuery] = useState('');

  useEffect(() => {
    // Faturaları yükle
    fetchInvoices();
  }, []);

  const fetchInvoices = async () => {
    try {
      const response = await fetch('/api/invoices');
      const data = await response.json();
      setInvoices(data);
    } catch (error) {
      console.error('Faturalar yüklenirken hata:', error);
    }
  };

  const handleVoidClick = (invoice: Invoice) => {
    setSelectedInvoice(invoice);
    setVoidDialogOpen(true);
  };

  const handleVoidConfirm = async () => {
    if (selectedInvoice && voidReason) {
      await onVoid(selectedInvoice, voidReason);
      setVoidDialogOpen(false);
      setVoidReason('');
      setSelectedInvoice(null);
      fetchInvoices();
    }
  };

  const getStatusColor = (status: InvoiceStatus) => {
    switch (status) {
      case InvoiceStatus.Completed:
        return 'success';
      case InvoiceStatus.Pending:
        return 'warning';
      case InvoiceStatus.Cancelled:
        return 'error';
      case InvoiceStatus.Refunded:
        return 'info';
      default:
        return 'default';
    }
  };

  const getTypeLabel = (type: InvoiceType) => {
    return t(`invoice.types.${type.toLowerCase()}`);
  };

  const filteredInvoices = invoices.filter((invoice) => {
    const matchesType = filterType === 'ALL' || invoice.invoiceType === filterType;
    const matchesStatus = filterStatus === 'ALL' || invoice.status === filterStatus;
    const matchesSearch = searchQuery === '' || 
      invoice.receiptNumber.toLowerCase().includes(searchQuery.toLowerCase()) ||
      invoice.customerDetails?.companyName?.toLowerCase().includes(searchQuery.toLowerCase()) ||
      invoice.customerDetails?.taxNumber?.includes(searchQuery);

    return matchesType && matchesStatus && matchesSearch;
  });

  return (
    <Box sx={{ p: 3 }}>
      <Card>
        <CardContent>
          <Grid container spacing={2} alignItems="center" sx={{ mb: 3 }}>
            <Grid item xs={12} md={4}>
              <TextField
                fullWidth
                label={t('invoice.search')}
                variant="outlined"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                InputProps={{
                  startAdornment: <SearchIcon sx={{ mr: 1, color: 'text.secondary' }} />,
                }}
              />
            </Grid>
            <Grid item xs={12} md={3}>
              <FormControl fullWidth>
                <InputLabel>{t('invoice.type')}</InputLabel>
                <Select
                  value={filterType}
                  label={t('invoice.type')}
                  onChange={(e) => setFilterType(e.target.value as InvoiceType | 'ALL')}
                >
                  <MenuItem value="ALL">{t('common.all')}</MenuItem>
                  {Object.values(InvoiceType).map((type) => (
                    <MenuItem key={type} value={type}>
                      {getTypeLabel(type)}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Grid>
            <Grid item xs={12} md={3}>
              <FormControl fullWidth>
                <InputLabel>{t('invoice.status')}</InputLabel>
                <Select
                  value={filterStatus}
                  label={t('invoice.status')}
                  onChange={(e) => setFilterStatus(e.target.value as InvoiceStatus | 'ALL')}
                >
                  <MenuItem value="ALL">{t('common.all')}</MenuItem>
                  {Object.values(InvoiceStatus).map((status) => (
                    <MenuItem key={status} value={status}>
                      {t(`invoice.status.${status.toLowerCase()}`)}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Grid>
            <Grid item xs={12} md={2}>
              <Button
                fullWidth
                variant="outlined"
                startIcon={<RefreshIcon />}
                onClick={onRefresh}
              >
                {t('common.refresh')}
              </Button>
            </Grid>
          </Grid>

          <TableContainer component={Paper}>
            <Table>
              <TableHead>
                <TableRow>
                  <TableCell>{t('invoice.number')}</TableCell>
                  <TableCell>{t('invoice.date')}</TableCell>
                  <TableCell>{t('invoice.type')}</TableCell>
                  <TableCell>{t('invoice.customer')}</TableCell>
                  <TableCell>{t('invoice.taxNumber')}</TableCell>
                  <TableCell>{t('invoice.amount')}</TableCell>
                  <TableCell>{t('invoice.status')}</TableCell>
                  <TableCell>{t('invoice.actions')}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {filteredInvoices.map((invoice) => (
                  <TableRow key={invoice.id}>
                    <TableCell>{invoice.receiptNumber}</TableCell>
                    <TableCell>
                      {new Date(invoice.createdAt).toLocaleString('de-DE')}
                    </TableCell>
                    <TableCell>{getTypeLabel(invoice.invoiceType)}</TableCell>
                    <TableCell>
                      {invoice.customerDetails?.companyName || 
                       `${invoice.customerDetails?.firstName} ${invoice.customerDetails?.lastName}`}
                    </TableCell>
                    <TableCell>{invoice.customerDetails?.taxNumber}</TableCell>
                    <TableCell>
                      {invoice.taxSummary.totalAmount.toLocaleString('de-DE', {
                        style: 'currency',
                        currency: 'EUR'
                      })}
                    </TableCell>
                    <TableCell>
                      <Chip
                        label={t(`invoice.status.${invoice.status.toLowerCase()}`)}
                        color={getStatusColor(invoice.status) as any}
                        size="small"
                      />
                    </TableCell>
                    <TableCell>
                      <IconButton
                        onClick={() => onPrint(invoice)}
                        disabled={!invoice.isPrinted}
                        title={t('buttons.printInvoice')}
                      >
                        <PrintIcon />
                      </IconButton>
                      <IconButton
                        onClick={() => handleVoidClick(invoice)}
                        disabled={invoice.isVoid || invoice.status === InvoiceStatus.Cancelled}
                        title={t('buttons.voidInvoice')}
                      >
                        <CancelIcon />
                      </IconButton>
                      <IconButton
                        onClick={() => window.open(`/api/invoices/${invoice.id}/pdf`, '_blank')}
                        title={t('buttons.downloadPDF')}
                      >
                        <ReceiptIcon />
                      </IconButton>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </CardContent>
      </Card>

      <Dialog open={voidDialogOpen} onClose={() => setVoidDialogOpen(false)}>
        <DialogTitle>{t('invoice.void.title')}</DialogTitle>
        <DialogContent>
          <Alert severity="warning" sx={{ mb: 2 }}>
            {t('invoice.void.warning')}
          </Alert>
          <TextField
            autoFocus
            margin="dense"
            label={t('invoice.void.reason')}
            fullWidth
            multiline
            rows={4}
            value={voidReason}
            onChange={(e) => setVoidReason(e.target.value)}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setVoidDialogOpen(false)}>
            {t('common.cancel')}
          </Button>
          <Button
            onClick={handleVoidConfirm}
            color="error"
            disabled={!voidReason.trim()}
          >
            {t('buttons.voidInvoice')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default InvoiceManagement; 