using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Wikidown.Web;
using Wikidown.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddScoped<ConnectionStore>();
builder.Services.AddScoped<IWikiBackend, GitHubBackend>();
builder.Services.AddScoped<IWikiBackend, AzureDevOpsBackend>();
builder.Services.AddScoped<BackendResolver>();

await builder.Build().RunAsync();
