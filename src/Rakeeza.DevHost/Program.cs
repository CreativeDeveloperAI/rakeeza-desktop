// مضيف التطوير: يُشغَّل على Linux للتطوير واختبارات Playwright فقط.
// طرف /invoke للأوامر يُضاف في الخطوة 0.3.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
