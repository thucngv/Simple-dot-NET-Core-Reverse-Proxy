using ReverseProxy;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseMiddleware<ReverseProxyMiddleware>();

app.Run();
