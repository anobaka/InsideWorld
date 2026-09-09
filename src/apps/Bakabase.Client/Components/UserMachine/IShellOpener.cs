using System.Diagnostics;
using Bakabase.Infrastructures.Components.App;

namespace Bakabase.Client.Components.UserMachine;

/// <summary>
/// The one place the client asks the operating system to open something.
/// </summary>
/// <remarks>
/// A seam rather than a direct call, because everything interesting about these handlers
/// happens before it: translating a path, refusing one that maps nowhere, falling back to
/// a parent folder, rejecting a scheme that is a program launch in disguise. Those are
/// the decisions worth testing, and none of them can be tested if reaching them means
/// spawning a file manager.
/// </remarks>
public interface IShellOpener
{
    /// <summary>
    /// Shows a file or folder in the file manager.
    /// </summary>
    /// <param name="inParentDirectory">
    /// True to open the containing folder with the item selected, false to open the item
    /// itself.
    /// </param>
    void Reveal(string path, bool inParentDirectory);

    /// <summary>Hands a file or a URL to whatever the OS associates with it.</summary>
    void Launch(string target);
}

public sealed class OsShellOpener : IShellOpener
{
    public void Reveal(string path, bool inParentDirectory) => OsShell.Open(path, inParentDirectory);

    public void Launch(string target) => Process.Start(new ProcessStartInfo(target) {UseShellExecute = true});
}
