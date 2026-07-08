using Sek.Modeling;

namespace SelfHost.Model
{
    /// <summary>
    /// A SEK model of SEK's own <c>sek</c> CLI project lifecycle — SEK validating SEK. The state is
    /// two interacting facts about a project: whether it has been <c>init</c>-ialised (a config
    /// exists) and whether an <c>explore</c> has produced a <c>.seexpl</c> graph.
    /// <para>
    /// The guards encode the tool's <b>real ordering constraints</b> against a *fresh* project:
    /// nothing but <c>init</c> is legal until the project is initialised (the CLI has no config to
    /// load), and <c>view</c> is legal only after an <c>explore</c> produced a graph to render. From
    /// those guards the exploration branches (init → {validate/explore/generate/test} → view) and,
    /// crucially, SEK <b>derives the illegal (state, action) pairs</b> — e.g. <c>explore</c> before
    /// <c>init</c>, <c>view</c> before <c>explore</c> — which <c>sek generate</c>/<c>sek test</c> turn
    /// into <b>negative conformance</b> tests that assert the real CLI rejects them. No error case is
    /// hand-coded; the model's guards do the work (EngLoopKit PM004).
    /// </para>
    /// </summary>
    public sealed class SekWorkflowModel : ModelProgram
    {
        /// <summary>True once <c>sek init</c> has written a project config.</summary>
        public bool Initialized { get; set; }

        /// <summary>True once an <c>explore</c> has produced a <c>.seexpl</c> graph.</summary>
        public bool Explored { get; set; }

        [Rule("SekSession.Init")]
        public void Init()
        {
            // `sek init` is always legal (idempotent) and establishes the project.
            Initialized = true;
        }

        [Rule("SekSession.Validate")]
        public void Validate()
        {
            Require(Initialized, "validate requires an initialized project");
        }

        [Rule("SekSession.Explore")]
        public void Explore()
        {
            Require(Initialized, "explore requires an initialized project");
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
            Require(Initialized, "generate requires an initialized project");
        }

        [Rule("SekSession.Test")]
        public void Test()
        {
            Require(Initialized, "test requires an initialized project");
        }

        [AcceptingCondition]
        public bool Done() => Explored;
    }
}
