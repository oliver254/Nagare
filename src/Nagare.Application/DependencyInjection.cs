using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nagare.Application.Abstractions;
using Nagare.Application.Channels;
using Nagare.Application.Media;
using Nagare.Application.Profiles;
using Nagare.Application.Streaming;
using Nagare.Domain.Common;

namespace Nagare.Application;

/// <summary>
/// Explicit DI registration of the Application layer (ADR-0003): one line per handler,
/// no assembly scanning.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNagareApplication(this IServiceCollection services)
    {
        services.AddOptions<ReconnectSettings>()
            .BindConfiguration(ReconnectSettings.SectionName);

        // The coordinator is a single shared instance exposing three roles.
        services.AddSingleton<StreamSessionCoordinator>();
        services.AddSingleton<IStreamSessionCoordinator>(sp => sp.GetRequiredService<StreamSessionCoordinator>());
        services.AddSingleton<ISessionMonitor>(sp => sp.GetRequiredService<StreamSessionCoordinator>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<StreamSessionCoordinator>());

        // Profiles
        services.AddScoped<ICommandHandler<SaveStreamProfileCommand, ProfileId>, SaveStreamProfileHandler>();
        services.AddScoped<ICommandHandler<DeleteStreamProfileCommand>, DeleteStreamProfileHandler>();
        services.AddScoped<IQueryHandler<GetStreamProfilesQuery, IReadOnlyList<StreamProfileDto>>, GetStreamProfilesHandler>();

        // Channels
        services.AddScoped<ICommandHandler<SaveChannelCommand, ChannelId>, SaveChannelHandler>();
        services.AddScoped<ICommandHandler<DeleteChannelCommand>, DeleteChannelHandler>();
        services.AddScoped<IQueryHandler<GetChannelsQuery, IReadOnlyList<ChannelDto>>, GetChannelsHandler>();

        // Streaming
        services.AddScoped<ICommandHandler<StartStreamCommand, SessionId>, StartStreamHandler>();
        services.AddScoped<ICommandHandler<StopStreamCommand>, StopStreamHandler>();
        services.AddScoped<IQueryHandler<GetSessionStatusQuery, SessionSnapshot?>, GetSessionStatusHandler>();
        services.AddScoped<IQueryHandler<GetSessionLogsQuery, IReadOnlyList<string>>, GetSessionLogsHandler>();
        services.AddScoped<IQueryHandler<BuildCommandPreviewQuery, string>, BuildCommandPreviewHandler>();

        // Media
        services.AddScoped<IQueryHandler<ValidateMediaFileQuery, MediaValidationResult>, ValidateMediaFileHandler>();
        services.AddScoped<IQueryHandler<GetFfmpegEnvironmentQuery, FfmpegEnvironmentReport>, GetFfmpegEnvironmentHandler>();

        return services;
    }
}
