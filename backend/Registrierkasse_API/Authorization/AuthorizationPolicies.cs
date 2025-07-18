using Microsoft.AspNetCore.Authorization;

namespace Registrierkasse_API.Authorization
{
    public static class AuthorizationPolicies
    {
        // Policy isimleri
        public const string RequireAdmin = "RequireAdmin";
        public const string RequireManager = "RequireManager";
        public const string RequireCashier = "RequireCashier";
        public const string RequireAccountant = "RequireAccountant";
        public const string RequireAdminOrManager = "RequireAdminOrManager";
        public const string RequireAdminOrCashier = "RequireAdminOrCashier";
        public const string RequireAdminOrAccountant = "RequireAdminOrAccountant";
        public const string RequireManagerOrCashier = "RequireManagerOrCashier";
        public const string RequireManagerOrAccountant = "RequireManagerOrAccountant";

        // Policy tanımları
        public static void ConfigurePolicies(AuthorizationOptions options)
        {
            // Admin Policy
            options.AddPolicy(RequireAdmin, policy =>
                policy.RequireRole("Administrator"));

            // Manager Policy
            options.AddPolicy(RequireManager, policy =>
                policy.RequireRole("Manager"));

            // Cashier Policy
            options.AddPolicy(RequireCashier, policy =>
                policy.RequireRole("Cashier"));

            // Accountant Policy
            options.AddPolicy(RequireAccountant, policy =>
                policy.RequireRole("Accountant"));

            // Admin veya Manager
            options.AddPolicy(RequireAdminOrManager, policy =>
                policy.RequireRole("Administrator", "Manager"));

            // Admin veya Cashier
            options.AddPolicy(RequireAdminOrCashier, policy =>
                policy.RequireRole("Administrator", "Cashier"));

            // Admin veya Accountant
            options.AddPolicy(RequireAdminOrAccountant, policy =>
                policy.RequireRole("Administrator", "Accountant"));

            // Manager veya Cashier
            options.AddPolicy(RequireManagerOrCashier, policy =>
                policy.RequireRole("Manager", "Cashier"));

            // Manager veya Accountant
            options.AddPolicy(RequireManagerOrAccountant, policy =>
                policy.RequireRole("Manager", "Accountant"));
        }
    }

    // Custom Authorization Attributes
    public class RequireAdminAttribute : AuthorizeAttribute
    {
        public RequireAdminAttribute() : base(AuthorizationPolicies.RequireAdmin) { }
    }

    public class RequireManagerAttribute : AuthorizeAttribute
    {
        public RequireManagerAttribute() : base(AuthorizationPolicies.RequireManager) { }
    }

    public class RequireCashierAttribute : AuthorizeAttribute
    {
        public RequireCashierAttribute() : base(AuthorizationPolicies.RequireCashier) { }
    }

    public class RequireAccountantAttribute : AuthorizeAttribute
    {
        public RequireAccountantAttribute() : base(AuthorizationPolicies.RequireAccountant) { }
    }

    public class RequireAdminOrManagerAttribute : AuthorizeAttribute
    {
        public RequireAdminOrManagerAttribute() : base(AuthorizationPolicies.RequireAdminOrManager) { }
    }

    public class RequireAdminOrCashierAttribute : AuthorizeAttribute
    {
        public RequireAdminOrCashierAttribute() : base(AuthorizationPolicies.RequireAdminOrCashier) { }
    }

    public class RequireAdminOrAccountantAttribute : AuthorizeAttribute
    {
        public RequireAdminOrAccountantAttribute() : base(AuthorizationPolicies.RequireAdminOrAccountant) { }
    }

    public class RequireManagerOrCashierAttribute : AuthorizeAttribute
    {
        public RequireManagerOrCashierAttribute() : base(AuthorizationPolicies.RequireManagerOrCashier) { }
    }

    public class RequireManagerOrAccountantAttribute : AuthorizeAttribute
    {
        public RequireManagerOrAccountantAttribute() : base(AuthorizationPolicies.RequireManagerOrAccountant) { }
    }
} 
