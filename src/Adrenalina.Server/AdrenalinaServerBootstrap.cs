using Adrenalina.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Adrenalina.Server;

public static class AdrenalinaServerBootstrap
{
    public static WebApplication BuildApplication(AdrenalinaServerHostOptions? options = null)
    {
        options ??= new AdrenalinaServerHostOptions();

        var builderOptions = new WebApplicationOptions
        {
            Args = options.Args,
            ApplicationName = typeof(AdrenalinaServerBootstrap).Assembly.FullName,
            ContentRootPath = options.ContentRootPath ?? AppContext.BaseDirectory,
            WebRootPath = options.WebRootPath ?? Path.Combine(options.ContentRootPath ?? AppContext.BaseDirectory, "wwwroot")
        };

        var builder = WebApplication.CreateBuilder(builderOptions);

        if (!string.IsNullOrWhiteSpace(options.DataRootPath))
        {
            builder.Configuration["Adrenalina:RootDirectory"] = options.DataRootPath;
        }

        if (!string.IsNullOrWhiteSpace(options.Urls))
        {
            builder.WebHost.UseUrls(options.Urls);
        }

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookieOptions =>
            {
                cookieOptions.LoginPath = "/auth/login";
                cookieOptions.AccessDeniedPath = "/auth/login";
                cookieOptions.Cookie.Name = "Adrenalina.Admin";
                cookieOptions.SlidingExpiration = true;
            });

        builder.Services.AddAuthorization();
        builder.Services.AddControllersWithViews();
        builder.Services.AddAdrenalinaServerPlatform(builder.Configuration, builder.Environment);

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/dashboard");
            app.UseHsts();
        }

        if (options.UseHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Dashboard}/{action=Index}/{id?}");

        return app;
    }
}
