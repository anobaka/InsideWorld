using Bakabase.Client.Abstractions;
using Bakabase.Infrastructures.Components.App;

namespace Bakabase.Client.Components;

/// <summary>
/// Puts the client's own state under its application data directory.
/// </summary>
/// <remarks>
/// The split between <see cref="Path"/> and <see cref="Ensure"/> exists because
/// <see cref="AppService.RequestAppDataDirectory"/> creates what it resolves. Reads have
/// to resolve without that, or a client that has never paired would grow an empty folder
/// simply by starting up.
/// </remarks>
public sealed class AppServiceClientDataDirectory(AppService appService) : IClientDataDirectory
{
    public const string DirectoryName = "client";

    public string Path => System.IO.Path.Combine(appService.AppDataDirectory, DirectoryName);

    public string Ensure() => appService.RequestAppDataDirectory(DirectoryName);
}
