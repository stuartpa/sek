namespace Turnstile.Sut
{
    /// <summary>
    /// A classic model-based-testing subject: a turnstile that starts locked, unlocks on a coin,
    /// and locks again when pushed through. This is the system-under-test (SUT) the generated
    /// tests replay against. It is <em>stateful</em> — a single instance must persist across the
    /// steps of one test path.
    /// </summary>
    public sealed class Turnstile
    {
        public bool Locked { get; private set; } = true;

        /// <summary>Insert a coin: the turnstile unlocks (idempotent while unlocked).</summary>
        public void Coin()
        {
            Locked = false;
        }

        /// <summary>Push through: only possible while unlocked; it then re-locks.</summary>
        public void Push()
        {
            if (Locked)
            {
                throw new System.InvalidOperationException("cannot push a locked turnstile");
            }

            Locked = true;
        }
    }
}
