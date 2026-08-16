using Valency.Shell;

namespace Valency.Shell.Tests.Scripting;

public class InterpreterTests
{
    private sealed class FakeRuntime : IShellRuntime
    {
        public List<IReadOnlyList<string>> SimpleCalls { get; } = new();
        public List<IReadOnlyList<string>> PipelineCalls { get; } = new();
        public int NextCode { get; set; }
        public Func<IReadOnlyList<string>, int>? Handler { get; set; }

        public int ExecuteSimpleCommand(IReadOnlyList<string> argv, IReadOnlyList<ResolvedRedirection> redirects)
        {
            SimpleCalls.Add(argv);
            return Handler?.Invoke(argv) ?? NextCode;
        }

        public int ExecutePipeline(IReadOnlyList<PipelineStage> stages)
        {
            PipelineCalls.Add(stages.Select(s => string.Join(" ", s.Argv)).ToList());
            return NextCode;
        }

        public int ExecuteBackground(IReadOnlyList<string> argv) => 0;
    }

    private static (FakeRuntime Runtime, Interpreter Interpreter) Create(int code = 0)
    {
        var runtime = new FakeRuntime { NextCode = code };
        var state = new ShellState();
        return (runtime, new Interpreter(runtime, state));
    }

    [Fact]
    public void SimpleCommand_IsDispatchedToRuntime()
    {
        var (rt, interp) = Create();
        interp.Execute("echo hi");
        Assert.Equal(new[] { "echo", "hi" }, Assert.Single(rt.SimpleCalls));
    }

    [Fact]
    public void Semicolon_RunsAllCommands()
    {
        var (rt, interp) = Create();
        interp.Execute("echo a; echo b");
        Assert.Equal(2, rt.SimpleCalls.Count);
    }

    [Fact]
    public void And_ShortCircuitsOnFailure()
    {
        var (success, interp) = Create(0);
        success.NextCode = 0;
        interp.Execute("a && b");
        Assert.Equal(2, success.SimpleCalls.Count);

        var (fail, interp2) = Create(1);
        interp2.Execute("a && b");
        Assert.Equal(["a"], Assert.Single(fail.SimpleCalls));
    }

    [Fact]
    public void Or_RunsSecondOnFailure()
    {
        var (fail, interp) = Create(1);
        interp.Execute("a || b");
        Assert.Equal(2, fail.SimpleCalls.Count);

        var (success, interp2) = Create(0);
        interp2.Execute("a || b");
        Assert.Equal(["a"], Assert.Single(success.SimpleCalls));
    }

    [Fact]
    public void VariableAssignment_And_Expansion()
    {
        var (rt, interp) = Create();
        interp.Execute("X=world");
        interp.Execute("echo $X");
        Assert.Equal(new[] { "echo", "world" }, rt.SimpleCalls[^1]);
    }

    [Fact]
    public void PositionalArgs_AreExpanded()
    {
        var (rt, interp) = Create();
        interp.State.PositionalArgs = ["p", "q"];
        interp.Execute("echo $1 $2 $#");
        Assert.Equal(new[] { "echo", "p", "q", "2" }, rt.SimpleCalls[^1]);
    }

    [Fact]
    public void If_RunsMatchingBranch()
    {
        var (rt, interp) = Create(0);
        interp.Execute("if true; then echo y; else echo n; fi");
        Assert.Equal(["true"], rt.SimpleCalls[0]);
        Assert.Equal(new[] { "echo", "y" }, rt.SimpleCalls[^1]);
    }

    [Fact]
    public void For_IteratesItems()
    {
        var (rt, interp) = Create();
        interp.Execute("for x in a b; do echo $x; done");
        Assert.Equal(2, rt.SimpleCalls.Count);
        Assert.Equal(new[] { "echo", "a" }, rt.SimpleCalls[0]);
        Assert.Equal(new[] { "echo", "b" }, rt.SimpleCalls[1]);
    }

    [Fact]
    public void Function_DefinesAndInvokes()
    {
        var (rt, interp) = Create();
        interp.Execute("f() { echo hi; }; f");
        Assert.Equal(new[] { "echo", "hi" }, Assert.Single(rt.SimpleCalls));
    }

    [Fact]
    public void Function_ReturnPropagatesCode()
    {
        var (rt, interp) = Create();
        rt.Handler = argv => argv[0] == "return"
            ? throw new ControlFlowException(ControlFlowKind.Return, 5)
            : 0;
        var code = interp.Execute("f() { return 5; }; f");
        Assert.Equal(5, code);
    }

    [Fact]
    public void Pipeline_IsDispatchedToRuntime()
    {
        var (rt, interp) = Create();
        interp.Execute("a | b");
        Assert.Single(rt.PipelineCalls);
        Assert.Equal(2, rt.PipelineCalls[0].Count);
    }

    [Fact]
    public void ArithmeticCommand_ReturnsZeroWhenNonzero()
    {
        var (_, interp) = Create();
        Assert.Equal(0, interp.Execute("(( 1 + 1 ))"));
        Assert.Equal(1, interp.Execute("(( 0 ))"));
    }

    [Fact]
    public void CommandSubstitution_CapturesOutput()
    {
        var (rt, interp) = Create();
        rt.Handler = argv =>
        {
            if (argv.Count > 1)
                Console.Out.Write(argv[1]);
            return 0;
        };
        interp.Execute("echo $(echo inner)");
        Assert.Equal(new[] { "echo", "inner" }, rt.SimpleCalls[0]);
        Assert.Equal(new[] { "echo", "inner" }, rt.SimpleCalls[^1]);
    }
}
