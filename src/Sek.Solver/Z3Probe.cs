using Microsoft.Z3;

namespace Sek.Solver;

/// <summary>Quick diagnostic that the native Z3 library loads and can solve.</summary>
public static class Z3Probe
{
    public static string SelfTest()
    {
        var asmVersion = typeof(Context).Assembly.GetName().Version?.ToString() ?? "?";

        using var ctx = new Context();
        var x = ctx.MkIntConst("x");
        var solver = ctx.MkSolver();
        // 3 < x < 7  and  x is even  -> expect x = 4 or 6
        solver.Add(ctx.MkGt(x, ctx.MkInt(3)));
        solver.Add(ctx.MkLt(x, ctx.MkInt(7)));
        solver.Add(ctx.MkEq(ctx.MkMod(x, ctx.MkInt(2)), ctx.MkInt(0)));

        var status = solver.Check();
        var value = status == Status.SATISFIABLE ? solver.Model.Evaluate(x).ToString() : "n/a";
        return $"Microsoft.Z3 {asmVersion}: check={status}, x={value}";
    }
}
