using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgentDeploy.Application.Parser;
using AgentDeploy.ApplicationHost.ExternalApi.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AgentDeploy.ApplicationHost.ExternalApi
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
                    .WithMethods("POST")));

            services.AddValidatedOptions<ExecutionOptions>(_configuration);
            
            services.AddSingleton<CommandSpecParser>();
            services.AddSingleton<ArgumentParser>();
            services.AddSingleton<TokenFileParser>();
            services.AddSingleton<ScriptExecutor>();
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseCors("Default");
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}