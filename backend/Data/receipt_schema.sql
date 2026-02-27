-- Recommended Schema for RKSV Receipts
-- This schema normalizes the JSON data currently stored in PaymentDetails

CREATE TABLE receipts (
    receipt_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id UUID NOT NULL REFERENCES "PaymentDetails"(Id),
    receipt_number VARCHAR(50) NOT NULL UNIQUE,
    issued_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    cashier_id VARCHAR(50),
    cash_register_id VARCHAR(50) NOT NULL,
    sub_total DECIMAL(10, 2) NOT NULL,
    tax_total DECIMAL(10, 2) NOT NULL,
    grand_total DECIMAL(10, 2) NOT NULL,
    qr_code_payload TEXT,
    signature_value TEXT,
    prev_signature_value TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE receipt_items (
    item_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    receipt_id UUID NOT NULL REFERENCES receipts(receipt_id),
    product_name VARCHAR(255) NOT NULL,
    quantity INT NOT NULL,
    unit_price DECIMAL(10, 2) NOT NULL,
    total_price DECIMAL(10, 2) NOT NULL,
    tax_rate DECIMAL(5, 2) NOT NULL
);

CREATE TABLE receipt_tax_lines (
    line_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    receipt_id UUID NOT NULL REFERENCES receipts(receipt_id),
    tax_rate DECIMAL(5, 2) NOT NULL,
    net_amount DECIMAL(10, 2) NOT NULL,
    tax_amount DECIMAL(10, 2) NOT NULL,
    gross_amount DECIMAL(10, 2) NOT NULL
);

CREATE INDEX idx_receipts_payment_id ON receipts(payment_id);
CREATE INDEX idx_receipts_receipt_number ON receipts(receipt_number);
