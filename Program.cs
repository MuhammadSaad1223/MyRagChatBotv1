using Microsoft.EntityFrameworkCore;
using MyRagChatBot.Components;
using MyRagChatBot.Data;
using MyRagChatBot.Services;

var builder = WebApplication.CreateBuilder(args);

// ----------------------
// Configuration
// ----------------------
var configuration = builder.Configuration;

// ----------------------
// Razor Components (.NET 8)
// ----------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<MarkdownService>();

// ----------------------
// Database Configuration
// ----------------------
var connectionString = configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Database connection string is missing.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// ----------------------
// Gemini AI Configuration
// ----------------------
builder.Services.AddHttpClient<GeminiAIService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    var baseUrl = config["Gemini:BaseUrl"]
                  ?? "https://generativelanguage.googleapis.com/v1beta/";

    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("User-Agent", "MyRagChatBot/1.0");
});

// ----------------------
// Application Services
// ----------------------
builder.Services.AddScoped<IGeminiAIService, GeminiAIService>();
builder.Services.AddScoped<IVectorDatabase, SqlVectorDatabase>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<RagService>();

var app = builder.Build();

// ----------------------
// Middleware Pipeline
// ----------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ----------------------
//Apply Database Migrations 
// ----------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
