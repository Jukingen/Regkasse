using System.Text.Json.Nodes;
using Microsoft.OpenApi;
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
            var requestBody = GetOrCreateRequestBody(operation);
            var key = requestBody.Content.Keys.FirstOrDefault(k => k.Contains("json", StringComparison.OrdinalIgnoreCase)) ?? "application/json";
            if (!requestBody.Content.ContainsKey(key))
                requestBody.Content[key] = new OpenApiMediaType();
            requestBody.Content[key].Example = new JsonObject
            {
                ["compactJws"] = "eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9.eyJrYXNzZW5JZCI6IktBU1NFLTAwMSIsImJlbGVnTnIiOiJBUy1LQVNTRTAwMS0yMDI1MDIyNS0xMjM0NTY3OCIsImJlbGVnRGF0dW0iOiIyNS4wMi4yMDI1IiwidWhyemVpdCI6IjE0OjMwOjAwIiwiYmV0cmFnIjoiMTIzLjQ1IiwicHJldlNpZ25hdHVyZVZhbHVlIjoiIiwidGF4RGV0YWlscyI6Int9In0.SIGNATURE_B64URL_PART"
            };
        }

        if (context.ApiDescription.RelativePath?.Contains("signature-debug") == true
            && context.ApiDescription.HttpMethod == "GET")
        {
            operation.Responses ??= new OpenApiResponses();
            var response = GetOrCreateResponse(operation.Responses, "200", "Diagnostic steps");
            var mediaType = GetOrCreateMediaType(response, "application/json");
            mediaType.Example = new JsonObject
            {
                ["success"] = true,
                ["message"] = "Signature diagnostic completed",
                ["data"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["stepId"] = 1,
                        ["name"] = "CMC match",
                        ["status"] = "PASS",
                        ["evidence"] = "Software mode: key provider used (no CMC)"
                    },
                    new JsonObject
                    {
                        ["stepId"] = 2,
                        ["name"] = "JWS format",
                        ["status"] = "PASS",
                        ["evidence"] = "header.payload.signature valid"
                    },
                    new JsonObject
                    {
                        ["stepId"] = 3,
                        ["name"] = "Hash",
                        ["status"] = "PASS",
                        ["evidence"] = "SHA-256(150 chars)"
                    },
                    new JsonObject
                    {
                        ["stepId"] = 4,
                        ["name"] = "Signature verify",
                        ["status"] = "PASS",
                        ["evidence"] = "ES256 verification succeeded"
                    },
                    new JsonObject
                    {
                        ["stepId"] = 5,
                        ["name"] = "Base64URL padding",
                        ["status"] = "PASS",
                        ["evidence"] = "No padding in any part"
                    }
                }
            };
        }

        if (context.ApiDescription.RelativePath?.Contains("Payment") == true
            && context.ApiDescription.HttpMethod == "POST"
            && !context.ApiDescription.RelativePath.Contains("signature"))
        {
            operation.Responses ??= new OpenApiResponses();
            OpenApiResponse targetResp;
            if (operation.Responses.TryGetValue("201", out var existing201) && existing201 is OpenApiResponse r201)
                targetResp = r201;
            else if (operation.Responses.TryGetValue("200", out var existing200) && existing200 is OpenApiResponse r200)
                targetResp = r200;
            else
            {
                targetResp = new OpenApiResponse { Description = "Payment created" };
                operation.Responses.Add("200", targetResp);
            }

            var mediaType = GetOrCreateMediaType(targetResp, "application/json");
            mediaType.Example = new JsonObject
            {
                ["success"] = true,
                ["paymentId"] = "c53521eb-0053-435a-b04e-54602578f62a",
                ["message"] = "Payment created successfully",
                ["payment"] = new JsonObject
                {
                    ["id"] = "c53521eb-0053-435a-b04e-54602578f62a",
                    ["totalAmount"] = "10.00",
                    ["receiptNumber"] = "AT-KASSE-001-20260228-011cfa48",
                    ["tseSignature"] = "eyJhbGci..."
                },
                ["tse"] = new JsonObject
                {
                    ["tseSignature"] = "eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9...",
                    ["qrPayload"] = "_R1-AT1_KASSE-001_AT-KASSE-001-20260228-011cfa48_2026-02-28T17:52:48_10.00_0.00_SW-TEST-abc12345_eyJhbGci...",
                    ["isDemoFiscal"] = true,
                    ["provider"] = "Demo"
                }
            };
        }
    }

    private static OpenApiRequestBody GetOrCreateRequestBody(OpenApiOperation operation)
    {
        if (operation.RequestBody is OpenApiRequestBody existing)
            return existing;

        var requestBody = new OpenApiRequestBody { Content = new Dictionary<string, OpenApiMediaType>() };
        operation.RequestBody = requestBody;
        return requestBody;
    }

    private static OpenApiResponse GetOrCreateResponse(OpenApiResponses responses, string statusCode, string description)
    {
        if (responses.TryGetValue(statusCode, out var existing) && existing is OpenApiResponse response)
            return response;

        response = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>()
        };
        responses.Add(statusCode, response);
        return response;
    }

    private static OpenApiMediaType GetOrCreateMediaType(OpenApiResponse response, string contentType)
    {
        response.Content ??= new Dictionary<string, OpenApiMediaType>();
        if (response.Content.TryGetValue(contentType, out var existing) && existing is OpenApiMediaType mediaType)
            return mediaType;

        mediaType = new OpenApiMediaType();
        response.Content.Add(contentType, mediaType);
        return mediaType;
    }
}
