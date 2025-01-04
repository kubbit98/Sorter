using Blazored.Toast;
using Sorter.Data;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    try { config.AddJsonFile("config.json", optional: true, reloadOnChange: true); }
    catch (InvalidDataException) { }
    try { config.AddJsonFile("keybinds.json", optional: true, reloadOnChange: true); }
    catch (InvalidDataException) { }
});
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


app.UseStaticFiles(new StaticFileOptions()
{
    FileProvider = app.Services.GetService<SourceDFP>(),
    RequestPath = new PathString("/src")
})
.UseStaticFiles(new StaticFileOptions()
{
    FileProvider = app.Services.GetService<DestinationDFP>(),
    RequestPath = new PathString("/dest")
})
.UseStaticFiles(new StaticFileOptions()
{
    FileProvider = app.Services.GetService<TempDFP>(),
    RequestPath = new PathString("/tmp")
});

string address = app.Configuration.GetSection("Kestrel").GetSection("Endpoints").GetSection("Http").GetValue<string>("Url");

try
{
    await app.StartAsync();
    try
    {
        Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
    }
    catch (System.ComponentModel.Win32Exception ex)
    {
        //If someone doesn't have a default browser set, this exception may occur
    }

    app.Logger.LogInformation("\nIf your browser does not open automatically, click on the link below (sometimes you have to hold down the ctrl key), or copy and paste it in your browser\n\n{address}\n\nTo shutdown the app, just hit ctrl+c or close the window", address);

    await app.WaitForShutdownAsync();
    app.Logger.LogInformation("You can close browser and console window now");

}
catch (IOException ex)
{

    app.Logger.LogCritical("The port in address {address} is currently in use, in order to run the application you must change the port in url configuration in appsettings.json. To close this window, hit ctrl+c or close the window", address);
    await app.WaitForShutdownAsync();
}
