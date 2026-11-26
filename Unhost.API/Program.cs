using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Unhost.API.Endpoints;
using Unhost.Application.Models.Database;
using Unhost.Infrastructure.Database;
using Unhost.Infrastructure.Docker;

var builder = WebApplication.CreateSlimBuilder(args);
var dataDirectory = Path.GetFullPath(builder.Configuration["UnHost:DataDirectory"] ?? ".");
Directory.CreateDirectory(dataDirectory);
var connectionString = new SqliteConnectionStringBuilder(UnhostDbContext.DefaultConnectionString)
{
    DataSource = Path.Combine(dataDirectory, "unhost.db")
}.ToString();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddIdentityCore<OperatorUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddUserStore<EfOperatorStore>()
    .AddUserManager<AspNetUserManager<OperatorUser>>()
    .AddSignInManager();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();

builder.Services.AddValidation();

builder.Services.AddDataProtection()
    .SetApplicationName("UnHost")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "DataProtection-Keys")));

builder.Services.AddDbContext<UnhostDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddScoped<FirstRunSetup>();
builder.Services.AddScoped<EfOperatorStore>();
builder.Services.AddScoped<LocalSwarmStore>();
var dockerSocketPath = builder.Configuration["UnHost:DockerSocketPath"] ?? "/var/run/docker.sock";
builder.Services.AddSingleton(_ => new DockerService(dockerSocketPath));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

SqliteMigrations.Apply(connectionString);

app.UseDefaultFiles();
app.UseStaticFiles();

var apiEndpoints = app.MapGroup("/api")
    .AddEndpointFilter(ValidateCsrfToken)
    .DisableCookieRedirect();

apiEndpoints.MapIdentityEndpoints();
apiEndpoints.MapStatusEndpoints();
apiEndpoints.MapDockerEndpoints();

app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();

static async ValueTask<object?> ValidateCsrfToken(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
{
    var httpContext = context.HttpContext;
    var csrfProtection = httpContext.RequestServices.GetRequiredService<ICsrfProtection>();
    var result = await csrfProtection.ValidateAsync(httpContext);

    if (!result.IsAllowed)
    {
        return TypedResults.Forbid();
    }
    return await next(context);
}

public partial class Program;
