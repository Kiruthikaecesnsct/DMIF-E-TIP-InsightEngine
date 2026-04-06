using InsightEngine.Components;
using InsightEngine.Repositories;
using InsightEngine.Services;
using InsightEngine.Services.Agents;
using InsightEngine.Services.Orchestration;
using Microsoft.SemanticKernel;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();
// Semantic Kernel — single shared instance for all agents
builder.Services.AddSingleton<SemanticKernelConfig>();
builder.Services.AddSingleton<Kernel>(sp =>
{
    var config = sp.GetRequiredService<SemanticKernelConfig>();
    return config.CreateKernel();
});
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddScoped<SchemaIntrospectionService>();
builder.Services.AddScoped<QueryInterpreterAgent>();
builder.Services.AddScoped<SqlExecutorService>();
builder.Services.AddScoped<DataAnalystAgent>();
builder.Services.AddScoped<VisualizationAgent>();
builder.Services.AddScoped<AnalysisPipelineService>();
builder.Services.AddScoped<AnomalyDetectionService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<IDataSourceRepository, DataSourceRepository>();
builder.Services.AddScoped<IQueryRepository, QueryRepository>();


// Controllers for API endpoints 
builder.Services.AddControllers();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
