using EbenezerBackend.Docs.Scalar;
using EbenezerBackend.Infrastructure.Extensions.ServiceCollection;
using EbenezerBackend.Infrastructure.Middleware;
using EbenezerBackend.Shared.Configurations;
using EbenezerBackend.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var variables = new Variables();

builder.Services.AddControllers();
builder.Services.AddArangoDb(builder.Configuration);
builder.Services.AddCustomIdentity();
builder.Services.AddVariables();
builder.Services.AddJwtAuthenticationService(variables);
builder.Services.AddSharedServices();
builder.Services.AddModules();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<SecuritySchemeTransformer>();
});

var app = builder.Build();

await app.UseArangoDbInitialization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.AddPreferredSecuritySchemes("Bearer"));
}

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionHandlerMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
