using System.Text;
using LeaveService.Data;
using LeaveService.Middleware;
using LeaveService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using Steeltoe.Discovery.Client;

var builder = WebApplication.CreateBuilder(args);

var otlp = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("leave-service"))
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
        "[{Timestamp:HH:mm:ss} {Level:u3}] LEAVE | {Message:lj}{NewLine}{Exception}"));

builder.Services.AddDbContext<LeaveDbContext>(opt =>
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
builder.Services.AddHttpContextAccessor();

var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, n => TimeSpan.FromSeconds(Math.Pow(2, n)));

var circuitBreaker = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30),
        onBreak:    (_, d) => Log.Warning("Circuit OPEN - EmployeeService down for {Seconds}s", d.TotalSeconds),
        onReset:    ()     => Log.Information("Circuit CLOSED - EmployeeService recovered"),
        onHalfOpen: ()     => Log.Information("Circuit HALF-OPEN - probing EmployeeService"));

builder.Services
    .AddHttpClient<IEmployeeServiceClient, EmployeeServiceClient>(c =>
    {
        c.BaseAddress = new Uri(
            builder.Configuration["Services:EmployeeService"] ?? "http://employee-service:8080");
        c.Timeout = TimeSpan.FromSeconds(15);
    })
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(circuitBreaker);

// TODO: maybe move rabbit connect to a helper
builder.Services.AddSingleton<IEventPublisher>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<RabbitMqEventPublisher>>();
    for (var attempt = 1; attempt <= 20; attempt++)
    {
        try
        {
            return RabbitMqEventPublisher.CreateAsync(config, logger).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RabbitMQ not ready (try {Attempt}/20)", attempt);
            Thread.Sleep(5000);
        }
    }
    throw new InvalidOperationException("Could not connect to RabbitMQ");
});

builder.Services.AddDiscoveryClient(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LeaveDbContext>();
    db.Database.Migrate();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
