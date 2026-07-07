using Sek.Modeling;

namespace SelfHost.Model
{
    /// <summary>
    /// A SEK model of SEK's own <c>sek</c> CLI — SEK validating SEK. The state under exploration is
    /// whether an <c>explore</c> has produced a <c>.seexpl</c> graph on disk.
    /// <para>
    /// The full command surface is modelled as rules that drive the <b>real</b> <c>sek</c> CLI
    /// (see <c>SelfHost.Sut.SekSession</c>): the happy path (<c>init</c>, <c>validate</c>,
    /// <c>explore</c>, <c>view</c>, <c>generate</c>, <c>test</c>) plus the tool's <b>error</b>
    /// behaviour as first-class transitions (<c>view</c> of a missing file, <c>explore</c> of an
    /// unknown machine — each asserts the CLI reports the error with a non-zero exit).
    /// </para>
    /// <para>
    /// Only one <em>real</em> ordering constraint exists against a pre-initialised project:
    /// <c>view</c> can only render a graph a prior <c>explore</c> wrote — that is the single guard.
    /// Every other command is independently runnable, so modelling extra ordering would be fiction
    /// (PM003: behaviour-level, non-theatre). The accepting scenario is "the core exploration loop
    /// has run", i.e. a graph exists.
    /// </para>
    /// </summary>
    public sealed class SekWorkflowModel : ModelProgram
    {
        /// <summary>True once an <c>explore</c> has produced a <c>.seexpl</c> graph on disk.</summary>
        public bool Explored { get; set; }

        [Rule("SekSession.Init")]
        public void Init()
        {
            // `sek init` is idempotent against an existing project — always available, no state change.
        }

        [Rule("SekSession.Validate")]
        public void Validate()
        {
            // `sek validate` checks the model/Cord line up — independent of exploration state.
        }

        [Rule("SekSession.Explore")]
        public void Explore()
        {
            Explored = true;
        }

        [Rule("SekSession.View")]
        public void View()
        {
            Require(Explored, "view requires a graph produced by a prior explore");
        }

        [Rule("SekSession.Generate")]
        public void Generate()
        {
            // `sek generate` explores internally and emits an xUnit project — independently runnable.
        }

        [Rule("SekSession.Test")]
        public void Test()
        {
            // `sek test` explores internally and replays conformance against the SUT — independently runnable.
        }

        [Rule("SekSession.ViewMissing")]
        public void ViewMissing()
        {
            // Error transition: `sek view <missing>` must fail cleanly (non-zero exit). Always reachable.
        }

        [Rule("SekSession.ExploreUnknown")]
        public void ExploreUnknown()
        {
            // Error transition: `sek explore <unknown-machine>` must fail cleanly (non-zero exit).
        }

        [AcceptingCondition]
        public bool Done() => Explored;
    }
}
