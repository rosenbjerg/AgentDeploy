using AgentDeploy.ExternalApi.Middleware;
using AgentDeploy.Models.Options;
using AgentDeploy.Services;
using AgentDeploy.Services.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentDeploy.ExternalApi
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options => options
                .AddPolicy("Default", cors => cors
                    .AllowAnyOrigin()
                    .WithHeaders("Authorization")
                    .WithMethods("POST")));

            services.AddValidatedOptions<ExecutionOptions>(_configuration);
            services.AddValidatedOptions<DirectoryOptions>(_configuration);
            
            services.AddScoped<CommandReader>();
            services.AddScoped<ExecutionContextService>();
            services.AddScoped<TokenReader>();
            services.AddScoped<ScriptExecutionService>();
            services.AddScoped<ScriptTransformer>();
            services.AddScoped<LocalScriptExecutor>();
            services.AddScoped<SecureShellExecutor>();
            services.AddScoped<OperationContext>();
            services.AddScoped<IOperationContext>(provider => provider.GetRequiredService<OperationContext>());
            services.AddSingleton(_ => new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build());
            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors("Default");
            app.UseMiddleware<AuthenticationMiddleware>();
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}