import React, { useState } from 'react';
import {
  Box,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Typography,
  Alert,
  CircularProgress,
  Divider
} from '@mui/material';
import {
  Payment as PaymentIcon,
  Print as PrintIcon,
  Receipt as ReceiptIcon,
  CheckCircle as CheckIcon
} from '@mui/icons-material';
import { useCart } from '../contexts/CartContext';

const QuickActions: React.FC = () => {
  const { items, total, clearCart } = useCart();
  const [paymentDialog, setPaymentDialog] = useState(false);
  const [paymentMethod, setPaymentMethod] = useState('cash');
  const [receivedAmount, setReceivedAmount] = useState('');
  const [processing, setProcessing] = useState(false);
  const [success, setSuccess] = useState(false);

  const handlePayment = () => {
    setPaymentDialog(true);
  };

  const processPayment = async () => {
    setProcessing(true);
    try {
      // Simüle edilmiş ödeme işlemi
      await new Promise(resolve => setTimeout(resolve, 2000));

      // Başarılı ödeme
      setSuccess(true);
      setTimeout(() => {
        setPaymentDialog(false);
        setSuccess(false);
        setProcessing(false);
        clearCart();
        setReceivedAmount('');
      }, 1500);
    } catch (error) {
      setProcessing(false);
    }
  };

  const handlePrint = () => {
    // Yazdırma işlemi
    const printContent = `
      === KASA FİŞİ ===
      Tarih: ${new Date().toLocaleString()}
      
      ${items.map(item =>
      `${item.product.name} x${item.quantity} = ${(item.product.price * item.quantity).toFixed(2)} €`
    ).join('\n')}
      
      ================
      TOPLAM: ${total.toFixed(2)} €
      ================
    `;

    const printWindow = window.open('', '_blank');
    if (printWindow) {
      printWindow.document.write(`<pre>${printContent}</pre>`);
      printWindow.document.close();
      printWindow.print();
    }
  };

  const calculateChange = () => {
    const received = parseFloat(receivedAmount) || 0;
    return Math.max(0, received - total);
  };

  return (
    <Box sx={{ mt: 2 }}>
      <Button
        variant="contained"
        color="success"
        fullWidth
        size="large"
        disabled={items.length === 0}
        onClick={handlePayment}
        startIcon={<PaymentIcon />}
        sx={{
          mb: 2,
          py: 1.5,
          fontWeight: 700,
          fontSize: '1.1rem'
        }}
      >
        Ödeme Al ({total.toFixed(2)} €)
      </Button>

      <Box sx={{ display: 'flex', gap: 1 }}>
        <Button
          variant="outlined"
          color="primary"
          fullWidth
          disabled={items.length === 0}
          onClick={handlePrint}
          startIcon={<PrintIcon />}
          sx={{ py: 1.2 }}
        >
          Yazdır
        </Button>

        <Button
          variant="outlined"
          color="secondary"
          fullWidth
          disabled={items.length === 0}
          startIcon={<ReceiptIcon />}
          sx={{ py: 1.2 }}
        >
          Fiş
        </Button>
      </Box>

      {/* Ödeme Dialog */}
      <Dialog open={paymentDialog} onClose={() => !processing && setPaymentDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>
          <Box sx={{ display: 'flex', alignItems: 'center' }}>
            <PaymentIcon sx={{ mr: 1 }} />
            Ödeme Al
          </Box>
        </DialogTitle>

        <DialogContent>
          {success ? (
            <Box sx={{ textAlign: 'center', py: 4 }}>
              <CheckIcon sx={{ fontSize: 60, color: 'success.main', mb: 2 }} />
              <Typography variant="h6" color="success.main" gutterBottom>
                Ödeme Başarılı!
              </Typography>
              <Typography variant="body2" color="text.secondary">
                Fiş yazdırılıyor...
              </Typography>
            </Box>
          ) : (
            <Box sx={{ py: 2 }}>
              <Typography variant="h6" gutterBottom>
                Toplam Tutar: {total.toFixed(2)} €
              </Typography>

              <Divider sx={{ my: 2 }} />

              <TextField
                fullWidth
                label="Alınan Tutar"
                type="number"
                value={receivedAmount}
                onChange={(e) => setReceivedAmount(e.target.value)}
                sx={{ mb: 2 }}
                InputProps={{
                  endAdornment: <Typography>€</Typography>
                }}
              />

              {receivedAmount && (
                <Alert severity="info" sx={{ mb: 2 }}>
                  Para Üstü: {calculateChange().toFixed(2)} €
                </Alert>
              )}
            </Box>
          )}
        </DialogContent>

        <DialogActions>
          <Button
            onClick={() => setPaymentDialog(false)}
            disabled={processing}
          >
            İptal
          </Button>
          <Button
            onClick={processPayment}
            variant="contained"
            disabled={processing || !receivedAmount || parseFloat(receivedAmount) < total}
            startIcon={processing ? <CircularProgress size={20} /> : <PaymentIcon />}
          >
            {processing ? 'İşleniyor...' : 'Ödemeyi Tamamla'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default QuickActions; 