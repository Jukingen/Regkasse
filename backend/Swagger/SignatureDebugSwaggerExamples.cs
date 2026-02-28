using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace KasseAPI_Final.Swagger;

/// <summary>
/// RKSV signature-debug ve verify-signature endpoint'leri için Swagger örnekleri.
/// </summary>
public class SignatureDebugSwaggerExamples : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.RelativePath?.Contains("verify-signature") == true
            && context.ApiDescription.HttpMethod == "POST")
        {
            operation.RequestBody ??= new OpenApiRequestBody();
            operation.RequestBody.Content ??= new Dictionary<string, OpenApiMediaType>();
            var key = operation.RequestBody.Content.Keys.FirstOrDefault(k => k.Contains("json", StringComparison.OrdinalIgnoreCase)) ?? "application/json";
            if (!operation.RequestBody.Content.ContainsKey(key))
                operation.RequestBody.Content[key] = new OpenApiMediaType();
            operation.RequestBody.Content[key].Example = new OpenApiObject
            {
                ["compactJws"] = new OpenApiString("eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9.eyJrYXNzZW5JZCI6IktBU1NFLTAwMSIsImJlbGVnTnIiOiJBUy1LQVNTRTAwMS0yMDI1MDIyNS0xMjM0NTY3OCIsImJlbGVnRGF0dW0iOiIyNS4wMi4yMDI1IiwidWhyemVpdCI6IjE0OjMwOjAwIiwiYmV0cmFnIjoiMTIzLjQ1IiwicHJldlNpZ25hdHVyZVZhbHVlIjoiIiwidGF4RGV0YWlscyI6Int9In0.SIGNATURE_B64URL_PART")
            };
        }

        if (context.ApiDescription.RelativePath?.Contains("signature-debug") == true
            && context.ApiDescription.HttpMethod == "GET")
        {
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["200"] ??= new OpenApiResponse { Description = "Diagnostic steps" };
            if (operation.Responses.TryGetValue("200", out var response))
            {
                response.Content ??= new Dictionary<string, OpenApiMediaType>();
                if (response.Content.TryGetValue("application/json", out var mediaType))
                {
                    mediaType.Example = new OpenApiObject
                    {
                        ["success"] = new OpenApiBoolean(true),
                        ["message"] = new OpenApiString("Signature diagnostic completed"),
                        ["data"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["stepId"] = new OpenApiInteger(1),
                                ["name"] = new OpenApiString("CMC match"),
                                ["status"] = new OpenApiString("PASS"),
                                ["evidence"] = new OpenApiString("Software mode: key provider used (no CMC)")
                            },
                            new OpenApiObject
                            {
                                ["stepId"] = new OpenApiInteger(2),
                                ["name"] = new OpenApiString("JWS format"),
                                ["status"] = new OpenApiString("PASS"),
                                ["evidence"] = new OpenApiString("header.payload.signature valid")
                            },
                            new OpenApiObject
                            {
                                ["stepId"] = new OpenApiInteger(3),
                                ["name"] = new OpenApiString("Hash"),
                                ["status"] = new OpenApiString("PASS"),
                                ["evidence"] = new OpenApiString("SHA-256(150 chars)")
                            },
                            new OpenApiObject
                            {
                                ["stepId"] = new OpenApiInteger(4),
                                ["name"] = new OpenApiString("Signature verify"),
                                ["status"] = new OpenApiString("PASS"),
                                ["evidence"] = new OpenApiString("ES256 verification succeeded")
                            },
                            new OpenApiObject
                            {
                                ["stepId"] = new OpenApiInteger(5),
                                ["name"] = new OpenApiString("Base64URL padding"),
                                ["status"] = new OpenApiString("PASS"),
                                ["evidence"] = new OpenApiString("No padding in any part")
                            }
                        }
                    };
                }
            }
        }

        // POST api/Payment: Örnek response (tse, qrPayload dahil)
        if (context.ApiDescription.RelativePath?.Contains("Payment") == true
            && context.ApiDescription.HttpMethod == "POST"
            && !context.ApiDescription.RelativePath.Contains("signature"))
        {
            operation.Responses ??= new OpenApiResponses();
            operation.Responses["201"] ??= new OpenApiResponse { Description = "Payment created" };
            if (operation.Responses.TryGetValue("201", out var resp))
            {
                resp.Content ??= new Dictionary<string, OpenApiMediaType>();
                resp.Content["application/json"] ??= new OpenApiMediaType();
                resp.Content["application/json"].Example = new OpenApiObject
                {
                    ["success"] = new OpenApiBoolean(true),
                    ["paymentId"] = new OpenApiString("c53521eb-0053-435a-b04e-54602578f62a"),
                    ["message"] = new OpenApiString("Payment created successfully"),
                    ["payment"] = new OpenApiObject
                    {
                        ["id"] = new OpenApiString("c53521eb-0053-435a-b04e-54602578f62a"),
                        ["totalAmount"] = new OpenApiString("10.00"),
                        ["receiptNumber"] = new OpenApiString("AT-KASSE-001-20260228-011cfa48"),
                        ["tseSignature"] = new OpenApiString("eyJhbGci...")
                    },
                    ["tse"] = new OpenApiObject
                    {
                        ["tseSignature"] = new OpenApiString("eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9..."),
                        ["qrPayload"] = new OpenApiString("_R1-AT1_KASSE-001_AT-KASSE-001-20260228-011cfa48_2026-02-28T17:52:48_10.00_0.00_SW-TEST-abc12345_eyJhbGci..."),
                        ["isDemoFiscal"] = new OpenApiBoolean(true),
                        ["provider"] = new OpenApiString("Demo")
                    }
                };
            }
        }
    }
}
