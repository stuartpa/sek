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

    /// <summary>
    /// The system-under-test model. It is intentionally stateless: the point of this
    /// sample is to show parameter generation, so each distinct set of AddJob arguments
    /// is a distinct transition out of the (single, accepting) state.
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

        [AcceptingCondition]
        public bool Accepting() => true;
    }
}
