using MudBlazor.Services;
using Nagare.Application;
using Nagare.Infrastructure;
using Nagare.WinApp.Components;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server + MudBlazor.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// Application (CQRS handlers, coordinator) and Infrastructure (ffmpeg, Data Protection, JSON).
builder.Services.AddNagareApplication();
builder.Services.AddNagareInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
