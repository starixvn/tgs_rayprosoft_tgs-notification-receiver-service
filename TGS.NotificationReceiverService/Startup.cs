using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using TGS.NotificationReceiverService.ReceiveHandlers;

namespace TGS.NotificationReceiverService
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                string[] xmlFileNames = null;
                xmlFileNames = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.xml");
                foreach (var xmlFileName in xmlFileNames)
                {
                    c.IncludeXmlComments(xmlFileName);
                }

                c.SwaggerDoc("v1", new OpenApiInfo { Title = "TGS.NotificationReceiverService", Version = "v1" });
            });

            services.AddHttpClient();

            services.Configure<TelegramSettings>(Configuration.GetSection("Telegram"));
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<TelegramSettings>>().Value);
            services.AddHealthChecks();
            services
                .AddRabbitMessageQueue(config =>
                {
                    config
                        .ConfigQueueOptions((options, sp) =>
                        {
                            var configuration = sp.GetRequiredService<IConfiguration>();

                            configuration.GetSection("RabbitMQ").Bind(options);
                            options.OnSetupQueueAndExchange = channel =>
                            {
                                channel.QueueDeclare(
                                    "report.error",
                                    true,
                                    false,
                                    false,
                                    null);

                                channel.QueueBind(
                                    "report.error",
                                    "amq.direct",
                                    "report.error",
                                    new Dictionary<string, object>());
                            };
                        })
                        .AddHandler<ReportReceiveHandler>("report.error", false);
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c
                    => c.SwaggerEndpoint("/swagger/v1/swagger.json", "TGS.NotificationReceiverService v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                endpoints.MapHealthChecks("/health", new()
                {
                    ResultStatusCodes = new Dictionary<HealthStatus, int>
                    {
                        { HealthStatus.Healthy, StatusCodes.Status200OK },
                        { HealthStatus.Unhealthy, StatusCodes.Status503ServiceUnavailable },
                        { HealthStatus.Degraded, StatusCodes.Status500InternalServerError }
                    },
                    ResponseWriter = async (context, report) =>
                    {
                        var hostName = Dns.GetHostName();
                        var hostEntry = await Dns.GetHostEntryAsync(hostName);
                        var hostIp = Array.Find(hostEntry.AddressList,
                            x => x.AddressFamily == AddressFamily.InterNetwork);
                        var response = new
                        {
                            Data = new
                            {
                                Environment.MachineName,
                                HostName = hostName,
                                HostIp = hostIp?.ToString() ?? string.Empty,
                                Environment = env.EnvironmentName,
                                OsVersion = $"{Environment.OSVersion}",
                                Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                                ProcessCount = Environment.ProcessorCount,
                                AdditionalValues = new Dictionary<string, object>()
                            },
                            Result = new
                            {
                                Status = report.Status.ToString(),
                                TotalDuration = report.TotalDuration.TotalSeconds,
                                DependencyServices = report.Entries.Select(srv => new
                                {
                                    srv.Key,
                                    Duration = srv.Value.Duration.TotalSeconds,
                                    srv.Value.Description,
                                    srv.Value.Data,
                                    Exception = srv.Value.Exception?.Message,
                                    Status = srv.Value.Status.ToString(),
                                    Tags = srv.Value.Tags.Any() ? string.Join(',', srv.Value.Tags) : string.Empty
                                }).ToArray()
                            }
                        };
                        var json = JsonSerializer.Serialize(response, new() { WriteIndented = true });
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(json);
                    }
                });
            });
        }
    }
}