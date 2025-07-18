using System;
using System.Collections.Generic;

namespace Registrierkasse_API.DTOs
{
    // Türkçe Açıklama: Döngüsel referansları önlemek ve sadece gerekli alanları dönmek için kullanılır.
    public class CartDto
    {
        public string CartId { get; set; }
        public List<CartItemDto> Items { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string Unit { get; set; }
        public string Category { get; set; }
        public string TaxType { get; set; }
        public bool IsActive { get; set; }
    }

    public class CartItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public ProductDto Product { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? Notes { get; set; }
    }
} 