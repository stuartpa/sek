---
title: Cord language reference
description: The full grammar of the Cord configuration and scenario language as implemented by SpecExplorerKit.
---

# Cord language reference

This is the grammar reference for **Cord** as implemented by SEK. For a gentle
introduction see [The Cord language](../concepts/cord-language.md) and the guide
[Writing Cord scenarios](../guides/writing-cord.md).

## Lexical structure

- **Comments**: `// line` and `/* block */`.
- **Identifiers, strings, numbers**: as in C#.
- **Embedded C#**: `(. expression .)` and `{. statements .}`.

## Grammar (EBNF)

Meta-notation: `::=` defines, `|` alternation, `( )` grouping, `[ ]` optional,
`{ }` zero-or-more.

```ebnf
CordScript    ::= { UsingClause } { Configuration | Machine } .
UsingClause   ::= 'using' QualIdent ';' .

Configuration ::= 'config' Ident [ ':' ConfigList ] '{' { ConfigClause ';' } '}' .
ConfigList    ::= Ident { ',' Ident } .
ConfigClause  ::= ActionClause | SwitchClause .

ActionClause  ::= ImportActions | DeclaredAction .
ImportActions ::= 'action' 'all' [ 'public' | 'internal' ] Type .
DeclaredAction::= 'action' [ 'exclude' | 'abstract' ] [ 'event' ]
                  [ 'static' ] RetType QualIdent '(' [ ParamList ] ')'
                  [ 'where' WhereBlock ] .
ParamList     ::= [ 'out' | 'ref' ] Type Ident { ',' [ 'out' | 'ref' ] Type Ident } .
WhereBlock    ::= '{.' { ConstraintStmt } '.}' .

SwitchClause  ::= 'switch' Ident '=' ( Literal | Ident | 'none' ) .

Machine       ::= 'machine' Ident '(' [ ParamList ] ')' [ '/' VarDecl ]
                  ':' ConfigList [ 'where' Switch { ',' Switch } ]
                  '{' Behavior '}' .

Behavior      ::= ParallelExpr .
ParallelExpr  ::= ChoiceExpr { ( '||' | '|||' | '|?|' | '&' | '->' ) ChoiceExpr } .
ChoiceExpr    ::= SeqExpr { '|' SeqExpr } .
SeqExpr       ::= PostfixExpr { ';' PostfixExpr } .
PostfixExpr   ::= Primary { '*' | '+' | '?' | '{' Int [ ( ',' [ Int ] ) | ( '..' Int ) ] '}' } .
Primary       ::= '(' Behavior ')'
                | '{.' CSharp '.}' ':' PostfixExpr        (* preconstraint *)
                | Construct
                | Let
                | '...'                                    (* any sequence = _* *)
                | Invocation .

Construct     ::= 'construct' 'model' 'program' 'from' QualIdent [ 'where' … ]
                | 'construct' 'accepting' 'paths' 'for' ( QualIdent | '(' Behavior ')' )
                | 'construct' 'test' 'cases' [ 'where' … ] 'for' ( QualIdent | '(' Behavior ')' ) .

Let           ::= 'let' … 'in' Behavior .

Invocation    ::= [ '!' ] [ 'call' | 'return' | 'event' ] ( '_' | QualIdent )
                  [ '(' [ ArgList ] ')' ] [ '/' Arg ] .
ArgList       ::= Arg { ',' Arg } .

Type          ::= SimpleType { '[' { ',' } ']' } .
QualIdent     ::= Ident { '.' Ident } .
Literal       ::= String | Number | 'true' | 'false' | 'null' .
```

## Behavior operators

Listed from lowest to highest precedence (parallel family binds loosest,
repetition binds tightest):

| Operator | Name | Meaning |
|---|---|---|
| `\|\|` | synchronized parallel | steps must synchronize on shared actions |
| `\|\|\|` | interleaved parallel | all interleavings of the operands |
| `\|?\|` | sync-interleaved parallel | shared actions synchronize, the rest interleave |
| `&` | permutation | both operands as atomic blocks, in either order |
| `->` | loose sequence | second operand after the first, with context actions allowed between |
| `\|` | choice / union | either operand |
| `;` | tight sequence | second operand immediately after the first |
| `*` `+` `?` | repetition | zero-or-more / one-or-more / optional |
| `{n}` `{n,}` `{n,m}` | bounded repetition | exactly / at-least / between |
| `_` | any action | any single action in the context signature |
| `...` | any sequence | zero or more of any action (`_*`) |
| `!` | negation | any atomic action *except* the operand (`call`/`return`/`event`) |

## `where` constraints

Inside a declared action's `where {. … .}` block:

| Constraint | Meaning |
|---|---|
| `Condition.In(p, v1, v2, …)` | parameter `p`'s candidate domain |
| `Condition.IsTrue(expr)` | boolean predicate (pruning); operators `== != < <= > >=`, `&& \|\| !`, `+ - * / %`, bitwise `& \|`; enum-qualified literals allowed |
| `Combination.Interaction(…)` | full product (default) |
| `Combination.Pairwise(…)` | minimal 2-wise cover |

## Switches

| Switch | Meaning |
|---|---|
| `StateBound` | max distinct states |
| `StepBound` | max transitions |
| `PathDepthBound` | max path depth from the initial state |
| `TestEnabled` | marks test-suite machines (informational) |

Other Spec Explorer switches (e.g. `GeneratedTestPath`, `TestClassBase`,
`RecommendedViews`) are accepted and ignored where not yet meaningful to `sek`.

## Notes on parity

- `construct model program from <Config>` and the full behavior algebra are supported.
- **Scenario slicing** — `Scenario || construct model program from <Config>` —
  restricts the model exploration to the action sequences the scenario permits.
- **Combination refinements** `Combination.Isolated`, `Combination.Seeded`, and
  `Combination.Expand` are supported (in addition to `Interaction` and `Pairwise`).
- **`bind Action(domain…) in Behavior`** binds parameter domains for an action within a
  behavior (literal values, `{set}` domains, and `_` unbound; structured/struct binds are
  treated as unbound).
- **`construct bounded exploration where PathDepth=n for M`** bounds the exploration depth.
- **`construct accepting paths for M`** keeps only paths that reach an accepting state.
- **`construct point shoot …`**, **`construct accept completion …`**, and
  **`construct requirement coverage …`** are parsed and explored; the point-shoot /
  accept-completion *steering heuristics* are approximated by exploring the referenced
  target machine, and requirement coverage reports the covered action set.
- **Argument-pattern matching** inside scenarios is supported: a scenario atom such as
  `AddJob("x", 600000)` slices the model to runs whose invocation arguments match those
  values, while a bare `AddJob` still matches any arguments.
- **`let vars where {. … .} in Behavior`** parameter generation is supported: each binding
  of the `let` variables (constrained by the `where` block) is substituted into the inner
  behavior and the resulting argument-matched behaviors are unioned before slicing.
- On the roadmap: native domain expressions (`instances T`, `T[n..m]`, ranges), and
  execution of arbitrary embedded C# in `{. … .}` / `(. … .)` blocks (beyond the
  recognized `Condition.*`/`Combination.*` patterns).

## Related

- [The Cord language](../concepts/cord-language.md)
- [Parameter generation](../concepts/parameter-generation.md)
- [Operators sample](../samples/operators.md)
