using System.Text;
using EmployeeService.Data;
using EmployeeService.Middleware;
using EmployeeService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Steeltoe.Discovery.Client;

var builder = WebApplication.CreateBuilder(args);

var otlp = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("employee-service"))
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
        "[{Timestamp:HH:mm:ss} {Level:u3}] EMPLOYEE | {Message:lj}{NewLine}{Exception}"));

builder.Services.AddDbContext<EmployeeDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
        ValidateIssuer           = true,  ValidIssuer   = jwt["Issuer"],
        ValidateAudience         = true,  ValidAudience = jwt["Audience"],
        ValidateLifetime         = true,  ClockSkew     = TimeSpan.Zero
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddDiscoveryClient(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EmployeeDbContext>();
    db.Database.Migrate();

    var runSeed = !string.Equals(builder.Configuration["RunDbSeed"], "false", StringComparison.OrdinalIgnoreCase);
    if (runSeed)
        await DataSeeder.SeedAsync(db);
}

app.UseMiddleware<GlobalExceptionMiddleware>();
// header so we can see which instance handled the request (load balance demo)
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Service-Instance"] =
            Environment.GetEnvironmentVariable("INSTANCE_ID") ?? "employee-1";
        return Task.CompletedTask;
    });
    await next();
});
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
