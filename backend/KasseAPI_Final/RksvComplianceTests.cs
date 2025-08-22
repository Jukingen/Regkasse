using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;

namespace KasseAPI_Final.Tests
{
    /// <summary>
    /// RKSV Compliance Endpoint Tests
    /// Tests RKSV compliance validation for products
    /// </summary>
    public class RksvComplianceTests
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "http://localhost:5183";
        private readonly string _authToken = "YOUR_AUTH_TOKEN_HERE";

        public RksvComplianceTests()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_authToken}");
        }

        /// <summary>
        /// Test valid RKSV product creation
        /// </summary>
        public async Task TestValidRksvProductCreation()
        {
            Console.WriteLine("Testing Valid RKSV Product Creation...");
            
            var product = new
            {
                name = "RKSV Test Product",
                price = 25.99m,
                taxType = "Standard",
                description = "Test product for RKSV compliance",
                category = "Test Category",
                stockQuantity = 100,
                minStockLevel = 10,
                unit = "piece",
                cost = 15.00m,
                taxRate = 20.0m,
                isFiscalCompliant = true,
                fiscalCategoryCode = "AT001",
                isTaxable = true,
                rksvProductType = "Standard"
            };

            var json = JsonSerializer.Serialize(product);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/products/create", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Valid RKSV Product Creation: SUCCESS");
                }
                else
                {
                    Console.WriteLine("❌ Valid RKSV Product Creation: FAILED");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Test RKSV product with tax exemption
        /// </summary>
        public async Task TestRksvProductWithTaxExemption()
        {
            Console.WriteLine("\nTesting RKSV Product with Tax Exemption...");
            
            var product = new
            {
                name = "RKSV Exempt Product",
                price = 15.50m,
                taxType = "Exempt",
                description = "Tax exempt product for RKSV compliance",
                category = "Exempt Category",
                stockQuantity = 50,
                minStockLevel = 5,
                unit = "piece",
                cost = 10.00m,
                taxRate = 0.0m,
                isFiscalCompliant = true,
                fiscalCategoryCode = "AT002",
                isTaxable = false,
                taxExemptionReason = "Medical supplies - tax exempt",
                rksvProductType = "Exempt"
            };

            var json = JsonSerializer.Serialize(product);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/products/create", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ RKSV Product with Tax Exemption: SUCCESS");
                }
                else
                {
                    Console.WriteLine("❌ RKSV Product with Tax Exemption: FAILED");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Test invalid RKSV product (missing tax exemption reason)
        /// </summary>
        public async Task TestInvalidRksvProductMissingExemptionReason()
        {
            Console.WriteLine("\nTesting Invalid RKSV Product (Missing Tax Exemption Reason)...");
            
            var product = new
            {
                name = "Invalid RKSV Product",
                price = 30.00m,
                taxType = "Exempt",
                description = "Invalid product missing tax exemption reason",
                category = "Invalid Category",
                stockQuantity = 75,
                minStockLevel = 8,
                unit = "piece",
                cost = 20.00m,
                taxRate = 0.0m,
                isFiscalCompliant = true,
                fiscalCategoryCode = "AT003",
                isTaxable = false,
                rksvProductType = "Exempt"
            };

            var json = JsonSerializer.Serialize(product);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/products/create", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Invalid RKSV Product (Missing Exemption Reason): CORRECTLY REJECTED");
                }
                else
                {
                    Console.WriteLine("❌ Invalid RKSV Product (Missing Exemption Reason): INCORRECTLY ACCEPTED");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Test invalid RKSV product (non-compliant)
        /// </summary>
        public async Task TestInvalidRksvProductNonCompliant()
        {
            Console.WriteLine("\nTesting Invalid RKSV Product (Non-Compliant)...");
            
            var product = new
            {
                name = "Non-Compliant Product",
                price = 45.00m,
                taxType = "Standard",
                description = "Non-compliant product for RKSV validation",
                category = "Non-Compliant Category",
                stockQuantity = 25,
                minStockLevel = 3,
                unit = "piece",
                cost = 30.00m,
                taxRate = 20.0m,
                isFiscalCompliant = false,
                fiscalCategoryCode = "AT004",
                isTaxable = true,
                rksvProductType = "Standard"
            };

            var json = JsonSerializer.Serialize(product);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/products/create", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Invalid RKSV Product (Non-Compliant): CORRECTLY REJECTED");
                }
                else
                {
                    Console.WriteLine("❌ Invalid RKSV Product (Non-Compliant): INCORRECTLY ACCEPTED");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Test invalid RKSV product (invalid tax type)
        /// </summary>
        public async Task TestInvalidRksvProductInvalidTaxType()
        {
            Console.WriteLine("\nTesting Invalid RKSV Product (Invalid Tax Type)...");
            
            var product = new
            {
                name = "Invalid Tax Type Product",
                price = 35.00m,
                taxType = "InvalidTaxType",
                description = "Product with invalid tax type",
                category = "Invalid Tax Category",
                stockQuantity = 60,
                minStockLevel = 6,
                unit = "piece",
                cost = 25.00m,
                taxRate = 25.0m,
                isFiscalCompliant = true,
                fiscalCategoryCode = "AT005",
                isTaxable = true,
                rksvProductType = "Standard"
            };

            var json = JsonSerializer.Serialize(product);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/products/create", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Invalid RKSV Product (Invalid Tax Type): CORRECTLY REJECTED");
                }
                else
                {
                    Console.WriteLine("❌ Invalid RKSV Product (Invalid Tax Type): INCORRECTLY ACCEPTED");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Test invalid RKSV product (invalid RKSV type)
        /// </summary>
        public async Task TestInvalidRksvProductInvalidRksvType()
        {
            Console.WriteLine("\nTesting Invalid RKSV Product (Invalid RKSV Type)...");
            
            var product = new
            {
                name = "Invalid RKSV Type Product",
                price = 40.00m,
                taxType = "Standard",
                description = "Product with invalid RKSV type",
                category = "Invalid RKSV Category",
                stockQuantity = 40,
                minStockLevel = 4,
                unit = "piece",
                cost = 28.00m,
                taxRate = 20.0m,
                isFiscalCompliant = true,
                fiscalCategoryCode = "AT006",
                isTaxable = true,
                rksvProductType = "InvalidRKSVType"
            };

            var json = JsonSerializer.Serialize(product);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/products/create", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Invalid RKSV Product (Invalid RKSV Type): CORRECTLY REJECTED");
                }
                else
                {
                    Console.WriteLine("❌ Invalid RKSV Product (Invalid RKSV Type): INCORRECTLY ACCEPTED");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Test invalid RKSV product (tax rate mismatch)
        /// </summary>
        public async Task TestInvalidRksvProductTaxRateMismatch()
        {
            Console.WriteLine("\nTesting Invalid RKSV Product (Tax Rate Mismatch)...");
            
            var product = new
            {
                name = "Tax Rate Mismatch Product",
                price = 50.00m,
                taxType = "Reduced",
                description = "Product with tax rate mismatch",
                category = "Tax Rate Category",
                stockQuantity = 30,
                minStockLevel = 3,
                unit = "piece",
                cost = 35.00m,
                taxRate = 20.0m,
                isFiscalCompliant = true,
                fiscalCategoryCode = "AT007",
                isTaxable = true,
                rksvProductType = "Reduced"
            };

            var json = JsonSerializer.Serialize(product);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/products/create", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {responseContent}");
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Invalid RKSV Product (Tax Rate Mismatch): CORRECTLY REJECTED");
                }
                else
                {
                    Console.WriteLine("❌ Invalid RKSV Product (Tax Rate Mismatch): INCORRECTLY ACCEPTED");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Test get all products to verify RKSV fields
        /// </summary>
        public async Task TestGetAllProductsWithRksvFields()
        {
            Console.WriteLine("\nTesting Get All Products (Verify RKSV Fields)...");
            
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/products/all");
                var responseContent = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"Status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Get All Products: SUCCESS");
                    Console.WriteLine($"Response length: {responseContent.Length} characters");
                    
                    // Check if response contains RKSV fields
                    if (responseContent.Contains("isFiscalCompliant") && 
                        responseContent.Contains("rksvProductType") &&
                        responseContent.Contains("fiscalCategoryCode"))
                    {
                        Console.WriteLine("✅ RKSV Fields Present in Response");
                    }
                    else
                    {
                        Console.WriteLine("❌ RKSV Fields Missing in Response");
                    }
                }
                else
                {
                    Console.WriteLine("❌ Get All Products: FAILED");
                    Console.WriteLine($"Response: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Test search products with RKSV compliance
        /// </summary>
        public async Task TestSearchProductsWithRksvCompliance()
        {
            Console.WriteLine("\nTesting Search Products with RKSV Compliance...");
            
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/products/search?name=RKSV&category=Test");
                var responseContent = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"Status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Search Products with RKSV: SUCCESS");
                    Console.WriteLine($"Response length: {responseContent.Length} characters");
                }
                else
                {
                    Console.WriteLine("❌ Search Products with RKSV: FAILED");
                    Console.WriteLine($"Response: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Run all RKSV compliance tests
        /// </summary>
        public async Task RunAllTests()
        {
            Console.WriteLine("🚀 Starting RKSV Compliance Endpoint Tests...\n");
            
            await TestValidRksvProductCreation();
            await TestRksvProductWithTaxExemption();
            await TestInvalidRksvProductMissingExemptionReason();
            await TestInvalidRksvProductNonCompliant();
            await TestInvalidRksvProductInvalidTaxType();
            await TestInvalidRksvProductInvalidRksvType();
            await TestInvalidRksvProductTaxRateMismatch();
            await TestGetAllProductsWithRksvFields();
            await TestSearchProductsWithRksvCompliance();
            
            Console.WriteLine("\n🎯 RKSV Compliance Endpoint Tests Completed!");
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Main program to run RKSV compliance tests
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("🔍 RKSV Compliance Testing Tool");
            Console.WriteLine("================================\n");
            
            using var tester = new RksvComplianceTests();
            await tester.RunAllTests();
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
