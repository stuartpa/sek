using Sek.Modeling;

namespace Turnstile.Model
{
    /// <summary>
    /// The SEK model of the turnstile SUT. State is a single boolean; the two rules mirror the
    /// SUT's methods (rule label <c>Turnstile.&lt;Method&gt;</c> binds to the SUT class
    /// <c>Turnstile</c>). Guards prune illegal steps so the exploration is exactly the reachable
    /// behaviour: locked --Coin--&gt; unlocked --Push--&gt; locked (and Coin while unlocked).
    /// </summary>
    public sealed class TurnstileModel : ModelProgram
    {
        public bool Locked { get; set; } = true;

        [Rule("Turnstile.Coin")]
        public void Coin()
        {
            Locked = false;
        }

        [Rule("Turnstile.Push")]
        public void Push()
        {
            Require(!Locked, "cannot push a locked turnstile");
            Locked = true;
        }

        /// <summary>A test may end with the turnstile locked (its resting state).</summary>
        [AcceptingCondition]
        public bool AtRest() => Locked;
    }
}
