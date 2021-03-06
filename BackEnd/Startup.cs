using AutoMapper;
using BackEnd.Filtros;
using BackEnd.Utilidades;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackEnd
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //en BD y localmente (wwwroot)
            services.AddTransient<IAlmacenadorArchivos, AlmacenadorArchivosLocal>();
            services.AddHttpContextAccessor();
            //en BD y AzureStorage
            //services.AddTransient<IAlmacenadorArchivos, AlmacenadorAzureStorage>();
            //Automaper DTO - CONTROLLERS
            services.AddAutoMapper(typeof(Startup));
            //Para la ubicacion
            services.AddSingleton(provider =>
                new MapperConfiguration(config =>
                {
                    var geometryFactory = provider.GetRequiredService<GeometryFactory>();
                    config.AddProfile(new AutoMapperProfiles(geometryFactory));
                }).CreateMapper());
            

            //Base de datos 
            services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(Configuration.GetConnectionString("BDPeliculasAngular"),
                    sqlServer => sqlServer.UseNetTopologySuite()));//Para activar querys espaciales

            //Para poder usar distancias
            services.AddSingleton<GeometryFactory>(NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326));

            /* CORS */
            services.AddCors(options => {

                /* Desde appsetting en desarrollo */
                var frontedURL = Configuration.GetValue<string>("Frontend_URL");

                options.AddDefaultPolicy(builder =>{
                    builder.WithOrigins(frontedURL).AllowAnyMethod().AllowAnyHeader()
                            .WithExposedHeaders(new string[] {"cantidadTotalRegistros"} );
                });
            });

            //Servicios de Identity
            services.AddIdentity<IdentityUser, IdentityRole>()
                                .AddEntityFrameworkStores<ApplicationDbContext>()
                                .AddDefaultTokenProviders();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                                .AddJwtBearer(opciones => 
                                    opciones.TokenValidationParameters = new TokenValidationParameters
                                    {
                                        ValidateIssuer=false,
                                        ValidateAudience = false,
                                        //Duracion del token
                                        ValidateLifetime= true,
                                        //Firma llave privada
                                        ValidateIssuerSigningKey= true,
                                        //configurar la firma con una llave
                                        IssuerSigningKey = new SymmetricSecurityKey(
                                            Encoding.UTF8.GetBytes(Configuration["llavejwt"])),
                                        ClockSkew= TimeSpan.Zero
                                    });
                                    //Ir a app.development.json

            services.AddControllers( options => {
                options.Filters.Add(typeof(FiltroDeExcepcion));
            });
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "BackEnd", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "BackEnd v1"));
            }

            app.UseHttpsRedirection();
            //Guardar archivos localmente
            app.UseStaticFiles();

            app.UseRouting();

            app.UseCors();

            app.UseAuthentication();

            app.UseAuthorization(); 

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
