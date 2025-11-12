using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using was.api.Middleware;
using was.api.Models;
using was.api.Services.Auth;
using was.api.Services.Coms;
using was.api.Services.Forms;


// Add services to the container.

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("Starting up");

    builder.Services.Configure<Settings>(builder.Configuration);

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var configuration = builder.Configuration;

    // services start

    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IUserContextService, UserContextService>();
    builder.Services.AddScoped<IUserManagementService, UserManagementService>();
    builder.Services.AddScoped<IFormsService, FormsService>();
    builder.Services.AddScoped<IEmailService, EmailService>();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // services end
    builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    }).AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"]))
        };
    });

    //builder.Services.AddHostedService<ReminderEmailScheduler>();
    //builder.Services.AddHostedService<RemainsOpenEmailReminder>();


    builder.WebHost.ConfigureKestrel(options =>
    {

        options.ListenAnyIP(5000); // HTTP
        //options.ListenAnyIP(5001, listenOptions =>
        //{
        //    listenOptions.UseHttps(); // optional dev cert
        //});

        //if (builder.Environment.IsDevelopment())
        //{
        //    // Development: Use HTTP only or dev certificate
        //    options.ListenAnyIP(5000); // HTTP
        //    options.ListenAnyIP(5001, listenOptions =>
        //    {
        //        listenOptions.UseHttps(); // optional dev cert
        //    });
        //}
        //else
        //{
        //    // Production: Use wildcard certificate from Windows store
        //    options.ListenAnyIP(443, listenOptions =>
        //    {
        //        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        //        store.Open(OpenFlags.ReadOnly);
        //        var certificate = store.Certificates
        //            .Find(X509FindType.FindBySubjectName, "qubesafe.indiqube.com", validOnly: true)
        //            .OfType<X509Certificate2>()
        //            .FirstOrDefault() ?? throw new Exception("Certificate not found");

        //        listenOptions.UseHttps(certificate);
        //    });
        //}
    });


    var app = builder.Build();

    app.UseMiddleware<ErrorHandlingMiddleware>();

    // Configure the HTTP request pipeline.
    //if (app.Environment.IsDevelopment())
    //{
        app.UseSwagger();
        app.UseSwaggerUI();
   // }

    app.UseAuthentication();
  
    app.UseHttpsRedirection();
    app.UseCors("AllowAll");
    app.UseAuthorization();

    app.MapControllers();

    app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
