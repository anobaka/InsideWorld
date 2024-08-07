﻿using Bakabase.Infrastructures.Components.App;
using Bakabase.Infrastructures.Components.App.Upgrade.Adapters;
using Bakabase.Infrastructures.Components.Jobs;
using Bakabase.Infrastructures.Components.Orm;
using Bakabase.InsideWorld.App.Core.Extensions;
using Bakabase.InsideWorld.Business;
using Bakabase.InsideWorld.Business.Components.Caching;
using Bakabase.InsideWorld.Business.Components.Compression;
using Bakabase.InsideWorld.Business.Components.CookieValidation;
using Bakabase.InsideWorld.Business.Components.CookieValidation.Infrastructures;
using Bakabase.InsideWorld.Business.Components.Dependency.Abstractions;
using Bakabase.InsideWorld.Business.Components.Dependency.Implementations.BakabaseUpdater;
using Bakabase.InsideWorld.Business.Components.Dependency.Implementations.FfMpeg;
using Bakabase.InsideWorld.Business.Components.Dependency.Implementations.Lux;
using Bakabase.InsideWorld.Business.Components.Downloader;
using Bakabase.InsideWorld.Business.Components.Downloader.Abstractions;
using Bakabase.InsideWorld.Business.Components.Downloader.DownloaderOptionsValidator;
using Bakabase.InsideWorld.Business.Components.Downloader.Implementations;
using Bakabase.InsideWorld.Business.Components.FileExplorer;
using Bakabase.InsideWorld.Business.Components.FileMover;
using Bakabase.InsideWorld.Business.Components.Gui;
using Bakabase.InsideWorld.Business.Components.Gui.Extensions;
using Bakabase.InsideWorld.Business.Components.Jobs;
using Bakabase.InsideWorld.Business.Components.Network;
using Bakabase.InsideWorld.Business.Components.Tasks;
using Bakabase.InsideWorld.Business.Components.ThirdParty.Bilibili;
using Bakabase.InsideWorld.Business.Components.ThirdParty.ExHentai;
using Bakabase.InsideWorld.Business.Components.ThirdParty.Http;
using Bakabase.InsideWorld.Business.Components.ThirdParty.Implementations;
using Bakabase.InsideWorld.Business.Components.ThirdParty.JavLibrary;
using Bakabase.InsideWorld.Business.Components.ThirdParty.Pixiv;
using Bakabase.InsideWorld.Business.Configurations;
using Bakabase.InsideWorld.Business.Resources;
using Bakabase.InsideWorld.Models.Constants;
using Bootstrap.Components.DependencyInjection;
using Bootstrap.Components.Doc.Swagger;
using Bootstrap.Components.Orm.Extensions;
using Bootstrap.Components.Storage.OneDrive;
using Bootstrap.Components.Tasks.Progressor;
using InsideWorld.Migrations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bakabase.InsideWorld.App.Core
{
    public class InsideWorldStartup : AppStartup<SwaggerCustomModelDocumentFilter>
    {
        public InsideWorldStartup(IConfiguration configuration, IWebHostEnvironment env) : base(configuration, env)
        {
        }

        protected override void ConfigureServicesBeforeOthers(IServiceCollection services)
        {
            services.AddInsideWorldBusinesses();

            services.AddSingleton<SimpleJobManager, InsideWorldJobManager>();

            services.AddSingleton<IwFsEntryTaskManager>();
            services.AddSingleton<BackgroundTaskManager>();

            //services.TryAddSingleton<SimpleBiliBiliFavoritesCollector>();
            services.AddSingleton<OneDriveService>();

            services.AddBootstrapServices<InsideWorldDbContext>(c =>
                c.UseBootstrapSqLite(AppDataPath, "bakabase_insideworld"));

            services.AddInsideWorldHttpClient<InsideWorldHttpClientHandler>(BusinessConstants.HttpClientNames.Default);

            services.AddInsideWorldHttpClient<JavLibraryThirdPartyHttpMessageHandler>(BusinessConstants.HttpClientNames
                .JavLibrary);
            services.AddSimpleProgressor<JavLibraryDownloader>();

            services.AddSingleton<DownloaderManager>();

            services.TryAddSingleton<ThirdPartyHttpRequestLogger>();

            services.AddInsideWorldHttpClient<BilibiliThirdPartyHttpMessageHandler>(BusinessConstants.HttpClientNames
                .Bilibili);
            services.TryAddSingleton<BilibiliClient>();
            services.AddTransient<BilibiliDownloader>();
            services.TryAddSingleton<BilibiliDownloaderOptionsValidator>();
            services.TryAddSingleton<BilibiliCookieValidator>();

            services.AddInsideWorldHttpClient<ExHentaiThirdPartyHttpMessageHandler>(BusinessConstants.HttpClientNames
                .ExHentai);
            services.TryAddSingleton<ExHentaiClient>();
            services.AddTransient<ExHentaiSingleWorkDownloader>();
            services.AddTransient<ExHentaiListDownloader>();
            services.AddTransient<ExHentaiWatchedDownloader>();
            services.TryAddSingleton<ExHentaiDownloaderOptionsValidator>();
            services.TryAddSingleton<ExHentaiCookieValidator>();

            services.AddInsideWorldHttpClient<PixivThirdPartyHttpMessageHandler>(
                BusinessConstants.HttpClientNames.Pixiv);
            services.TryAddSingleton<PixivClient>();
            services.AddTransient<PixivSearchDownloader>();
            services.AddTransient<PixivRankingDownloader>();
            services.AddTransient<PixivFollowingDownloader>();
            services.TryAddSingleton<PixivDownloaderOptionsValidator>();
            services.TryAddSingleton<PixivCookieValidator>();

            services.RegisterAllRegisteredTypeAs<IDownloader>();
            services.RegisterAllRegisteredTypeAs<IDownloaderOptionsValidator>();

            services.AddSingleton<InsideWorldOptionsManagerPool>();

            services.AddSingleton<ThirdPartyHttpRequestLogger>();

            services.AddInsideWorldMigrations();

            services.RegisterAllRegisteredTypeAs<ICookieValidator>();

            services.TryAddSingleton<FfMpegService>();
            services.TryAddSingleton<LuxService>();
            services.TryAddSingleton<BakabaseUpdaterService>();
            services.RegisterAllRegisteredTypeAs<IDependentComponentService>();

            services.TryAddSingleton<IBakabaseUpdater>(sp => sp.GetRequiredService<BakabaseUpdaterService>());

            services.TryAddSingleton<WebGuiHubConfigurationAdapter>();
            services.TryAddSingleton<CompressedFileService>();

            services.TryAddSingleton<GlobalCacheContainer>();

            services.TryAddTransient<InsideWorldLocalizer>();

            services.TryAddSingleton<IFileMover, FileMover>();

            services.TryAddSingleton<InsideWorldWebProxy>();
        }

        protected override void ConfigureEndpointsAtFirst(IEndpointRouteBuilder routeBuilder)
        {
            routeBuilder.MapHub<WebGuiHub>("/hub/ui");
        }

        public override void Configure(IApplicationBuilder app, IHostApplicationLifetime lifetime)
        {
            // todo: merge gui configuration
            app.ApplicationServices.GetRequiredService<WebGuiHubConfigurationAdapter>().Initialize();
            app.ConfigureGui();

            base.Configure(app, lifetime);
        }
    }
}