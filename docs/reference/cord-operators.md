---
title: Cord operator semantics
description: Calculate accepted Cord traces, acceptance, repetition, alphabet-wide behavior, and synchronized/interleaved products in SpecExplorerKit.
---

# Cord operator semantics

The summary table in the [Cord language reference](cord-language.md) is an index. This page gives
the operational model needed to design and review nontrivial behavior expressions.

## Accepted finite traces

A behavior `B` denotes a set $L(B)$ of accepted finite action traces over an effective action
alphabet $\Sigma$.

- `ε` is the empty trace.
- An atomic action `A` denotes $L(A) = \{A\}$.
- A machine reference behaves like its body.
- The graph contains reachable **prefixes**. A prefix is a valid completed trace only when its
  destination state is **accepting**.
- A parallel product accepts only when both component states accept.
- A model/scenario slice accepts only when both the model and scenario accept.

`A; B` has reachable prefixes `ε`, `A`, and `A B`, but only `A B` is accepted.

## Reasoning procedure

1. Determine the effective bare-label alphabet $\Sigma$.
2. Add explicit parentheses using [precedence](cord-language.md#behavior-precedence).
3. Determine whether each operand accepts `ε` and list its shortest accepted traces.
4. Apply sequence, choice, and repetition transformations.
5. For parallel composition, compute both operands' **full signatures** (labels reachable anywhere).
6. Explore and verify initial acceptance, enabled labels, accepting endpoints, and trace witnesses.

## Precedence examples

| Written | Parsed as |
|---|---|
| `A; B*` | `A; (B*)` |
| `A; B | C` | `(A; B) | C` |
| `A | B; C` | `A | (B; C)` |
| `A; B || C` | `(A; B) || C` |
| `A || B | C` | `A || (B | C)` |
| `A & B -> C` | `(A & B) -> C` |
| `A; B : fail` | `A; (B : fail)` |
| `(A; B) : fail` | failure annotation on the complete sequence endpoint |

Write the parentheses rather than relying on precedence memory.

## Sequence and choice

### Tight sequence: `A ; B`

$$L(A;B) = \{xy \mid x \in L(A),\ y \in L(B)\}$$

No context action may occur between operands. `(A | B); C` accepts `A C` and `B C`.
`A?; B` accepts `B` and `A B` because `A?` accepts `ε`.

### Choice: `A | B`

$$L(A|B) = L(A) \cup L(B)$$

Choice is trace union, not a mutable branch flag. `(A; B) | (A; C)` may merge its shared `A`
prefix and then permit either continuation.

## Repetition

| Form | Atomic-`A` traces | Initial state accepting? |
|---|---|---|
| `A*` | `ε`, `A`, `A A`, ... | yes |
| `A+` | `A`, `A A`, ... | no |
| `A?` | `ε`, `A` | yes |
| `A{2}` | `A A` | no |
| `A{2,}` | `A A`, `A A A`, ... | no |
| `A{1,3}` / `A{1..3}` | `A`, `A A`, `A A A` | no |

Repetition applies to the complete operand: `(A; B){2}` accepts `A B A B`. Use non-negative
bounds with minimum no greater than maximum.

## Alphabet-wide operators

These range over effective **bare action labels** in $\Sigma$. In pure behavior mode, declare the
alphabet explicitly. They do not invent unseen labels, and explicit pinned symbols are not produced
by wildcard enumeration.

- `_` accepts exactly one arbitrary bare action: $L(\_) = \{a \mid a\in\Sigma\}$.
- `...` is `_*`, accepting every finite sequence over $\Sigma$, including `ε`.
- `!A` accepts one bare action except `A`: $L(!A)=\Sigma\setminus\{A\}$.

`!` is atomic-label negation, not language complement. Current lowering ignores arguments, so
`!A(1)` excludes target `A`, not only `A(1)`; prefer bare `!A`.

## Loose sequence and permutation

### Loose sequence: `A -> B`

SEK lowers this to `A; _*; B`: both anchors remain required and ordered, while arbitrary context
actions may occur between them. It can dramatically enlarge the graph.

### Binary block permutation: `A & B`

SEK lowers this to `(A; B) | (B; A)`. Compound operands remain whole blocks, so
`(A; B) & (C; D)` accepts `A B C D` and `C D A B`, not action-level interleavings.

`&` is binary and left-associative. `A & B & C` means `(A & B) & C` and does **not** produce all six
permutations; write an explicit choice when all permutations are required.

## Parallel products

A product state is a pair `(leftState, rightState)` and accepts only when both components accept.
Keep parallel at the current/root composition: nested parallel under `;`, `|`, repetition, `&`, or
`->` can compile as empty in the current engine.

A bare label can synchronize with a pinned form of the same action, yielding the pinned label.

### Fully synchronized: `A || B`

Every emitted action must advance both current component states. For ordinary bare labels this is
conceptually trace-language intersection:

$$L(A||B) \approx L(A) \cap L(B)$$

`(A; B) || (A; B)` accepts `A B`. `(A; B) || (A; C)` can take `A` and then deadlocks without an
accepted trace. An action enabled on only one side is blocked.

Use `||` when both sides must agree on every step, including scenario slicing.

### Interleaved: `A ||| B`

With disjoint signatures, this accepts every order-preserving shuffle. `(A; B) ||| C` accepts:

```text
A B C
A C B
C A B
```

Each event advances one side; both sides must eventually accept. Current limitation: if both sides
can independently emit the same label from one product state, the deterministic map retains only
one successor. Use `|||` with disjoint signatures unless overlap behavior has a dedicated regression
test.

### Shared-signature synchronization: `A |?| B`

SEK computes the intersection of both operands' **full reachable signatures** once. Shared labels
must advance both sides; labels unique to one signature interleave.

`(A; B) |?| (A; C)` has shared signature `{A}`. `A` synchronizes, then `B` and `C` interleave,
accepting `A B C` and `A C B`.

A shared label is blocked when only one current state can take it, even if the other side can take it
later. This is signature-based—not "synchronize whatever is simultaneously enabled."

## Failure annotation: `B : fail`

Completing `B` marks the endpoint as a model-check failure; it does not remove or complement `B`.
Prefer `(A; B) : fail` to make the annotated scope explicit. In a product, either component failure
marks the product failure.

`StopAtError` truncates expansion after a reached failure but does not force a nonzero `sek explore`
exit. Inspect fail metadata and enforce expected fail counts externally. Use `: fail` in a
model-backed slice, not as a pure behavior verdict.

## Operator selection

| Requirement | Use |
|---|---|
| Immediate ordering | `;` |
| Alternative traces | `|` |
| Optional/repeated complete behavior | `?`, `*`, `+`, bounds |
| Ordered anchors with arbitrary context | `->` |
| Two whole blocks in either order | `&` |
| Both sides agree on every action | `||` |
| Independent disjoint protocols, all shuffles | `|||` |
| Shared labels synchronize; private labels interleave | `|?|` |
| One / any number of context actions | `_` / `...` |
| One action except named atomic action | `!A` |
| Mark a forbidden completed trace | `(B) : fail` |

## Review checklist

Require an author or agent to state:

1. the fully parenthesized parse;
2. the effective alphabet and parallel signatures;
3. whether `ε` is accepted;
4. one accepted and one rejected trace;
5. why the chosen parallel family fits;
6. that no parallel product is nested under unsupported algebra;
7. whether same-label `|||` overlap exists;
8. how failure metadata and exit status are checked.

See the [Operators sample](../samples/operators.md) and the
[operator support matrix](cord-support.md).
