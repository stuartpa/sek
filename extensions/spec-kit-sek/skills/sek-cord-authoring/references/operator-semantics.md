# Cord operator semantics for agents

Use this reference to **calculate** what a Cord behavior permits. The short operator table in
[implemented language](implemented-language.md) is an index, not a complete semantics.

## Mental model: accepted finite traces

A behavior `B` denotes a set $L(B)$ of accepted finite action traces over an effective action
alphabet $\Sigma$.

- `ε` is the empty trace.
- An atomic action `A` denotes $L(A) = \{ A \}$.
- A machine reference behaves like its referenced body.
- The exploration graph contains every reachable **prefix**. A prefix is a valid completed trace
  only when its destination state is **accepting**.
- A scenario slice may permit a next action without yet being accepting. Combined model/scenario
  acceptance requires both the model state and scenario state to accept.

Example: `A; B` has reachable prefixes `ε`, `A`, and `A B`, but only `A B` is accepted.

## How to reason about an expression

1. Determine the effective bare-label alphabet $\Sigma$ from the machine's action universe.
2. Add parentheses according to the precedence rules below.
3. Compute whether each operand accepts `ε` and list its shortest accepted traces.
4. Apply sequence/choice/repetition transformations.
5. For a parallel product, compute each operand's **full signature** (all labels reachable
   anywhere in that operand), then apply the product rule.
6. Explore the machine and verify initial acceptance, enabled labels, accepted endpoints, and
   expected trace witnesses. Do not infer correctness from state/transition counts alone.

## Precedence: examples after parsing

Postfix operators bind first, then `;`, then `|`, then the parallel/loose/permutation family.
The lowest family is left-associative.

| Written expression | Parsed as |
|---|---|
| `A; B*` | `A; (B*)` |
| `A; B | C` | `(A; B) | C` |
| `A | B; C` | `A | (B; C)` |
| `A; B || C` | `(A; B) || C` |
| `A || B | C` | `A || (B | C)` |
| `A & B -> C` | `(A & B) -> C` |
| `A; B : fail` | `A; (B : fail)` |
| `(A; B) : fail` | failure annotation on the whole sequence endpoint |

Always write the intended parentheses; agents should not rely on readers remembering this table.

## Tight sequence and choice

### `A ; B` — tight sequence

Concatenates every accepted trace of `A` with every accepted trace of `B`:

$$L(A;B) = \{xy \mid x \in L(A),\ y \in L(B)\}$$

No unrelated context action may occur between the operands. If `A` has several accepting states,
`B` is appended at each one.

- `Open; Close` accepts only `Open Close`.
- `(A | B); C` accepts `A C` and `B C`.
- `A?; B` accepts `B` and `A B` because `A?` accepts `ε`.

### `A | B` — choice/union

$$L(A|B) = L(A) \cup L(B)$$

Choice is trace union, not a mutable branch flag. Shared prefixes may merge during DFA
construction: `(A; B) | (A; C)` permits `A` followed by either `B` or `C`.

## Repetition

Repetition applies to the complete operand, not merely its last action.

| Form | Accepted traces for atomic `A` | Initial state accepting? |
|---|---|---|
| `A*` | `ε`, `A`, `A A`, ... | yes |
| `A+` | `A`, `A A`, ... | no |
| `A?` | `ε`, `A` | yes |
| `A{2}` | `A A` | no |
| `A{2,}` | `A A`, `A A A`, ... | no |
| `A{1,3}` / `A{1..3}` | `A`, `A A`, `A A A` | no |

For `(A; B){2}`, the accepted trace is `A B A B`, not `A B B`. Use non-negative bounds
with minimum no greater than maximum; do not depend on unspecified handling of malformed bounds.

## Alphabet-wide operators

These operators range over the effective **bare action labels** in $\Sigma$. In pure behavior
mode, declare that alphabet explicitly. They do not invent or match unseen action names.
Argument-pinned scenario symbols are reached by their explicit atoms, not by `_` enumeration.

### `_` — exactly one arbitrary action

$$L(\_) = \{a \mid a \in \Sigma\}$$

`Open; _; Close` requires exactly one alphabet action between `Open` and `Close`.

### `...` — any finite sequence

`...` is exactly `_*`, so it accepts `ε` and every finite sequence over $\Sigma$.

`Open; ...; Close` permits zero or more context actions between the anchors.

### `!A` — one action other than `A`

$$L(!A) = \Sigma \setminus \{A\}$$

This is **atomic-label negation**, not complement of an arbitrary behavior or trace language.
Arguments are ignored by the current lowering, so `!A(1)` excludes the bare target `A`, not only
`A(1)`. Apply `!` only to one atomic action and prefer a bare label.

## Loose sequence and permutation

### `A -> B` — loose sequence

