using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Bakabase.Modules.RemoteAccess.Abstractions.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Swashbuckle.AspNetCore.Annotations;

namespace Bakabase.Service.Components.RemoteAccess;

/// <summary>
/// The set of actions marked <see cref="RunsOnUserMachineAttribute"/>, resolved once
/// by reflection.
/// </summary>
/// <remarks>
/// <para>
/// The gate needs a fast lookup, and the snapshot test needs something it can compare
/// against a golden list — an endpoint quietly entering or leaving this set is exactly
/// the mistake that would send a "play this" to a screen nobody is watching.
/// </para>
/// <para>
/// Keyed by HTTP method plus route template rather than by method handle, because that
/// is what the client's forwarding layer sees: it has a request, not a
/// <see cref="MethodInfo"/>.
/// </para>
/// </remarks>
public static class UserMachineEndpointCatalog
{
    public sealed record Entry(string HttpMethod, string RouteTemplate, string? OperationId, string? Reason)
    {
        /// <summary>
        /// Stable key for snapshots and for the forwarder's lookup. Compare it with
        /// <see cref="StringComparer.OrdinalIgnoreCase"/> — routes match without regard
        /// to case, and a controller written as <c>[Route("~/[controller]")]</c> yields
        /// the class name's casing rather than the lowercase form everyone writes.
        /// </summary>
        public string Key => $"{HttpMethod} {RouteTemplate}";
    }

    private static readonly Lazy<ImmutableArray<Entry>> LazyEntries =
        new(() => Build(typeof(UserMachineEndpointCatalog).Assembly));

    private static readonly Lazy<ImmutableHashSet<string>> LazyKeys =
        new(() => LazyEntries.Value.Select(e => e.Key).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase));

    public static ImmutableArray<Entry> Entries => LazyEntries.Value;

    public static ImmutableHashSet<string> Keys => LazyKeys.Value;

    internal static ImmutableArray<Entry> Build(Assembly assembly)
    {
        var entries = new List<Entry>();

        foreach (var controller in assembly.GetTypes().Where(IsController))
        {
            var controllerMarked = controller.GetCustomAttribute<RunsOnUserMachineAttribute>();
            var prefix = GetControllerRoutePrefix(controller);

            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var marker = action.GetCustomAttribute<RunsOnUserMachineAttribute>() ?? controllerMarked;
                if (marker == null)
                {
                    continue;
                }

                var operationId = action.GetCustomAttribute<SwaggerOperationAttribute>()?.OperationId;

                foreach (var http in action.GetCustomAttributes<HttpMethodAttribute>())
                {
                    foreach (var method in http.HttpMethods)
                    {
                        entries.Add(new Entry(method, Combine(prefix, http.Template), operationId, marker.Reason));
                    }
                }
            }
        }

        return entries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase).ToImmutableArray();
    }

    private static bool IsController(Type t) =>
        t is {IsAbstract: false, IsPublic: true} && typeof(ControllerBase).IsAssignableFrom(t);

    private static string GetControllerRoutePrefix(Type controller)
    {
        var template = controller.GetCustomAttributes<RouteAttribute>().FirstOrDefault()?.Template ?? string.Empty;

        // "~/" means "ignore any inherited prefix"; the catalog has no inherited
        // prefix to begin with, so it is only noise here.
        template = template.TrimStart('~').Trim('/');

        // Controllers written as [Route("~/[controller]")] rely on MVC's token
        // replacement, which does not run outside the routing system.
        var name = controller.Name.EndsWith("Controller", StringComparison.Ordinal)
            ? controller.Name[..^"Controller".Length]
            : controller.Name;

        return template.Replace("[controller]", name, StringComparison.OrdinalIgnoreCase);
    }

    private static string Combine(string prefix, string? actionTemplate)
    {
        var action = actionTemplate?.Trim('/') ?? string.Empty;
        if (prefix.Length == 0)
        {
            return "/" + action;
        }

        return action.Length == 0 ? "/" + prefix : $"/{prefix}/{action}";
    }
}
