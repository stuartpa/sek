# Cord language implemented by SEK

This reference describes behavior consumed by current SEK. The
[support matrix](support-and-safety.md) overrides broad legacy descriptions.

## Lexical and top-level form

Cord supports `//` and `/* ... */` comments, identifiers, signed decimal integers, quoted strings
with basic escapes, and embedded `(. expression .)` / `{. statements .}` blocks. The lexer is
C#-like, not the complete C# lexer; avoid character/floating literals, numeric suffixes,
interpolated/verbatim strings, and `@identifier` in Cord syntax.

```ebnf
Script  ::= { Using | Config | Machine }
Using   ::= "using" QualifiedName ";"
Config  ::= "config" Name [":" Base {"," Base}] "{" {Clause} "}"
Machine ::= "machine" Name "(" [Parameters] ")" ["/" ResultDecl]
            ":" Config {"," Config} ["where" Switch {"," Switch}]
            "{" Behavior "}"
```

`using` is retained for readability but does not resolve names. Config bases are applied in order;
later bases and the derived config override earlier values. Names and switch keys are case-sensitive.
Treat unknown/cyclic bases as errors even where validation only warns or stops walking a cycle.

## Actions

```text
action all AdapterType;
action void Service.Start(int id);
action event void Service.Completed(int id);
action Result Service.Query(string key)
    where {.
        Condition.In(key, "a", "b");
    .};
```

- Explicit declarations select model rule labels and are preferred.
- `action all T` resolves labels by qualifier leaf, but is Conditional: a no-match path can expose
  the complete rule set. Verify the explored action universe.
- Config-level `event` tags generated steps as observations, subject to replay limitations below.
- `public`, `internal`, `static`, and `abstract` are compatibility syntax only.
- Never use `exclude`; exclusion semantics are not applied.
- Declared return types and `out`/`ref` modifiers do not control reflected execution.
- Only embedded `where {. ... .}` / `where (. ... .)` blocks have effective constraint semantics.

## Constraints and combinations

### Domains

```text
Condition.In(id, 1, 2, 3);
Condition.In(mode, Mode.Read, Mode.Write);
Condition.In(info.Kind, "x", "y");
```

Supported for direct scalar parameters and public struct fields. Multiple `Condition.In` calls for
the same parameter form a union. Unknown parameter names can be ignored, so inspect emitted values.

### Predicates

```text
Condition.IsTrue(id >= 1 && id <= 8);
Condition.IsTrue(left != right);
```

The native expression path supports Boolean/bitwise operators, comparisons, integer arithmetic,
unary operators, strings, integers/hex integers, parameters, and dotted enum/field names. More
complex primitive expressions may run through a Roslyn post-filter. Unrecognized expressions can
be dropped; prefer simple predicates and verify generated rows.

### Combinations

| Form | Effect |
|---|---|
| `Combination.Interaction(...)` | Full satisfying Cartesian product (default) |
| `Combination.Pairwise(...)` | Greedy pairwise cover, including derived expression columns |
| `Combination.Expand(...)` | Adds rows to represent observed tuples |
| `Combination.Isolated(expr)` | Retains an isolated satisfying row per predicate |
| `Combination.Seeded(expr, ...)` | Adds a satisfying conjunction row when absent |

Plain Pairwise currently reduces over all action parameters, not only a listed subset. Advanced
refinements are Conditional on object/floating enumeration paths.

### Probability ordering

```text
if (Probability.IsTrue(0.8))
    Condition.In(name, "normal-a", "normal-b");
else
    Condition.In(name, "rare-error");
switch RandomSeed = 2;
```

SEK unions both branch domains. Probability plus `RandomSeed` controls reproducible ordering for
bounded generation; it is not statistical sampling and does not discard the rare branch.

## Effective switches

| Switch | Effect |
|---|---|
| `StateBound` | Maximum distinct model states |
| `StepBound` | Maximum model transitions |
| `PathDepthBound` | Maximum BFS depth |
| `StopAtError` | Stops expansion after the first reached model-check fail state |
| `RandomSeed` | Orders probabilistic branch unions reproducibly |

`TestEnabled`, `ForExploration`, generated output, UI/view, timeout, and expansion-limit switches
are Parsed-only or informational. Use CLI options for generated output and namespace.

## Behavior precedence

Lowest to highest:

