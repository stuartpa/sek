---
title: Authoring a model
description: Write a SpecExplorerKit model program with state, rules, guards, domains, and accepting conditions.
---

# Authoring a model

A **model program** is a small C# class that captures the *intended* behavior of a
system. SEK explores it into a transition system. This guide covers the moving
parts; see also the [Model programs](../concepts/model-programs.md) concept.

## 1. Create a project

A model is a plain net8 class library that references the `Sek.Modeling` runtime.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>Model</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SpecExplorerKit.Modeling" Version="0.1.0" />
  </ItemGroup>
</Project>
```

> [!NOTE]
> In the SEK source tree, samples use a `ProjectReference` to
> `src/Sek.Modeling/Sek.Modeling.csproj` instead of a package reference.

## 2. Hold state in public properties

The engine snapshots state by serializing your instance to JSON, so **all state
must live in public read/write properties** and the class needs a parameterless
constructor.

```csharp
public sealed class Turnstile : ModelProgram
{
    public bool Locked { get; set; } = true;
}
```

## 3. Write rules

A `[Rule]` method is an action. Guard preconditions with `Require`; mutate state to
describe the effect. The action label defaults to `Type.Method`, or pass an
explicit label.

```csharp
[Rule("Turnstile.Coin")]
public void Coin()
{
    Require(Locked, "already unlocked");
    Locked = false;
}

[Rule("Turnstile.Push")]
public void Push()
{
    Require(!Locked, "still locked");
    Locked = true;
}
```

When `Require(false, ...)` runs, the action is *disabled* in that state (it throws
`GuardDisabledException`, which the explorer treats as "not enabled"). See
[Rules and guards](../concepts/rules-and-guards.md).

## 4. Parameters and domains

Rule parameters get their candidate values from one of:

- **Cord `Condition.In`** for value types (`int`, `string`, `enum`, `bool`) — the
  usual case, solved by Z3. See [Parameter generation](../concepts/parameter-generation.md).
- **A `[Domain("Method")]`** attribute naming a method that returns candidates —
  useful for *state-dependent* domains.
- **Reachable objects** for reference-typed parameters (no attribute needed) — the
  domain is the set of objects of that type in the current state. See
  [Object domains](../concepts/object-domains.md).
- **Natural domains**: `enum` parameters range over all members and `bool` over
  `{false, true}` automatically.

```csharp
[Rule("Sail")]
public void Sail(Heading heading, int hours, int knots) { /* ... */ }

// state-dependent domain:
private int[] XDomain() => new[] { X };
[Rule("RunAground")]
public void RunAground([Domain("XDomain")] int atx) => Require(atx == X, "must match");
```

## 5. Accepting conditions

An `[AcceptingCondition]` is a parameterless `bool` method. A state is *accepting*
when **all** accepting conditions return `true`. Accepting states are the goals for
`construct accepting paths` and `construct test cases`.

```csharp
[AcceptingCondition]
public bool AtRest() => Locked;
```

> [!IMPORTANT]
> If a model declares **no** accepting conditions, **no** state is accepting.

## 6. Build and validate

```bash
dotnet build path/to/Model.csproj
sek validate --project path/to/project
```

`validate` reports Cord actions with no matching rule, and `construct` references
that don't resolve.

## Design tips

- Keep the model **minimal** — only the state needed to decide guards and acceptance.
- Prefer structural state (lists/records) so set-equal states de-duplicate.
- Keep exploration finite: bound collection sizes in guards, or bound the scenario
  in Cord (`StateBound`, `StepBound`, `PathDepthBound`).
- The model describes intended behavior; it is **not** the implementation.
  Conformance to the real system is checked separately — see
  [Running conformance](conformance.md).
