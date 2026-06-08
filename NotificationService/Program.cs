using NotificationService.Consumers;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Steeltoe.Discovery.Client;

var builder = WebApplication.CreateBuilder(args);

var otlp = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("notification-service"))
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
        "[{Timestamp:HH:mm:ss} {Level:u3}] NOTIFY | {Message:lj}{NewLine}{Exception}"));

builder.Services.AddHostedService<LeaveEventConsumer>();
builder.Services.AddControllers();
builder.Services.AddDiscoveryClient(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapControllers();
app.MapHealthChecks("/health");

app.MapGet("/api/notifications/status", () => new
{
    status  = "Running",
    service = "NotificationService",
    note    = "Check docker logs for notification output"
});

app.Run();
