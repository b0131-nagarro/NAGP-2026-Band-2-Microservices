using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Eureka;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Steeltoe.Discovery.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

var otlp = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("api-gateway"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(otlp);
            o.Protocol = OtlpExportProtocol.Grpc;
        }));

builder.Services.AddSerilog(cfg => cfg
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] GATEWAY | {Message:lj}{NewLine}{Exception}"));

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer", o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
        ValidateIssuer           = true,  ValidIssuer   = jwt["Issuer"],
        ValidateAudience         = true,  ValidAudience = jwt["Audience"],
        ValidateLifetime         = true,  ClockSkew     = TimeSpan.Zero
    });

builder.Services.AddAuthorization();
builder.Services.AddDiscoveryClient(builder.Configuration);
builder.Services.AddOcelot(builder.Configuration).AddEureka();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

// health before ocelot (ocelot doesn't have /health)
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/health"))
    {
        await next(context);
        return;
    }

    var report = await context.RequestServices
        .GetRequiredService<HealthCheckService>()
        .CheckHealthAsync(context.RequestAborted);

    context.Response.StatusCode = report.Status == HealthStatus.Healthy ? 200 : 503;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { status = report.Status.ToString(), service = "api-gateway" });
});

await app.UseOcelot();

app.Run();
