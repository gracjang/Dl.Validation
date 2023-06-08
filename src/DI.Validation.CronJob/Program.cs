using DI.Validation.CronJob;
using DI.Validation.CronJob.Components.Blob;
using DI.Validation.CronJob.Configurations;
using Microsoft.Extensions.Options;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(
        (ctx, services) =>
        {
            services.Configure<ServiceBusConfiguration>(ctx.Configuration);
            services.Configure<ContainerConfiguration>(ctx.Configuration);
            services.AddSingleton(p => p.GetRequiredService<IOptions<ServiceBusConfiguration>>().Value);
            services.AddSingleton(p => p.GetRequiredService<IOptions<ContainerConfiguration>>().Value);

            services.AddSingleton<IDeadLetterProcessor, DeadLetterProcessor>();
            services.AddSingleton<IBlobClient, BlobClient>();
            services.AddSingleton<IBlobClientFactory, BlobClientFactory>();
        })
    .Build();

await host.Services.GetRequiredService<IDeadLetterProcessor>().Execute(CancellationToken.None);