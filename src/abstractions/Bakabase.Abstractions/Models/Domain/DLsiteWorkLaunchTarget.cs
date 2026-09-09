namespace Bakabase.Abstractions.Models.Domain;

/// <summary>
/// What running a downloaded DLsite work would start.
/// </summary>
/// <param name="File">The file to run, as the machine holding the download names it.</param>
/// <param name="IsExecutable">
/// True when running it starts a program rather than opening a document. The two are
/// launched differently, and only one of them can be locale-emulated.
/// </param>
/// <param name="UseLocaleEmulator">
/// Whether the user asked for this work to run under Locale Emulator. Whether it can is a
/// separate question, answered by whichever machine actually runs it.
/// </param>
public sealed record DLsiteWorkLaunchTarget(string File, bool IsExecutable, bool UseLocaleEmulator);
