using Sek.Modeling;

namespace SelfHost.Model
{
    /// <summary>
    /// A SEK model of SEK's own CLI workflow. State: whether an exploration has produced a graph.
    /// <c>Validate</c> and <c>Explore</c> are always available; <c>View</c> is guarded on a prior
    /// <c>Explore</c> (a graph must exist to render) — the real ordering constraint of the tool.
    /// </summary>
    public sealed class SekWorkflowModel : ModelProgram
    {
        /// <summary>True once an exploration has produced a .seexpl graph.</summary>
        public bool Explored { get; set; }

        [Rule("SekSession.Validate")]
        public void Validate()
        {
            // Independent of exploration state.
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

        [AcceptingCondition]
        public bool Done() => true;
    }
}
