using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.Modeling;

namespace TypeBindingSample
{
    public static class AccountImpl
    {
        static HashSet<Account> accountRecords = new HashSet<Account>();
        static bool injectedBug = false;

        public static Account CreateAccount()
        {
            Account account = new Account();
            accountRecords.Add(account);
            return account;
        }

        public static void SetBalance(Account account, float balance)
        {
            account.setBalance(balance);
        }

        public static float GetBalance(Account account)
        {
            return account.getBalance();
        }

        public static Set<Account> SearchAccounts(float balance)
        {
            SetContainer<Account> set = new SetContainer<Account>();
            var matchingAccounts = from account in accountRecords where account.getBalance() == balance select account;
            foreach (Account account in matchingAccounts)
            {
                set.Add(account);
            }
            if (injectedBug)
                return new Set<Account>();
            return set.ToSet();
        }

        public static void Clear()
        {
            accountRecords.Clear();
        }
    }

    /// <summary>
    /// The real Account class, which contains more fields and methods than AccountDefinition in model
    /// </summary>
    public class Account    
    {
        private string name;
        private string description;
        private float balance;

        public Account()
        {
        }

        public void setName(string accountName)
        {
            this.name = accountName;
        }

        public string getName()
        {
            return this.name;
        }

        public void setDescription(string accountDescription)
        {
            this.description = accountDescription;
        }

        public string getDescription()
        {
            return this.description;
        }

        public void setBalance(float accountBalance)
        {
            this.balance = accountBalance;
        }

        public float getBalance()
        {
            return this.balance;
        }

        public void Deposit(float amount)
        {
            this.balance += amount;
        }

        public void Withdraw(float amount)
        {
            this.balance -= amount;
        }
    }
}
