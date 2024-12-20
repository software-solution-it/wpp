using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Hangfire;
using System.Net.WebSockets;
using WhatsAppProject.Data;
using WhatsAppProject.Services;
using Hangfire.MySql;
using MongoDB.Driver;
using WhatsAppProject.Dtos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

builder.Services.AddControllers();

builder.Services.AddDbContext<WhatsAppContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 26)),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure()
    )
    .EnableSensitiveDataLogging()
    .EnableDetailedErrors()
);

builder.Services.AddDbContext<SaasDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("SaasDatabase"),
        new MySqlServerVersion(new Version(8, 0, 26)),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure()
    )
    .EnableSensitiveDataLogging()
    .EnableDetailedErrors()
);

var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb");
var mongoDatabaseName = builder.Configuration["DatabaseName"];

builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));

builder.Services.AddScoped<MongoDbContext>(sp => new MongoDbContext(sp.GetRequiredService<IMongoClient>(), mongoDatabaseName));

builder.Services.AddScoped<WhatsAppService>();
builder.Services.AddScoped<ContactService>();
builder.Services.AddScoped<WebhookService>();
builder.Services.AddScoped<MessageSchedulingService>();
builder.Services.AddSingleton<WebSocketManager>();
builder.Services.AddHttpClient();

builder.Services.AddHangfire(config =>
{
    var storageOptions = new MySqlStorageOptions
    {
        PrepareSchemaIfNecessary = true,
        QueuePollInterval = TimeSpan.FromSeconds(15),
        JobExpirationCheckInterval = TimeSpan.FromHours(1)
    };

    config.UseStorage(new MySqlStorage(builder.Configuration.GetConnectionString("DefaultConnection"), storageOptions));
});

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 1; // Define o n�mero de workers ativos
    options.Queues = new[] { "default" }; // Define a fila padr�o
});


builder.Services.AddSingleton<IRecurringJobManager, RecurringJobManager>();
builder.Services.AddSingleton<IBackgroundJobClient, BackgroundJobClient>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "WhatsApp API", Version = "v1" });
});

var app = builder.Build();

app.UseCors("AllowAll");

app.UseWebSockets();

app.Use(async (context, next) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocketManager = context.RequestServices.GetRequiredService<WebSocketManager>();
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        var sectorId = context.Request.Query["sectorId"];

        if (!string.IsNullOrEmpty(sectorId))
        {
            webSocketManager.AddClient(sectorId, webSocket);
            await Echo(context, webSocket, webSocketManager, sectorId);
        }
        else
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Sector ID is required.", CancellationToken.None);
        }
    }
    else
    {
        await next();
    }
});

async Task Echo(HttpContext context, WebSocket webSocket, WebSocketManager webSocketManager, string sectorId)
{
    var buffer = new byte[1024 * 4];
    WebSocketReceiveResult result;

    do
    {
        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Text)
        {
            var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"Received message: {message} from sector: {sectorId}");

            try
            {
                // Converte a mensagem JSON para um objeto
                var messageData = System.Text.Json.JsonSerializer.Deserialize<MessageReadDto>(message);

                if (messageData != null && messageData.IsRead && messageData.MessageId > 0)
                {
                    Console.WriteLine($"Marking message {messageData.MessageId} as read.");

                    // Atualiza o status da mensagem no banco de dados
                    using var scope = context.RequestServices.CreateScope();
                    var whatsAppService = scope.ServiceProvider.GetRequiredService<WhatsAppService>();

                    await whatsAppService.MarkMessageAsReadAsync(messageData.MessageId);
                }
                else
                {
                    // Tratamento normal para mensagens comuns
                    await webSocketManager.SendMessageToSectorAsync(sectorId, message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }
    } while (!result.CloseStatus.HasValue);

    webSocketManager.RemoveClient(webSocket);
    await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
}

app.UseHangfireDashboard();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "WhatsApp API v1");
});

using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    recurringJobManager.AddOrUpdate(
        "ContinuousWorker",
        () => scope.ServiceProvider.GetRequiredService<MessageSchedulingService>().ScheduleAllMessagesAsync(),
        Cron.Minutely // Configura para rodar a cada minuto
    );
}


app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
