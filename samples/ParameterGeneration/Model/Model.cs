using System;
using Sek.Modeling;

// ParameterGeneration (SEK port) — the classic sample defines one action name (SUT.AddJob)
// across several model *scopes*, each with a different signature. SEK loads the model program
// whose namespace matches the Cord `scope`, so each scope is a small model class below.

namespace PG
{
    /// <summary>Job frequency — an enum parameter (natural domain: Once/Daily/Weekly).</summary>
    public enum Frequency
    {
        Once,
        Daily,
        Weekly,
    }

    /// <summary>Days-of-week as a [Flags] enum (for the Flags / EquivalenceClass scopes).</summary>
    [Flags]
    public enum DaysOfWeek
    {
        None = 0,
        Mon = 1,
        Tue = 2,
        Wed = 4,
        Thu = 8,
        Fri = 16,
        Sat = 32,
        Sun = 64,
        All = 127,
    }

    /// <summary>A structured job description (for the Struct scope).</summary>
    public sealed class JobInfo
    {
        public string Name { get; set; }
        public int Time { get; set; }
        public Frequency Frequency { get; set; }
    }
}

namespace PG.ModelWithFrequency
{
    /// <summary>SUT.AddJob(name, time, frequency) — used by the combination scopes.</summary>
    public sealed class SUT : ModelProgram
    {
        [Rule("SUT.AddJob")]
        public void AddJob(string name, int time, PG.Frequency frequency) { }

        [AcceptingCondition]
        public bool Accepting() => true;
    }
}

namespace PG.ModelWithExpand
{
    public sealed class SUT : ModelProgram
    {
        // The Expand demo expands every parameter fully; the model supplies the name/time
        // domains (the frequency enum ranges naturally).
        private string[] Names() => new[] { "@$^", "t.cmd", "t.exe" };
        private int[] Times() => new[] { -1, 60, 3600 };

        [Rule("SUT.AddJob")]
        public void AddJob([Domain("Names")] string name, [Domain("Times")] int time, PG.Frequency frequency) { }

        [AcceptingCondition]
        public bool Accepting() => true;
    }
}

namespace PG.ModelWithStruct
{
    public sealed class SUT : ModelProgram
    {
        [Rule("SUT.AddJob")]
        public void AddJob(PG.JobInfo info, int priority) { }

        [AcceptingCondition]
        public bool Accepting() => true;
    }
}

namespace PG.ModelWithBitmask
{
    public sealed class SUT : ModelProgram
    {
        [Rule("SUT.AddJob")]
        public void AddJob(string name, int time, uint days) { }

        [AcceptingCondition]
        public bool Accepting() => true;
    }
}

namespace PG.ModelWithFlags
{
    public sealed class SUT : ModelProgram
    {
        [Rule("SUT.AddJob")]
        public void AddJob(string name, int time, PG.DaysOfWeek days) { }

        [AcceptingCondition]
        public bool Accepting() => true;
    }
}

namespace PG.ModelWithEquivalenceClass
{
    public sealed class SUT : ModelProgram
    {
        [Rule("SUT.AddJob")]
        public void AddJob(string name, int time, PG.DaysOfWeek days) { }

        [AcceptingCondition]
        public bool Accepting() => true;
    }
}

namespace PG.Probability
{
    public sealed class SUT : ModelProgram
    {
        [Rule("SUT.CreateFile")]
        public void CreateFile(string name, bool errorIfExists, bool appendAtEnd) { }

        [AcceptingCondition]
        public bool Accepting() => true;
    }
}

namespace PG.Let
{
    /// <summary>SUT.A/B/C(int x) — used by the `let` behavior machine (explored directly).</summary>
    public sealed class SUT : ModelProgram
    {
        [Rule("SUT.A")]
        public void A(int x) { }

        [Rule("SUT.B")]
        public void B(int x) { }

        [Rule("SUT.C")]
        public void C(int x) { }

        [AcceptingCondition]
        public bool Accepting() => true;
    }
}
