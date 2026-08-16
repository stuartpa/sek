# Cord support and safety matrix

The lexer, parser, semantic phase, explorer, and replay implementation—not legacy Spec Explorer
documentation—define behavior. Successful parsing is not runtime support.

| Surface | Status | Authoring rule |
|---|---|---|
| Config/machine declarations and inheritance | Supported | Reject unknown/cyclic bases |
| `using` name resolution | Parsed-only | Readability only |
| Explicit actions | Supported | Preferred; keep scenario short labels unique |
| `action all` | Conditional | Verify exact rules; no-match can expose all rules |
| `public/internal/static/abstract` | Parsed-only | Cosmetic only |
| `exclude` | Parsed-only/unsafe | Never author |
| Config `action event` | Conditional | Tagged, but replay is direct invocation |
| Config/behavior `call` and `return` kinds | Parsed-only | Do not rely on separate call/return semantics |
| `Condition.In` | Supported | Use finite domains; inspect unknown-name cases |
| `Condition.IsTrue` | Conditional | Prefer simple expressions; verify rows |
| Interaction/Pairwise/Expand/Isolated/Seeded | Supported/Conditional | Strongest on scalar/struct paths |
| `Probability.IsTrue` + `RandomSeed` | Conditional | Both branches union; seed orders, not samples |
| Legacy parenthesized action domains | Parsed-only | Never author |
| Config `domain` / `bound` clauses | Unsupported/swallowed | Never author |
| `;`, `|`, grouping, repetition | Supported | Parenthesize mixed expressions |
| `_`, `...`, `!` | Conditional | Known alphabet; negation is atomic-target only |
| `->` and binary `&` | Supported lowering | Understand context and binary-block semantics |
| Root/current `||`, `|||`, `|?|` | Conditional | Keep at root; verify same-label interleave |
| Nested parallel | Unsupported behavior | Can become empty; never author |
| Argument pinning and per-argument `_` | Supported | Non-empty arg list pins |
| Reachable object domains | Conditional | Verify identity and emitted arguments |
| `new T` | Conditional | Ordinary model action, not intrinsic allocation |
| Parameterized machines | Conditional | Whole-token substitution; verify arity/types |
| Bounded `let` | Conditional | Prove nonzero concrete assignments |
| Top-level model-backed `bind` | Conditional | Domain replacement, not intersection |
| State preconstraint | Conditional | Reflective assignment may fail silently |
| `: fail` in a model slice | Conditional | Inspect fail metadata; exit can succeed |
| Direct model construct | Supported | Preferred core machine |
| Model-backed accepting paths | Supported | Verify non-empty result |
| Pure-behavior construct targets | Unsupported/unsafe | Explore behavior directly |
| Bounded exploration | Conditional | `PathDepth` only |
| Test-case construct | Conditional | Target plus short/long path strategy |
| Point-shoot | Conditional | Simple Boolean goal; verify phases/goals |
| Accept-completion | Conditional | Acceptance prune; completer incomplete |
| Requirement coverage | Conditional | Aggregate report, not directed generation |
| Model return in graph | Supported | Reflected model result recorded |
| `/` return binding in slicing | Conditional | Model-time dataflow only |
| Dynamic SUT return binding in generated replay | Unsupported | Use a custom harness |
| Collection-size domains, maplets, `TypeBinding` | Unsupported | Do not author |
| Hiding, postconstraints, broader legacy grammar | Unsupported | Do not author |

## Fail-closed checklist

1. Treat semantic warnings as failures.
2. Compare the explored action universe with the expected `[Rule]` inventory.
3. Give every scalar parameter a finite domain; do not rely on unconstrained enumeration.
4. Verify every constraint changes emitted arguments; unknown/unrecognized constraints can drop.
5. Keep `let` finite/non-empty and `bind` top-level; inspect replacement domains.
6. Keep parallel at the current/root composition.
7. Use constructs only around model-backed targets.
8. Avoid reflective state slices unless no modeled initialization action works.
9. Convert reached `: fail` states into an external failing verdict.
10. Generate model-derived negative conformance from an unsliced machine; slices omit it.
11. Do not infer per-input rejection coverage from an action-level negative edge.
12. Do not claim asynchronous event conformance from generated `Observe`.
13. Do not claim dynamic SUT-handle threading from `/` binding in generated tests.
14. Reject bounds, empty/suspicious graphs, missing actions, unexpected acceptance/fail states,
    unmet requirements, and missing negative tests.
15. Build current model and SUT binaries; reject stale graphs and generated suites.
16. Treat `sek validate` as necessary but insufficient for scopes, invocation references, arity,
    finite domains, constructs, and runtime semantics.

## Evidence to record

For each machine record Cord/model identities, SEK version, solver, bounds and bound-hit status,
states/transitions/acceptance, action labels and arguments, fail/goal/requirement counts, positive
paths, negative tests, refreshed generated destination, binding identity, and replay result. Keep
direct-model evidence separate from sliced-machine evidence because they prove different things.
