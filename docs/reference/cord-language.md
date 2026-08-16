---
title: Cord language reference
description: The implemented syntax, precedence, and runtime semantics of the Cord language in SpecExplorerKit.
---

# Cord language reference

This is the language reference for **Cord as implemented by SEK**. It is not a claim that
every legacy Microsoft Spec Explorer production is implemented. Consult the
[Cord support matrix](cord-support.md) before using advanced or compatibility syntax.
For a gentle introduction, see [The Cord language](../concepts/cord-language.md) and
[Writing Cord scenarios](../guides/writing-cord.md).

## Lexical structure

- **Comments**: `// line` and `/* block */`.
- **Identifiers**: letter/underscore followed by letters, digits, or underscores.
- **Literals**: signed decimal integers, quoted strings with basic escapes, `true`, `false`,
  and `null`.
- **Embedded C#**: `(. expression .)` and `{. statements .}`.

The surface is C#-like but is not the complete C# lexer. Do not use character/floating
literals, numeric suffixes, interpolated/verbatim strings, or `@identifier` as Cord tokens.

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
DeclaredAction::= 'action' [ 'exclude' | 'abstract' ] [ 'event' | 'call' | 'return' ]
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
PostfixExpr   ::= Primary { '*' | '+' | '?' | RepetitionCount } [ ':' 'fail' ] .
RepetitionCount ::= '{' Int [ ',' [ Int ] | '..' Int ] '}' .
Primary       ::= '(' Behavior ')'
                | '{.' CSharp '.}' ':' PostfixExpr        (* preconstraint *)
                | Construct
                | Let
                | Bind
                | '...'                                    (* any sequence = _* *)
                | Invocation .

Construct     ::= 'construct' 'model' 'program' 'from' QualIdent [ 'where' … ]
                | 'construct' 'accepting' 'paths' 'for' ( QualIdent | '(' Behavior ')' )
                | 'construct' 'test' 'cases' [ 'where' … ] 'for' ( QualIdent | '(' Behavior ')' )
                | 'construct' 'bounded' 'exploration' [ 'where' … ] 'for' Target
                | 'construct' 'point' 'shoot' [ 'where' … ] [ 'with' Embedded ] 'for' Target
                | 'construct' 'accept' 'completion' [ 'where' … ] 'for' Target
                | 'construct' 'requirement' 'coverage' [ 'where' … ] 'for' Target .

Let           ::= 'let' … 'in' Behavior .
Bind          ::= 'bind' BindClause { ',' BindClause } 'in' Behavior .
BindClause    ::= QualIdent [ '(' [ DomainArg { ',' DomainArg } ] ')' ] .

Invocation    ::= [ '!' ] [ 'call' | 'return' | 'event' ] ( '_' | QualIdent )
                  [ '(' [ ArgList ] ')' ] [ '/' Arg ] .
ArgList       ::= Arg { ',' Arg } .

