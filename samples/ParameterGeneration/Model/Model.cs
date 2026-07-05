using System;
using Sek.Modeling;

namespace PG
{
    /// <summary>
    /// Job frequency — an enum parameter. With no explicit Condition.In, the SEK engine
    /// gives an enum parameter its natural domain (all enum members), so Z3 ranges over
    /// Once/Daily/Weekly.
    /// </summary>
    public enum Frequency
    {
        Once,
        Daily,
        Weekly,
    }

    /// <summary>Days-of-week as a [Flags] enum (for the Flags / EquivalenceClass strategies).</summary>
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

    /// <summary>A structured job description (for the Struct strategy).</summary>
    public sealed class JobInfo
    {
        public string Name { get; set; }
        public int Time { get; set; }
        public Frequency Frequency { get; set; }
    }

    /// <summary>
    /// The system-under-test model. It is intentionally stateless: the point of this
    /// sample is to show parameter generation, so each distinct set of arguments is a
    /// distinct transition out of the (single, accepting) state.
    /// </summary>
    public sealed class SUT : ModelProgram
    {
        // A trivial counter kept only so the state is serializable; it is reset to 0 by
        // AddJob (making AddJob a self-loop) so the graph collapses to "N combinations".
        public int Jobs { get; set; }

        [Rule("SUT.AddJob")]
        public void AddJob(string name, int time, Frequency frequency)
        {
            // Stateless w.r.t. the explored graph: no state change => self-loop.
        }

        /// <summary>Struct strategy: a JobInfo + priority.</summary>
        [Rule("SUT.AddJobStruct")]
        public void AddJobStruct(JobInfo info, int priority)
        {
        }

        /// <summary>Bitmask strategy: an integer day bitmask.</summary>
        [Rule("SUT.AddJobBitmask")]
        public void AddJobBitmask(string name, int time, uint days)
        {
        }

        /// <summary>Flags strategy: a [Flags] enum.</summary>
        [Rule("SUT.AddJobFlags")]
        public void AddJobFlags(string name, int time, DaysOfWeek days)
        {
        }

        /// <summary>Equivalence-class strategy.</summary>
        [Rule("SUT.AddJobEC")]
        public void AddJobEC(string name, int time, DaysOfWeek days)
        {
        }

        /// <summary>Probability strategy.</summary>
        [Rule("SUT.CreateFile")]
        public void CreateFile(string name, bool errorIfExists, bool appendAtEnd)
        {
        }

        [Rule("SUT.A")]
        public void A(int x)
        {
        }

        [Rule("SUT.B")]
        public void B(int x)
        {
        }

        [Rule("SUT.C")]
        public void C(int x)
        {
        }

        [AcceptingCondition]
        public bool Accepting() => true;
    }
}
