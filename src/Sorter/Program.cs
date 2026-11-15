using Blazored.Toast;
using Microsoft.AspNetCore.StaticFiles;
using Sorter.Data;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("config.json", optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile("keybinds.json", optional: true, reloadOnChange: true);
builder.WebHost.UseStaticWebAssets();
// Add services to the container.
builder.Services.AddOptions<ConfigOptions>().BindConfiguration(ConfigOptions.config);
builder.Services.AddOptions<KeyBindsOptions>().BindConfiguration(KeyBindsOptions.config);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddBlazoredToast();

builder.Services.AddSingleton<FileService>();
builder.Services.AddSingleton<ConfigOptionsService>();
builder.Services.AddSingleton(provider => new SourceDFP(builder.Configuration.GetSection(ConfigOptions.config).GetValue<string>("Source"), provider.GetRequiredService<ILogger<SourceDFP>>()));
builder.Services.AddSingleton(provider => new DestinationDFP(builder.Configuration.GetSection(ConfigOptions.config).GetValue<string>("Destination"), provider.GetRequiredService<ILogger<DestinationDFP>>()));
builder.Services.AddSingleton(provider => new TempDFP(builder.Configuration.GetValue<string>("TempPath"), provider.GetRequiredService<ILogger<TempDFP>>()));
// TempDFP falls back to "Temp" as well, both must point at the same folder
builder.Services.AddSingleton<IThumbnailGenerator>(_ => new ImageSharpThumbnailGenerator(
    string.IsNullOrWhiteSpace(builder.Configuration.GetValue<string>("TempPath")) ? "Temp" : builder.Configuration.GetValue<string>("TempPath")!));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");


// supported extensions missing from the default mappings (static files middleware returns 404 without them)
// or mapped to an outdated type
var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".apng"] = "image/apng";
contentTypeProvider.Mappings[".avif"] = "image/avif";
contentTypeProvider.Mappings[".jfif"] = "image/jpeg";
contentTypeProvider.Mappings[".m3u8"] = "application/vnd.apple.mpegurl";
contentTypeProvider.Mappings[".m4p"] = "audio/mp4";
contentTypeProvider.Mappings[".m4b"] = "audio/mp4";
contentTypeProvider.Mappings[".m4r"] = "audio/mp4";

app.UseStaticFiles(new StaticFileOptions()
{
    FileProvider = app.Services.GetService<SourceDFP>(),
    RequestPath = new PathString("/src"),
    ContentTypeProvider = contentTypeProvider
})
.UseStaticFiles(new StaticFileOptions()
{
    FileProvider = app.Services.GetService<DestinationDFP>(),
    RequestPath = new PathString("/dest"),
    ContentTypeProvider = contentTypeProvider
})
.UseStaticFiles(new StaticFileOptions()
{
    FileProvider = app.Services.GetService<TempDFP>(),
    RequestPath = new PathString("/tmp"),
    ContentTypeProvider = contentTypeProvider
});

// Kestrel listens on http://localhost:5000 when no endpoint is configured
string address = app.Configuration.GetSection("Kestrel").GetSection("Endpoints").GetSection("Http").GetValue<string>("Url") ?? "http://localhost:5000";

try
{
    await app.StartAsync();
    try
    {
        Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
    }
    catch (System.ComponentModel.Win32Exception)
    {
        //If someone doesn't have a default browser set, this exception may occur
    }

    app.Logger.LogInformation("\nIf your browser does not open automatically, click on the link below (sometimes you have to hold down the ctrl key), or copy and paste it in your browser\n\n{address}\n\nTo shutdown the app, just hit ctrl+c or close the window", address);

    await app.WaitForShutdownAsync();
    app.Logger.LogInformation("You can close browser and console window now");

}
catch (IOException)
{

    app.Logger.LogCritical("The port in address {address} is currently in use, in order to run the application you must change the port in url configuration in appsettings.json. To close this window, hit ctrl+c or close the window", address);
    await app.WaitForShutdownAsync();
}
