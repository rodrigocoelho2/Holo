using TcgStockBot.Website;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTcgStockBotWebsite(builder.Configuration);

var app = builder.Build();
app.UseTcgStockBotWebsite();
await app.RunAsync();
