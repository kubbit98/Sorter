using Blazored.Toast;
using Sorter.Data;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    try { config.AddJsonFile("config.json", optional: true, reloadOnChange: true); }
    catch (InvalidDataException) { }

});
builder.WebHost.UseStaticWebAssets();
// Add services to the container.
builder.Services.AddOptions<ConfigOptions>().BindConfiguration(ConfigOptions.config);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddBlazoredToast();

builder.Services.AddSingleton<FileService>();
builder.Services.AddSingleton<ConfigOptionsService>();
builder.Services.AddSingleton(_ => new SourceDFP(builder.Configuration.GetSection(ConfigOptions.config).GetValue<string>("Source")));
builder.Services.AddSingleton(_ => new DestinationDFP(builder.Configuration.GetSection(ConfigOptions.config).GetValue<string>("Destination")));
builder.Services.AddSingleton(_ => new TempDFP(builder.Configuration.GetValue<string>("TempPath")));

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

await app.StartAsync();
Process.Start(new ProcessStartInfo("http://localhost:5000") { UseShellExecute = true });
Console.WriteLine("If your browser does not open automatically, click on the link below (sometimes you have to hold down the ctrl key), or copy and paste it in your browser");
Console.WriteLine("\nhttp://localhost:5000\n");
Console.WriteLine("To shutdown the app, just hit ctrl+c");
await app.WaitForShutdownAsync();
Console.WriteLine("You can close browser and console window now");
