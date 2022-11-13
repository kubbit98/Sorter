using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.FileProviders;
using Sortownik.Data;
using Microsoft.Extensions.Configuration;
/*using ElectronNET.API; */

var builder = WebApplication.CreateBuilder(args);

/*builder.WebHost.UseElectron(args);*/

builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    config.AddJsonFile("config.json",
                       optional: false,
                       reloadOnChange: true);
});

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddSingleton<FileService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

/*if (HybridSupport.IsElectronActive)
{
    var window = await Electron.WindowManager.CreateWindowAsync();
    window.OnClosed += () => {
        Electron.App.Quit();
    };
}*/

/*Task.Run(async () => await Electron.WindowManager.CreateWindowAsync());*/

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.UseStaticFiles(new StaticFileOptions()
{
    FileProvider = new PhysicalFileProvider(builder.Configuration["Source"]),
    RequestPath = new PathString("/src")
})
.UseStaticFiles(new StaticFileOptions()
{
    FileProvider = new PhysicalFileProvider(builder.Configuration["Destination"]),
    RequestPath = new PathString("/dest")
});

/*if (HybridSupport.IsElectronActive)
{
    CreateWindow();
}
async void CreateWindow()
{
    var window = await Electron.WindowManager.CreateWindowAsync();
    var Webwindow = await Electron.WindowManager.CreateBrowserViewAsync();
    window.OnClose += Window_OncClosed;
}
void Window_OncClosed()
{
    Electron.App.Exit();
}*/

app.Run();