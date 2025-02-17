using System.Collections.Specialized;
using Buildalyzer;

namespace Altinn.Analysis;

public sealed record AppRepository(DirectoryInfo Dir, string Org, string Name);

// TODO: Consider SoA if this approach works OK
public readonly record struct AppAnalysisResult
{
    private static readonly BitVector32.Section _validProject = BitVector32.CreateSection(2);
    private static readonly BitVector32.Section _builds = BitVector32.CreateSection(
        2,
        _validProject
    );
    private static readonly BitVector32.Section _hasAppLib = BitVector32.CreateSection(2, _builds);
    private static readonly BitVector32.Section _hasLatestAppLib = BitVector32.CreateSection(
        2,
        _hasAppLib
    );

    private readonly BitVector32 _bits;

    public AppAnalysisResult(
        AppRepository repo,
        bool? validProject = null,
        bool? builds = null,
        bool? hasAppLib = null,
        bool? HasLatestAppLib = null
    )
    {
        AppRepository = repo;
        _bits = new BitVector32(0);
        _bits[_validProject] = ToValue(validProject);
        _bits[_builds] = ToValue(builds);
        _bits[_hasAppLib] = ToValue(hasAppLib);
        _bits[_hasLatestAppLib] = ToValue(HasLatestAppLib);
    }

    public AppRepository AppRepository { get; }

    public readonly bool? ValidProject => FromValue(_validProject);
    public readonly bool? Builds => FromValue(_builds);
    public readonly bool? HasAppLib => FromValue(_hasAppLib);
    public readonly bool? HasLatestAppLib => FromValue(_hasLatestAppLib);

    public bool OK =>
        (_bits[_validProject] & _bits[_builds] & _bits[_hasAppLib] & _bits[_hasLatestAppLib]) == 1;

    private static int ToValue(bool? value) =>
        value switch
        {
            null => 2,
            false => 0,
            true => 1,
        };

    private bool? FromValue(BitVector32.Section section) =>
        _bits[section] switch
        {
            2 => default(bool?),
            0 => false,
            1 => true,
            var u => throw new Exception($"Unexpected value: {u}"),
        };
}

public static class AppAnalyzer
{
    public static AppAnalysisResult Run(AppRepository app, CancellationToken cancellationToken)
    {
        var manager = new AnalyzerManager();
        var dir = app.Dir;
        var projectFile = Path.Combine(dir.FullName, "App", "App.csproj");

        if (!File.Exists(projectFile))
            return new AppAnalysisResult(app, validProject: false);

        const string tfm = "net8.0";
        IAnalyzerResults buildResults;
        try
        {
            var project = manager.GetProject(projectFile);
            cancellationToken.ThrowIfCancellationRequested();

            buildResults = project.Build(tfm);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new AppAnalysisResult(app, validProject: false);
        }

        if (!buildResults.TryGetTargetFramework(tfm, out var result))
            return new AppAnalysisResult(app, validProject: false);

        if (!result.Succeeded)
            return new AppAnalysisResult(app, validProject: true, builds: false);

        var appCoreRef = result.PackageReferences.FirstOrDefault(r =>
            r.Key == "Altinn.App.Core" || r.Key == "Altinn.App.Core.Experimental"
        );
        if (appCoreRef.Key is null)
            return new AppAnalysisResult(app, validProject: true, builds: true, hasAppLib: false);

        if (!appCoreRef.Value.TryGetValue("Version", out var version))
            return new AppAnalysisResult(app, validProject: true, builds: true, hasAppLib: false);

        if (!version.StartsWith('8'))
            return new AppAnalysisResult(
                app,
                validProject: true,
                builds: true,
                hasAppLib: true,
                HasLatestAppLib: false
            );

        return new AppAnalysisResult(
            app,
            validProject: true,
            builds: true,
            hasAppLib: true,
            HasLatestAppLib: true
        );
    }
}
