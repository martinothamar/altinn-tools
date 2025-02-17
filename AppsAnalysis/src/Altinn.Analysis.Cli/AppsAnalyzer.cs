using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics;
using Buildalyzer;
using Spectre.Console;

namespace Altinn.Analysis.Cli;

internal sealed class AppsAnalyzer
{
    private readonly AnalysisConfig _config;

    private DirectoryInfo? _directory;
    private int _parallelism;

    public AppsAnalyzer(AnalysisConfig config)
    {
        _config = config;
    }

    private bool VerifyConfig()
    {
        var table = new Table();
        table.Border(TableBorder.None);
        table.AddColumn(new TableColumn(""));
        table.AddColumn(new TableColumn(""));

        _parallelism = Math.Min(Constants.LimitMaxParallelism, _config.MaxParallelism);
        var directory = Path.GetFullPath(
            _config.Directory.Replace(
                "~",
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            )
        );
        table.AddRow(
            new Markup(nameof(FetchConfig.Directory)),
            new Markup($"= [bold]{directory}[/]")
        );
        table.AddRow(new Markup("Parallelism"), new Markup($"= [bold]{_parallelism}[/]"));
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (!Debugger.IsAttached)
        {
            var proceed = AnsiConsole.Prompt(new ConfirmationPrompt($"Continue?"));
            return proceed;
        }

        return true;
    }

    public async Task Analyze(CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[blue]Analyzing Altinn apps[/]... Config:");
        if (!VerifyConfig())
            return;

        var directory = Path.GetFullPath(
            _config.Directory.Replace(
                "~",
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            )
        );
        _directory = new DirectoryInfo(directory);
        if (!_directory.Exists)
        {
            AnsiConsole.MarkupLine(
                $"Directory for downloaded apps [red]doesn't exist[/]: [bold]{directory}[/]"
            );
            return;
        }

        var repos = GetRepos(cancellationToken);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"Analysing [blue]{repos.Count}[/] apps").LeftJustified());

        AppAnalysisResult[]? results = null;
        await AnsiConsole
            .Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new ElapsedTimeColumn(),
                new SpinnerColumn()
            )
            .HideCompleted(true)
            .AutoClear(true)
            .StartAsync(async ctx =>
            {
                results = await Task.Run(
                    () => AnalyzeRepos(ctx, repos, cancellationToken),
                    cancellationToken
                );
            });

        Debug.Assert(results is not null);
        AnsiConsole.MarkupLine($"[green]Successfully[/] analyzed [blue]{results.Length}[/] apps");

        // Ample opportunity for SIMD belowow...
        var invalidProjects = results.Count(r => r.ValidProject is false);
        var failedBuilds = results.Count(r => r.Builds is false);
        var noAppLib = results.Count(r => r.HasAppLib is false);
        var oldAppLib = results.Count(r => r.HasLatestAppLib is false);
        var aOkay = results.Count(r => r.OK);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new BarChart()
                .Width(60)
                .Label("[green bold underline]Analysis[/]")
                .LeftAlignLabel()
                .AddItem("Invalid project", invalidProjects, Color.Red)
                .AddItem("Failed builds", failedBuilds, Color.Red)
                .AddItem("Missing Altinn.App.Core", noAppLib, Color.Red)
                .AddItem("Old Altinn.App.Core", oldAppLib, Color.Yellow)
                .AddItem("On Altinn.App.Core v8", aOkay, Color.Green)
        );
    }

    private List<AppRepository> GetRepos(CancellationToken cancellationToken)
    {
        Debug.Assert(_directory is not null);

        var result = new List<AppRepository>(64);
        var orgsCounter = 0;
        foreach (var orgDir in Directory.EnumerateDirectories(_directory.FullName))
        {
            var org = orgDir.Substring(orgDir.LastIndexOf(Path.DirectorySeparatorChar) + 1);
            cancellationToken.ThrowIfCancellationRequested();

            var reposCounter = 0;
            foreach (var repoDir in Directory.EnumerateDirectories(orgDir))
            {
                var name = repoDir.Substring(repoDir.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                var repoDirInfo = new DirectoryInfo(
                    Path.Combine(repoDir, Constants.MainBranchFolder)
                );
                if (!repoDirInfo.Exists)
                {
                    AnsiConsole.MarkupLine(
                        $"Couldn't find main branch folder for repo: '{repoDirInfo.Name}'"
                    );
                    continue;
                }
                result.Add(new AppRepository(repoDirInfo, org, name));
                reposCounter++;

                if (reposCounter == Constants.LimitRepos)
                    break;
            }

            orgsCounter++;

            if (orgsCounter == Constants.LimitOrgs)
                break;
        }

        return result;
    }

    private AppAnalysisResult[] AnalyzeRepos(
        ProgressContext ctx,
        List<AppRepository> repos,
        CancellationToken cancellationToken
    )
    {
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = _parallelism,
        };

        var results = new ConcurrentBag<AppAnalysisResult>();

        Parallel.ForEach(
            repos,
            options,
            repo =>
            {
                var (dir, org, name) = repo;
                var task = ctx.AddTask($"[green]{org}/{name}[/]", autoStart: false, maxValue: 1);
                task.IsIndeterminate = true;
                task.StartTask();

                var result = AppAnalyzer.Run(repo, cancellationToken);
                results.Add(result);

                task.Increment(1.0);
                task.StopTask();
            }
        );

        return results.ToArray();
    }
}