Type          ::= SimpleType { '[' { ',' } ']' } .
QualIdent     ::= Ident { '.' Ident } .
Literal       ::= String | Number | 'true' | 'false' | 'null' .
```

This grammar records the implemented authoring subset. The parser also accepts some legacy
compatibility forms that should not be authored; the [support matrix](cord-support.md)
classifies those as Conditional, Parsed-only, or Unsupported.

## Configurations and actions

Configs inherit with `config Derived : Base1, Base2`. Bases are resolved in order; later
bases and the derived config override earlier values. Names and switch keys are case-sensitive.

Prefer explicit action declarations. `action all T` is Conditional because a qualifier that
resolves no rules can expose the entire model rule set. Verify the explored action universe.
`public`, `internal`, `static`, and `abstract` are compatibility syntax. Do not author
`exclude`: exclusion semantics are not applied.

Only embedded action constraints (`where {. ... .}` / `where (. ... .)`) have effective
constraint semantics. Legacy parenthesized domain forms do not.

## Behavior precedence

From lowest to highest; each binary family is left-associative:

1. `||`, `|||`, `|?|`, `&`, `->`.
2. `|`.
3. `;`.
4. `*`, `+`, `?`, bounded repetition, then optional `: fail`.
5. Grouping, preconstraint, construct, `let`, `bind`, `...`, and invocation.

Parenthesize mixed operators even when precedence is known.

## Behavior operators

The table is an index. Use [Cord operator semantics](cord-operators.md) to calculate
accepted traces, empty-trace acceptance, parallel signatures, synchronization, and failure scope.

| Operator | Name | Meaning |
|---|---|---|
| `\|\|` | synchronized parallel | emitted labels advance both operands |
| `\|\|\|` | interleaved parallel | emitted labels advance one operand |
| `\|?\|` | sync-interleaved parallel | shared exact labels synchronize; others interleave |
| `&` | binary block permutation | lowers to `(A;B) \| (B;A)` |
| `->` | loose sequence | lowers to `A ; _* ; B` |
| `\|` | choice / union | either operand |
| `;` | tight sequence | second operand immediately after the first |
| `*` `+` `?` | repetition | zero-or-more / one-or-more / optional |
| `{n}` `{n,}` `{n,m}` `{n..m}` | bounded repetition | exactly / at-least / between |
| `_` | any action | one action from the current behavior alphabet |
| `...` | any sequence | zero or more alphabet actions (`_*`) |
| `!A` | negation | any bare alphabet action except atomic target `A` |
| `B : fail` | model-check failure | marks the endpoint as a failure in a model slice |

Parallel composition is Conditional: keep it at the current/root composition. A parallel node
nested under sequence, choice, repetition, permutation, or loose sequence can compile as empty.
`|||` also cannot reliably preserve two distinct same-label successors.

## Invocations and arguments

- Bare `Action` and `Action()` match any concrete arguments.
- A non-empty argument list pins supplied values; `_` is a per-argument wildcard.
- Scenario matching uses short labels, so keep those labels unique.
- `new T` is an ordinary action named for `T`; the model rule performs allocation.
- `Producer() / h; Consumer(h)` threads the model return while slicing. Generated/offline
  replay does not capture and thread a dynamic SUT return.

Config-level `action event` tags an event and generated tests call `Observe`, but current
`Observe` directly invokes the bound method. Behavior-level `call`, `return`, and `event`
qualifiers are Parsed-only for runtime semantics.

## `where` constraints

Inside a declared action's `where {. … .}` block:

| Constraint | Meaning |
|---|---|
| `Condition.In(p, v1, v2, …)` | parameter `p`'s candidate domain |
| `Condition.IsTrue(expr)` | boolean predicate (pruning); operators `== != < <= > >=`, `&& \|\| !`, `+ - * / %`, bitwise `& \|`; enum-qualified literals allowed |
| `Combination.Interaction(…)` | full product (default) |
| `Combination.Pairwise(…)` | minimal 2-wise cover |
| `Combination.Expand(…)` | adds rows to represent observed tuples |
| `Combination.Isolated(expr)` | retains an isolated satisfying row for a predicate |
| `Combination.Seeded(expr, …)` | adds a satisfying conjunction row when absent |

Pairwise can include derived expression columns. Complex primitive predicates may use a
Roslyn post-filter, but unrecognized expressions can be dropped; verify generated rows.

`Probability.IsTrue(p)` in an `if/else` unions values from both branches. `RandomSeed`
controls reproducible ordering for bounded generation; it does not randomly omit a branch.

## `bind`, `let`, and preconstraints

Top-level `bind Action(domain...) in Behavior` supports `_`, literals, `{set}`, integer
ranges, union `+`, `instances T`, and structured `Type(Field=domain, ...)`. A concrete bind
replaces the action's extracted constraints; it does not intersect them.

Bounded `let` assignments are lowered into a choice and substituted into complete invocation
arguments. Verify assignments are non-empty: a zero-row `let` can leave the unsubstituted body.

`{. Type.StaticMember = value; .}: Behavior` reflectively assigns public static model state.
This is Conditional because qualifier/conversion/set failures can be swallowed; prefer a
modeled initialization action.

## Constructs

| Construct | Current behavior |
|---|---|
| `construct model program from C` | Supported model exploration |
| `construct accepting paths for M` | model-backed acceptance pruning |
| `construct bounded exploration where PathDepth=n for M` | Conditional; depth only |
| `construct test cases where strategy=... for M` | Conditional short/long path strategy |
| `construct point shoot ... for M` | Conditional phased steering with a simple Boolean goal |
| `construct accept completion ... for M` | Conditional acceptance pruning; completer incomplete |
| `construct requirement coverage ... for M` | Conditional aggregate reporting, not directed selection |

Use constructs around model-backed machines. Pure behavior or inline construct targets can
collapse to empty; explore a pure behavior machine directly. A reached `: fail` state does not
force `sek explore` to return failure, so enforce the expected fail count externally.

## Switches

| Switch | Meaning |
|---|---|
| `StateBound` | max distinct states |
| `StepBound` | max transitions |
| `PathDepthBound` | max path depth from the initial state |
| `StopAtError` | stop expansion after the first reached model-check fail state |
| `RandomSeed` | order probabilistic branch unions reproducibly |

`TestEnabled`, `ForExploration`, and legacy generated-output/UI/view/timeout/expansion-limit
switches are Parsed-only or informational. Use CLI options for generated output and namespace.

## Scenario slicing and negative conformance

`Scenario || ModelProgram` restricts model exploration to permitted action sequences. Keep a
separate unsliced model machine when model-derived negative conformance is required: current
sliced exploration does not emit negative transitions. An action-level negative edge also does
not prove every individually invalid argument was tested when another argument enables the action.

## Related

- [Cord support matrix](cord-support.md)
- [Cord operator semantics](cord-operators.md)
- [The Cord language](../concepts/cord-language.md)
- [Writing Cord scenarios](../guides/writing-cord.md)
- [Parameter generation](../concepts/parameter-generation.md)
- [Operators sample](../samples/operators.md)
