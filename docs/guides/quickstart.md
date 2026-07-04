---
title: Quickstart
description: Explore your first SpecExplorerKit model in five minutes.
---

# Quickstart

This walkthrough uses the **Account** sample that ships with SEK to take you from
zero to an explored, rendered transition system in a few minutes.

## 1. Get SEK

Follow [Install SEK](../install/index.md). To use the in-repo build directly:

```bash
git clone https://github.com/stuartpa_microsoft/sek
cd sek
dotnet build src/Sek.Cli/Sek.Cli.csproj
```

For brevity, the rest of this page assumes `sek` is on your `PATH`. If you're
running the in-repo build, replace `sek` with
`dotnet src/Sek.Cli/bin/Debug/sek.dll`.

## 2. Look at the model

`samples/Account/Model/Model.cs` is a tiny model program:

```csharp
public sealed class AccountModel : ModelProgram
{
    public List<Account> Accounts { get; set; } = new();

    [Rule("AccountImpl.CreateAccount")]
    public void CreateAccount()
    {
        Require(Accounts.Count < 2, "bound the number of accounts");
        Accounts.Add(new Account { Balance = 0 });
    }

    [Rule("AccountImpl.SetBalance")]
    public void SetBalance(Account account, int balance) => account.Balance = balance;

    [AcceptingCondition]
    public bool Accepting() => true;
}
```

State lives in public properties. Each `[Rule]` is an action; `Require(...)` guards
it. A reference-typed parameter such as `account` draws its domain from the
accounts that exist in the current state — SEK's
[reachable-object domains](../concepts/object-domains.md).

`samples/Account/Model/Config.cord` describes the scenario and the parameter
domains that Z3 will generate:

```text
config ParameterCombinationConfig : Main
{
    action void AccountImpl.SetBalance(Account account, int balance)
      where {. Condition.In(balance, 10, 100); .};
}

machine AccountExploration() : ParameterCombinationConfig
{
    construct model program from ParameterCombinationConfig
}
```

## 3. Validate the project

```bash
sek validate --project samples/Account
```

This checks that every Cord action maps to a model rule and that machine
`construct` references resolve.

## 4. Explore

```bash
sek explore AccountExploration --project samples/Account
```

```text
Explored 'AccountExploration': 10 states, 58 transitions, 10 accepting.
Wrote samples/Account/.specexplorerkit/out/AccountExploration.seexpl
```

SEK performed a deterministic breadth-first search, used Z3 to generate the
`balance` values, and de-duplicated states by structural hash.

## 5. View the graph

```bash
sek view samples/Account/.specexplorerkit/out/AccountExploration.seexpl \
  --format html --out account.html
```

Formats: `mermaid` (default, great for PRs and docs), `dot` (Graphviz), and
`html` (a self-contained page).

## 6. (Optional) Verify conformance

If you have a system-under-test binding configured (see
[Running conformance](conformance.md)), replay the exploration against it:

```bash
sek test AccountExploration --project samples/Account
```

## What next?

- Understand the pieces in [Concepts](../concepts/index.md).
- Write your own model: [Authoring a model](authoring-a-model.md).
- Learn the scenario language: [Writing Cord](writing-cord.md).
