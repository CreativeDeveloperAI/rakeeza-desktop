// مضيف التطوير: يُشغَّل على Linux للتطوير واختبارات Playwright فقط، ولا يُشحن للمستخدم.
// يعرض أوامر الجسر نفسها عبر POST /invoke، ويقدّم واجهة ui/dist المبنيّة إن ضُبط DevHost:UiDist.
using Microsoft.Extensions.FileProviders;
using Rakeeza.Commands.Bridge;
using Rakeeza.Commands.System;
using Rakeeza.DevHost;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRakeezaBridge();
builder.Services.AddSingleton<IHostEnvironmentInfo, DevHostEnvironmentInfo>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/invoke", async (HttpRequest request, CommandDispatcher dispatcher, CancellationToken cancellationToken) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync(cancellationToken);
    var response = await dispatcher.DispatchJsonAsync(body, cancellationToken);
    return Results.Json(response, BridgeJson.Options);
});

var uiDist = app.Configuration["DevHost:UiDist"];
if (!string.IsNullOrWhiteSpace(uiDist))
{
    var root = Path.GetFullPath(uiDist, app.Environment.ContentRootPath);
    var files = new PhysicalFileProvider(root);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
}

await app.RunAsync();