SEK lowers it to:

```text
A; _*; B
```

Therefore $A$ and $B$ are still required and ordered, but any finite sequence over $\Sigma$ may
occur between them. This can greatly enlarge a graph; use tight sequence unless context actions are
part of the requirement.

### `A & B` — binary block permutation

SEK lowers a two-operand permutation to:

```text
(A; B) | (B; A)
```

Each operand remains an indivisible behavior block: `(A; B) & (C; D)` accepts `A B C D` and
`C D A B`, with no action-level interleaving. `&` is binary and left-associative; `A & B & C`
means `(A & B) & C` and does **not** produce all six permutations. Write an explicit choice when
all permutations are required.

## Parallel products

For every parallel operator, a product state is a pair `(leftState, rightState)`. The product is
accepting only when **both** component states are accepting. Keep a parallel expression at the
current/root composition: nested parallel beneath `;`, `|`, repetition, `&`, or `->` can compile as
empty in the current engine.

A bare transition can synchronize with an argument-pinned form of the same action; the resulting
product label is the pinned form. Avoid ambiguous short action labels.

### `A || B` — fully synchronized product

Every emitted action must advance **both** operands from their current states. Conceptually, for
ordinary bare labels this accepts the intersection of their trace languages:

$$L(A || B) \approx L(A) \cap L(B)$$

- `(A; B) || (A; B)` accepts `A B`.
- `(A; B) || (A; C)` can take shared prefix `A`, then deadlocks; it has no accepted trace.
- An action enabled on only one side is blocked.

Use `||` for scenario slicing (`Scenario || ModelProgram`) or when two protocols must agree on every
step—not merely on the labels they happen to share.

### `A ||| B` — interleaved product

With disjoint signatures, this accepts every order-preserving shuffle of one accepted left trace
and one accepted right trace. `(A; B) ||| C` accepts:

```text
A B C
A C B
C A B
```

Each event advances exactly one side, and both sides must eventually accept. **Current limitation:**
when both sides can independently emit the same label from one product state, the deterministic
transition map retains only one successor instead of both nondeterministic choices. Use `|||` only
with disjoint operand signatures unless the exact overlap behavior has a regression test.

### `A |?| B` — synchronize shared signature, interleave the rest

SEK computes the intersection of the operands' **full reachable signatures** once. Labels in that
intersection must always advance both sides; labels unique to one signature advance that side only.

Example:

```text
(A; B) |?| (A; C)
```

The shared signature is `{A}`. `A` must synchronize, after which `B` and `C` interleave, yielding
accepted traces `A B C` and `A C B`.

A shared label is blocked whenever only one current component state can take it, even if the other
side could take it later. Non-shared actions may move a side until synchronization becomes possible.
This is signature-based synchronization, not "synchronize labels that are simultaneously enabled."

## Failure annotation

### `B : fail`

The endpoint reached after completing `B` is marked as a model-check failure state. The annotation
does not complement or remove `B` from the trace language.

- `A; B : fail` marks completion of `B` (the second atom) as failure.
- `(A; B) : fail` explicitly marks the whole sequence endpoint and is preferred.
- In a product, either component entering a fail state marks the product state as failure.
- `StopAtError` stops expansion after a reached failure; it does not force `sek explore` to return a
  nonzero exit code. Inspect fail metadata and enforce the expected fail count externally.

Use `: fail` only in a model-backed slice. Pure behavior rendering does not provide the complete
model-check verdict contract.

## Choosing the operator

| Requirement | Operator |
|---|---|
| One behavior immediately after another | `;` |
| Either behavior | `|` |
| Optional/repeated complete behavior | `?`, `*`, `+`, bounds |
| Ordered anchors with arbitrary context between | `->` |
| Two whole blocks in either order | `&` |
| Both sides agree on every action | `||` |
| Independent disjoint protocols, all shuffles | `|||` |
| Shared protocol actions synchronize; private actions interleave | `|?|` |
| Exactly one / any number of context actions | `_` / `...` |
| One action except a named atomic action | `!A` |
| Identify a forbidden completed trace | `(B) : fail` |

## Review checklist

Before accepting generated Cord, require the author or agent to state:

1. the parsed, fully parenthesized expression;
2. the effective alphabet and each parallel operand's full signature;
3. whether `ε` is accepted;
4. at least one accepted trace and one intentionally rejected trace;
5. why the chosen parallel family is correct;
6. that no parallel product is nested under unsupported algebra;
7. whether same-label `|||` overlap exists;
8. how fail states and command exit status are checked.

Use the parse-validated [behavior-mode asset](../assets/behavior-mode.cord) and the
[`samples/Operators/Config.cord`](https://github.com/stuartpa/sek/blob/main/samples/Operators/Config.cord)
as executable examples.
