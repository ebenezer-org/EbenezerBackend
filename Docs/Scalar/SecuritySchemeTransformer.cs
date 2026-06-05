using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Build.Evaluation;
using Microsoft.OpenApi;

namespace EbenezerBackend.Docs.Scalar;

internal sealed class SecuritySchemeTransformer(IAuthenticationSchemeProvider authProvider) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument doc, OpenApiDocumentTransformerContext ctx, CancellationToken ct)
    {
        var schemes = await authProvider.GetAllSchemesAsync();
        
        if (schemes.Any(s => s.Name == "Bearer"))
        {
            doc.Components ??= new OpenApiComponents();
            
            doc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

            doc.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
            };

            foreach (var operation in doc.Paths.Values.SelectMany(p => p.Operations?.Values))
            {
                operation.Security ??= new List<OpenApiSecurityRequirement>();
                
                operation.Security.Add(new OpenApiSecurityRequirement 
                { 
                    [new OpenApiSecuritySchemeReference("Bearer", doc)] = [] 
                });
            }
        }
    }
}