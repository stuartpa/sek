using System.Collections.Generic;
using System.Linq;
using Sek.Modeling;

namespace AccountSample
{
    /// <summary>A model-side account. State is its balance; identity is structural.</summary>
    public sealed class Account
    {
        public float Balance { get; set; }
    }

    /// <summary>
    /// The Account model. Accounts are created dynamically and held in model state; rules
    /// that take an <see cref="Account"/> parameter draw their domain from the accounts
    /// reachable in the current state (SEK's general reachable-object domain — the engine
    /// has no Account-specific knowledge).
    /// </summary>
    public sealed class AccountModel : ModelProgram
    {
        public List<Account> Accounts { get; set; } = new List<Account>();

        [Rule("AccountImpl.CreateAccount")]
        public void CreateAccount()
        {
            Require(Accounts.Count < 2, "bound the number of accounts");
            Accounts.Add(new Account { Balance = 0 });
        }

        [Rule("AccountImpl.SetBalance")]
        public void SetBalance(Account account, float balance)
        {
            account.Balance = balance;
        }

        [Rule("AccountImpl.GetBalance")]
        public void GetBalance(Account account)
        {
            // Observation only — no state change.
        }

        /// <summary>Returns the accounts whose balance matches (the classic sample's
        /// Set&lt;Account&gt;-returning LINQ query). Observation only during exploration.</summary>
        [Rule("AccountImpl.SearchAccounts")]
        public IEnumerable<Account> SearchAccounts(float balance)
        {
            return Accounts.Where(a => a.Balance == balance).ToList();
        }

        [Rule("AccountImpl.Clear")]
        public void Clear()
        {
            Require(Accounts.Count > 0, "nothing to clear");
            Accounts.Clear();
        }

        [AcceptingCondition]
        public bool Accepting() => true;
    }
}
