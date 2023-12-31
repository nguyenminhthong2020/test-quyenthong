﻿using FluentValidation;
using FptEcommerce.API.Caching;
using FptEcommerce.API.Validators;
using FptEcommerce.Core.Interfaces.Infrastructure;
using FptEcommerce.Core.Interfaces.Services;
using FptEcommerce.Core.Services;
using FptEcommerce.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FptEcommerce.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        readonly string CorsPolicy = "_corsPolicy ";

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // add Cors
            services.AddCors(options =>
            {
                options.AddPolicy(name: CorsPolicy,
                                  builder =>
                                  {
                                      builder.AllowAnyOrigin()
                                          .AllowAnyMethod()
                                          .AllowAnyHeader();
                                  });
            });

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "FptEcommerce.API", Version = "v1" });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description =
                        "JWT Authorization header using the Bearer scheme.",
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Scheme = "Bearer",
                            Name = "Bearer",
                            In = ParameterLocation.Header
                        },
                        new List<string>()
                    }
                });
            });


            // for Redis cache
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = $"{Configuration.GetValue<string>("RedisCache:Host")}:{Configuration.GetValue<int>("RedisCache:Port")}";
            });
            services.AddSingleton<IRedisCacheService, RedisCacheService>();


            // for Auth
            var secretKey = Configuration["AppSettings:SecretKey"];
            var secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(opt =>
                {
                    opt.TokenValidationParameters = new TokenValidationParameters
                    {
                        // Đáng lẽ phải validate Issuer
                        ValidateIssuer = false,
                        ValidateAudience = false,

                        //ký vào token
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(secretKeyBytes),

                        ClockSkew = TimeSpan.Zero   // chỗ này kiểm tra xem token còn hạn không
                    };
                });


            // for Fluent Validation
            AssemblyScanner
                .FindValidatorsInAssembly(typeof(CustomerLoginValidator).Assembly)
                .ForEach(item => services.AddScoped(item.InterfaceType, item.ValidatorType));
            AssemblyScanner
                .FindValidatorsInAssembly(typeof(CustomerUpdateInfoValidator).Assembly)
                .ForEach(item => services.AddScoped(item.InterfaceType, item.ValidatorType));


            // for Services
            // IHttpContextAccessor is no longer wired up by default, you have to register it yourself
            // services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddTransient<IProductRepository>(x => new ProductRepository(Configuration.GetConnectionString("LearnDapper")));
            services.AddScoped<IProductService, ProductService>();
            services.AddTransient<ICustomerRepository>(x => new CustomerRepository(Configuration.GetConnectionString("LearnDapper")));
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddTransient<IAddressRepository>(x => new AddressRepository(Configuration.GetConnectionString("LearnDapper")));
            services.AddScoped<IAddressService, AddressService>();
            services.AddTransient<IOrderRepository>(x => new OrderRepository(Configuration.GetConnectionString("LearnDapper")));
            services.AddScoped<IOrderService, OrderService>();
            services.AddTransient<IOrderDetailRepository>(x => new OrderDetailRepository(Configuration.GetConnectionString("LearnDapper")));
            services.AddScoped<IOrderDetailService, OrderDetailService>();
            services.AddTransient<IHistoryEmailRepository>(x => new HistoryEmailRepository(Configuration.GetConnectionString("LearnDapper")));
            services.AddScoped<IHistoryEmailService, HistoryEmailService>();
            services.AddTransient<IHistoryPdfRepository>(x => new HistoryPdfRepository(Configuration.GetConnectionString("LearnDapper")));
            services.AddScoped<IHistoryPdfService, HistoryPdfService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Enable CORS
            app.UseCors(options => options.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FptEcommerce.API v1"));
            }

            app.UseHttpsRedirection();
            //app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
