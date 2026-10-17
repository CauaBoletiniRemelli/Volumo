using System.Diagnostics;
using Serilog;
using Volumo.Api.Auth;
using Volumo.Api.Errors;
using Volumo.Api.Security;
using Volumo.Api.Sessoes;
using Volumo.Api.Treinos;
using Volumo.Infrastructure;
using Volumo.Infrastructure.Persistencia;

const long maxRequestBodyBytes = 1_048_576; // 1 MB; uploads terão limite próprio

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = maxRequestBodyBytes;
});
builder.Services.Configure<IISServerOptions>(options => options.MaxRequestBodySize = maxRequestBodyBytes);

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<BusinessRuleExceptionHandler>();

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

builder.Services.AddVolumoCors(builder.Configuration);
builder.Services.AddVolumoRateLimiting(builder.Configuration);
builder.Services.AddVolumoInfrastructure(builder.Configuration.GetConnectionString(DependencyInjection.ConnectionStringName));
builder.Services.AddVolumoAuth(builder.Configuration, builder.Environment);
builder.Services.AddScoped<PlanMapper>();
builder.Services.AddScoped<SessionSynchronizer>();
builder.Services.AddScoped<SessionQueries>();
builder.Services.AddHealthChecks().AddDbContextCheck<VolumoDbContext>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();
