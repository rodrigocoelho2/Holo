using TcgStockBot.WebApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTcgStockBot(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseTcgStockBot();
await app.RunAsync();
