using FluentValidation;
using Lectures.Api.Data;
using Lectures.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program;
