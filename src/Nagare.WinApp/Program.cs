using MudBlazor.Services;
using Nagare.Application;
using Nagare.Infrastructure;
using Nagare.WinApp.Components;

var builder = WebApplication.CreateBuilder(args);

// Machine-local configuration (ffmpeg paths, optional test channel) lives in User Secrets,
// never in the repository. Loaded explicitly rather than relying on the host's implicit
// Development-only behaviour: the WinUI 3 host (ADR-0006) will not have it.
// User Secrets are NOT encrypted — they hold dev configuration only. Real channel keys are
// protected at rest by Data Protection/DPAPI (ADR-0005).
#if DEBUG
builder.Configuration.AddUserSecrets<Program>(optional: true);
#endif

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
