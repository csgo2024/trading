using Trading.API.Extensions;
using Trading.Application.Middlerwares;
using Trading.Application.Queries;
using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.API;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<CredentialSettings>(Configuration.GetSection("CredentialSettings"));
        services.Configure<string>(Configuration.GetSection("PrivateKey"));

        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                builder =>
                {
                    builder
                        .AllowAnyOrigin()        // 允许任何来源
                        .AllowAnyMethod()        // 允许任何HTTP方法
                        .AllowAnyHeader();       // 允许任何请求头
                }
            );
        });

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddMongoDb(Configuration);
        services.AddTelegram(Configuration);

        services.AddScoped<IStrategyQuery, StrategyQuery>();
        services.AddSingleton<ICredentialQuery, CredentialQuery>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining(typeof(Program));

        });
        services.AddTradingServices();
        services.AddBinance(Configuration);
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        app.UseCors("AllowAll");  // 使用允许所有的策略
        app.UseExceptionHandlingMiddleware();
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseRouting();

        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