1. `||`, `|||`, `|?|`, `&`, `->` — one left-associative family.
2. `|` — choice.
3. `;` — tight sequence.
4. `*`, `+`, `?`, `{n}`, `{n,}`, `{n,m}`, `{n..m}`, then optional `: fail`.
5. Grouping, preconstraint, constructs, `let`, `bind`, `...`, invocation.

Always parenthesize mixed operators for reviewability.

## Operators

The table is a scan-friendly index. Before composing operators, use
[operator semantics](operator-semantics.md) to calculate accepted traces, `ε` acceptance,
parallel signatures, synchronization, and failure scope.

| Operator | SEK meaning |
|---|---|
| `A ; B` | Tight sequence |
| `A | B` | Choice/union |
| `A*`, `A+`, `A?` | Zero-or-more, one-or-more, optional |
| `A{n}`, `A{n,}`, `A{n,m}` / `A{n..m}` | Exact, at-least, bounded repetition |
| `_` | One action from the current behavior alphabet |
| `...` | Zero or more alphabet actions (`_*`) |
| `!A` | Any bare alphabet action except atomic target `A` |
| `A -> B` | `A ; _* ; B` |
| `A & B` | Binary block permutation `(A;B) | (B;A)` |
| `A || B` | Synchronized product |
| `A ||| B` | Interleaved product |
| `A |?| B` | Shared exact labels synchronize; others interleave |
| `B : fail` | Marks a model-check failure endpoint in a model slice |

Parallel products are Conditional: keep them as the current/root composition. A parallel node
nested under sequence, choice, repetition, permutation, or loose sequence can compile as empty.
`|||` also cannot reliably retain two distinct same-label successors.

## Invocations and arguments

```text
Start
Start()
Start(1, _)
new Session(_)
Producer() / handle; Consumer(handle)
```

Bare `Action` and `Action()` match any concrete arguments. A non-empty list pins supplied values;
`_` is a per-argument wildcard. Scenario matching uses short labels, so keep those labels unique.
`new T` is an ordinary model action named for `T`; it has no intrinsic allocation semantics.

Config-level `action event` causes generated `Observe`, but current Observe directly invokes the
bound method rather than awaiting an asynchronous channel. Behavior-level `call`, `return`, and
`event` qualifiers are Parsed-only. `/` return binding threads the model's returned value during
slicing; generated/offline replay does not capture and thread the SUT's runtime return.

## `bind`, `let`, and preconstraints

### Top-level bind

```text
bind Open({1, 2}, _), Write(_, {"a", "b"})
in
construct model program from Config
```

Supported atoms: `_`, one literal/qualified identifier, `{a,b}`, integer `a..b`, union `+`,
`instances T`, and structured `Type(Field=domain, ...)`. Keep bind top-level. A concrete bind
replaces an action's extracted constraints; it does not intersect them. Verify emitted arguments.

### Bounded let

```text
let int id, int other
    where {.
        Condition.In(id, 1, 2, 3);
        Condition.In(other, 1, 2, 3);
        Condition.IsTrue(id != other);
    .}
in
Request(id); Response(other)
```

SEK lowers finite assignments into a choice and substitutes complete argument tokens. Verify that
assignments are non-empty: a zero-row let can leave the unsubstituted behavior in place.

### State preconstraint

`{. Type.StaticMember = value; .}: Behavior` reflectively assigns public static model-assembly
state. This is Conditional because qualifier/conversion/set failures can be swallowed. Prefer a
modeled initialization action.

## Constructs

| Construct | Current behavior |
|---|---|
| `construct model program from C` | Supported model exploration |
| `construct accepting paths for M` | Model-backed acceptance pruning |
| `construct bounded exploration where PathDepth=n for M` | Conditional; depth only |
| `construct test cases where strategy="shorttests"|"longtests" for M` | Conditional generation path strategy |
| `construct point shoot ... for M` | Conditional phased steering with a simple Boolean goal |
| `construct accept completion ... for M` | Conditional acceptance pruning; completer semantics incomplete |
| `construct requirement coverage ... for M` | Conditional aggregate reporting, not requirement-directed selection |

Use constructs around model-backed machines. Pure behavior/inline construct targets can collapse to
empty; explore a pure behavior machine directly.

`B : fail` marks a failure in model slicing. `StopAtError` truncates exploration, but `sek explore`
can still exit successfully and generated tests do not automatically assert the model-check verdict.
Inspect fail metadata and enforce the expected fail count externally.
