using System.Reflection;
using System.Runtime.InteropServices;

namespace Altinn.Apps.Monitoring.Tests;

internal sealed class TestProject : ITestProject
{
    private static IDisposable[]? _disposables;
    private static readonly CancellationTokenSource _cancellationTokenSource = new();

    public static CancellationToken CancellationToken => _cancellationTokenSource.Token;

    private static void HandlePosixSignal(PosixSignalContext context)
    {
        lock (_cancellationTokenSource)
        {
            if (_cancellationTokenSource.IsCancellationRequested)
                return;

            _cancellationTokenSource.Cancel();
        }
    }

    public void Configure(TestConfiguration configuration, TestEnvironment environment)
    {
        if (_disposables is not null)
            throw new InvalidOperationException("Configure should only be called once");

        _disposables =
        [
            PosixSignalRegistration.Create(PosixSignal.SIGINT, HandlePosixSignal),
            PosixSignalRegistration.Create(PosixSignal.SIGQUIT, HandlePosixSignal),
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, HandlePosixSignal),
        ];

        VerifierSettings.AssignTargetAssembly(environment.Assembly);
        configuration.Conventions.Add<TestDiscovery, TestExecution>();
    }
}

internal sealed class TestDiscovery : IDiscovery
{
    public IEnumerable<Type> TestClasses(IEnumerable<Type> concreteClasses) =>
        concreteClasses.Where(x => x.Name.EndsWith("Tests", StringComparison.Ordinal));

    public IEnumerable<MethodInfo> TestMethods(IEnumerable<MethodInfo> publicMethods) =>
        publicMethods.Where(x => x.IsStatic);
}

internal sealed class TestExecution : IExecution
{
    public async Task Run(TestSuite testSuite)
    {
        var testsWithParameters = testSuite
            .TestClasses.SelectMany(testClass =>
            {
                return testClass.Tests.SelectMany(test =>
                {
                    if (!test.HasParameters)
                        return [(testClass, test, [])];

                    var allParameters = test.GetAll<InputParameter>().Select(x => x.Parameters).ToArray();
                    return allParameters.Select(parameters => (testClass, test, parameters)).ToArray();
                });
            })
            .ToArray();

        await Parallel.ForEachAsync(
            testsWithParameters,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 2,
                CancellationToken = TestProject.CancellationToken,
            },
            async (testInfo, _) =>
            {
                var (testClass, test, parameters) = testInfo;

                if (parameters.Length > 0)
                {
                    using var __ = ExecutionState.Set(testClass, test, parameters);
                    await test.Run(parameters);
                }
                else
                {
                    using var __ = ExecutionState.Set(testClass, test, null);
                    await test.Run();
                }
            }
        );
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class InputParameter : Attribute
{
    public InputParameter(params object[] parameters)
    {
        Parameters = parameters;
    }

    public object[] Parameters { get; }
}
