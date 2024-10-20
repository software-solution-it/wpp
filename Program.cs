using Microsoft.EntityFrameworkCore;
using WhatsAppProject.Data;
using WhatsAppProject.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configuração do DbContext para MySQL
builder.Services.AddDbContext<WhatsAppContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
    new MySqlServerVersion(new Version(8, 0, 26)))); // Altere a versão conforme necessário

// Adicionar serviços de WhatsApp e HttpClient
builder.Services.AddScoped<WhatsAppService>();
builder.Services.AddHttpClient(); // Para enviar requisições HTTP

// Adicionar configuração do Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "WhatsApp API", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    // Habilitar o Swagger
    app.UseSwagger();

    // Configurar a UI do Swagger para servir no caminho raiz
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WhatsApp API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
