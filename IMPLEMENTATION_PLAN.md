# Skogix Notation - Implementation Work Plan

**Updated**: Includes Resolution & Expansion Model

---

## Architecture Overview

```
┌─────────────────┐
│  Input String   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Tokenizer     │  ← Break into tokens
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Parser         │  ← Build AST
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Resolver       │  ← NEW: Expand references
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Resolved Value  │  ← Final output
└─────────────────┘
```

---

## Module Structure

```
SParse/
├── Core/
│   ├── Common.fs                 # Existing: Parser combinators
│   ├── SParse.fs                 # Existing: JSON parser
│   ├── SkogixTokens.fs          # NEW: Token types
│   ├── SkogixLexer.fs           # NEW: Tokenization
│   ├── SkogixAST.fs             # NEW: AST types
│   ├── SkogixParser.fs          # NEW: Parser (tokens → AST)
│   ├── SkogixRegistry.fs        # NEW: Definition registry
│   ├── SkogixResolver.fs        # NEW: Resolution engine
│   └── Skogix.fs                # NEW: Public API
├── Console/
│   └── Program.fs                # Update: Use new parser
└── Tests/                        # NEW: Test project
    ├── Tests.fsproj
    ├── LexerTests.fs
    ├── ParserTests.fs
    └── ResolverTests.fs
```

---

## Implementation Phases

### **Phase 0: Foundation (Days 1-2)**

#### **Task 0.1: Create New Module Files**
```bash
touch Core/SkogixTokens.fs
touch Core/SkogixLexer.fs
touch Core/SkogixAST.fs
touch Core/SkogixParser.fs
touch Core/SkogixRegistry.fs
touch Core/SkogixResolver.fs
touch Core/Skogix.fs
```

#### **Task 0.2: Update Core.fsproj**
```xml
<ItemGroup>
  <Compile Include="Common.fs" />
  <Compile Include="SParse.fs" />
  <!-- New Skogix files -->
  <Compile Include="SkogixTokens.fs" />
  <Compile Include="SkogixLexer.fs" />
  <Compile Include="SkogixAST.fs" />
  <Compile Include="SkogixParser.fs" />
  <Compile Include="SkogixRegistry.fs" />
  <Compile Include="SkogixResolver.fs" />
  <Compile Include="Skogix.fs" />
</ItemGroup>
```

#### **Task 0.3: Create Test Project**
```bash
dotnet new xunit -n Tests -lang F#
dotnet sln add Tests/Tests.fsproj
cd Tests
dotnet add reference ../Core/Core.fsproj
```

---

### **Phase 1: Token System (Days 3-4)**

#### **Task 1.1: Define Token Types** (`SkogixTokens.fs`)

```fsharp
module Core.SkogixTokens

/// Token types for Skogix notation
type Token =
  // Operators
  | TDollar                       // $
  | TAt                           // @
  | TPipe                         // |
  | TUnderscore                   // _
  | TDot                          // .
  | TColon                        // :
  | TEquals                       // =
  | TNotEquals                    // !=
  | TArrow                        // ->
  | TStar                         // *

  // Brackets
  | TLeftBracket                  // [
  | TRightBracket                 // ]
  | TLeftBrace                    // {
  | TRightBrace                   // }

  // Separators
  | TComma                        // ,

  // Literals
  | TIdentifier of string         // id, name, entity, etc.
  | TNumber of float              // 0, 42, 3.14, -5
  | TString of string             // "hello", ""
  | TNull                         // null
  | TBool of bool                 // true, false

  // End of input
  | TEOF

/// Pretty print token for debugging
let tokenToString (token: Token) : string =
  match token with
  | TDollar -> "$"
  | TAt -> "@"
  | TPipe -> "|"
  | TUnderscore -> "_"
  | TDot -> "."
  | TColon -> ":"
  | TEquals -> "="
  | TNotEquals -> "!="
  | TArrow -> "->"
  | TStar -> "*"
  | TLeftBracket -> "["
  | TRightBracket -> "]"
  | TLeftBrace -> "{"
  | TRightBrace -> "}"
  | TComma -> ","
  | TIdentifier s -> $"ID({s})"
  | TNumber n -> $"NUM({n})"
  | TString s -> $"STR(\"{s}\")"
  | TNull -> "null"
  | TBool b -> string b
  | TEOF -> "EOF"
```

**Test Cases**:
```fsharp
// Tests/LexerTests.fs
[<Fact>]
let ``Token toString works`` () =
  Assert.Equal("$", tokenToString TDollar)
  Assert.Equal("ID(test)", tokenToString (TIdentifier "test"))
```

---

#### **Task 1.2: Build Tokenizer** (`SkogixLexer.fs`)

```fsharp
module Core.SkogixLexer

open System
open Core.SkogixTokens

/// Tokenize input string into list of tokens
let tokenize (input: string) : Token list =
  let rec tokenizeRec (chars: char list) (acc: Token list) : Token list =
    match chars with
    | [] -> List.rev (TEOF :: acc)

    // Whitespace (skip)
    | c :: rest when Char.IsWhiteSpace c ->
        tokenizeRec rest acc

    // Two-character operators
    | '!' :: '=' :: rest ->
        tokenizeRec rest (TNotEquals :: acc)
    | '-' :: '>' :: rest ->
        tokenizeRec rest (TArrow :: acc)

    // Single-character operators
    | '$' :: rest -> tokenizeRec rest (TDollar :: acc)
    | '@' :: rest -> tokenizeRec rest (TAt :: acc)
    | '|' :: rest -> tokenizeRec rest (TPipe :: acc)
    | '_' :: rest -> tokenizeRec rest (TUnderscore :: acc)
    | '.' :: rest -> tokenizeRec rest (TDot :: acc)
    | ':' :: rest -> tokenizeRec rest (TColon :: acc)
    | '=' :: rest -> tokenizeRec rest (TEquals :: acc)
    | '*' :: rest -> tokenizeRec rest (TStar :: acc)
    | '[' :: rest -> tokenizeRec rest (TLeftBracket :: acc)
    | ']' :: rest -> tokenizeRec rest (TRightBracket :: acc)
    | '{' :: rest -> tokenizeRec rest (TLeftBrace :: acc)
    | '}' :: rest -> tokenizeRec rest (TRightBrace :: acc)
    | ',' :: rest -> tokenizeRec rest (TComma :: acc)

    // String literals
    | '\"' :: rest ->
        let (str, remaining) = parseString rest []
        tokenizeRec remaining (TString str :: acc)

    // Numbers
    | c :: rest when Char.IsDigit c || c = '-' ->
        let (num, remaining) = parseNumber chars []
        tokenizeRec remaining (TNumber num :: acc)

    // Identifiers or keywords
    | c :: rest when Char.IsLetter c ->
        let (id, remaining) = parseIdentifier chars []
        let token =
          match id with
          | "null" -> TNull
          | "true" -> TBool true
          | "false" -> TBool false
          | _ -> TIdentifier id
        tokenizeRec remaining (token :: acc)

    | c :: rest ->
        failwith $"Unexpected character: '{c}'"

  and parseString (chars: char list) (acc: char list) : string * char list =
    match chars with
    | '\"' :: rest -> (String(List.rev acc |> List.toArray), rest)
    | '\\' :: '\"' :: rest -> parseString rest ('\"' :: acc)
    | '\\' :: '\\' :: rest -> parseString rest ('\\' :: acc)
    | '\\' :: 'n' :: rest -> parseString rest ('\n' :: acc)
    | '\\' :: 't' :: rest -> parseString rest ('\t' :: acc)
    | c :: rest -> parseString rest (c :: acc)
    | [] -> failwith "Unterminated string"

  and parseNumber (chars: char list) (acc: char list) : float * char list =
    match chars with
    | c :: rest when Char.IsDigit c || c = '.' || c = '-' ->
        parseNumber rest (c :: acc)
    | rest ->
        let numStr = String(List.rev acc |> List.toArray)
        (float numStr, rest)

  and parseIdentifier (chars: char list) (acc: char list) : string * char list =
    match chars with
    | c :: rest when Char.IsLetterOrDigit c || c = '_' ->
        parseIdentifier rest (c :: acc)
    | rest ->
        let id = String(List.rev acc |> List.toArray)
        (id, rest)

  tokenizeRec (List.ofSeq input) []
```

**Test Cases**:
```fsharp
[<Fact>]
let ``Tokenize simple reference`` () =
  let tokens = tokenize "$id"
  Assert.Equal([TDollar; TIdentifier "id"; TEOF], tokens)

[<Fact>]
let ``Tokenize member access`` () =
  let tokens = tokenize "$entity.id"
  Assert.Equal([TDollar; TIdentifier "entity"; TDot; TIdentifier "id"; TEOF], tokens)

[<Fact>]
let ``Tokenize product`` () =
  let tokens = tokenize "$int*$unique"
  Assert.Equal([
    TDollar; TIdentifier "int"; TStar; TDollar; TIdentifier "unique"; TEOF
  ], tokens)
```

---

### **Phase 2: AST Definition (Day 5)**

#### **Task 2.1: Define AST Types** (`SkogixAST.fs`)

```fsharp
module Core.SkogixAST

/// Abstract Syntax Tree for Skogix notation
type SkogixValue =
  // Primitives
  | SNull
  | SBool of bool
  | SNumber of float
  | SString of string

  // References and Actions
  | SRef of string                          // $id
  | SAction of string                       // @action
  | SExistence                              // _

  // Operators
  | SMember of SkogixValue * string         // $entity.id
  | SType of SkogixValue * SkogixValue      // $id:$int
  | SBind of SkogixValue * SkogixValue      // $id@$action
  | SProduct of SkogixValue * SkogixValue   // $a*$b
  | SMorphism of SkogixValue * SkogixValue  // $a->$b
  | SChoice of SkogixValue * SkogixValue    // $a|$b
  | SEquality of SkogixValue * SkogixValue  // $a=$b
  | SInequality of SkogixValue * SkogixValue// $a!=$b

  // Structures
  | SArray of SkogixValue list              // [1, 2, 3]
  | SObject of Map<string, SkogixValue>     // {"key": "value"}
  | SSimilarity of SkogixValue              // [$expr]
  | SDifference of SkogixValue              // {$expr}

  // Legacy
  | SCommand of string * SkogixValue option // foo "arg"

/// Pretty print AST for debugging
let rec astToString (value: SkogixValue) : string =
  match value with
  | SNull -> "null"
  | SBool b -> string b
  | SNumber n -> string n
  | SString s -> $"\"{s}\""
  | SRef name -> $"${name}"
  | SAction name -> $"@{name}"
  | SExistence -> "_"
  | SMember (obj, field) -> $"{astToString obj}.{field}"
  | SType (value, typ) -> $"{astToString value}:{astToString typ}"
  | SBind (left, right) -> $"{astToString left}@{astToString right}"
  | SProduct (left, right) -> $"{astToString left}*{astToString right}"
  | SMorphism (left, right) -> $"{astToString left}->{astToString right}"
  | SChoice (left, right) -> $"{astToString left}|{astToString right}"
  | SEquality (left, right) -> $"{astToString left}={astToString right}"
  | SInequality (left, right) -> $"{astToString left}!={astToString right}"
  | SArray values -> $"[{String.Join(\", \", List.map astToString values)}]"
  | SObject map -> $"{{{String.Join(\", \", Map.toList map |> List.map (fun (k, v) -> $"\"{k}\": {astToString v}"))}}}"
  | SSimilarity expr -> $"[{astToString expr}]"
  | SDifference expr -> $"{{{astToString expr}}}"
  | SCommand (name, arg) ->
      match arg with
      | Some a -> $"{name} {astToString a}"
      | None -> name
```

---

### **Phase 3: Parser (Days 6-10)**

#### **Task 3.1: Parser Framework** (`SkogixParser.fs`)

```fsharp
module Core.SkogixParser

open Core.SkogixTokens
open Core.SkogixAST

/// Parser state
type ParserState = {
  tokens: Token list
  position: int
}

/// Parser result
type ParseResult<'T> =
  | ParseOk of 'T * ParserState
  | ParseError of string

/// Create initial parser state
let initState (tokens: Token list) : ParserState =
  { tokens = tokens; position = 0 }

/// Peek at current token
let peek (state: ParserState) : Token =
  if state.position < state.tokens.Length then
    state.tokens.[state.position]
  else
    TEOF

/// Consume current token and advance
let consume (state: ParserState) : ParserState =
  { state with position = state.position + 1 }

/// Expect specific token
let expect (expected: Token) (state: ParserState) : ParseResult<unit> =
  let current = peek state
  if current = expected then
    ParseOk ((), consume state)
  else
    ParseError $"Expected {tokenToString expected}, got {tokenToString current}"

// ... parser functions to be implemented
```

#### **Task 3.2: Atom Parsers**

```fsharp
/// Parse atomic values (primitives, references, actions)
let rec parseAtom (state: ParserState) : ParseResult<SkogixValue> =
  match peek state with
  | TNull -> ParseOk (SNull, consume state)
  | TBool b -> ParseOk (SBool b, consume state)
  | TNumber n -> ParseOk (SNumber n, consume state)
  | TString s -> ParseOk (SString s, consume state)
  | TUnderscore -> ParseOk (SExistence, consume state)

  | TDollar ->
      let state' = consume state
      match peek state' with
      | TIdentifier name -> ParseOk (SRef name, consume state')
      | _ -> ParseError "Expected identifier after $"

  | TAt ->
      let state' = consume state
      match peek state' with
      | TIdentifier name -> ParseOk (SAction name, consume state')
      | _ -> ParseError "Expected identifier after @"

  | TLeftBracket -> parseArrayOrSimilarity state
  | TLeftBrace -> parseObjectOrDifference state

  | t -> ParseError $"Unexpected token in atom: {tokenToString t}"
```

#### **Task 3.3: Operator Precedence Parser**

```fsharp
/// Operator precedence (higher = tighter binding)
let precedence (token: Token) : int =
  match token with
  | TDot -> 8        // $a.$b
  | TColon -> 7      // $a:$b
  | TAt -> 6         // $a@$b
  | TStar -> 5       // $a*$b
  | TArrow -> 4      // $a->$b
  | TPipe -> 3       // $a|$b
  | TEquals -> 2     // $a=$b
  | TNotEquals -> 2  // $a!=$b
  | _ -> 0

/// Parse expression with precedence climbing
let rec parseExpression (minPrec: int) (state: ParserState) : ParseResult<SkogixValue> =
  match parseAtom state with
  | ParseError err -> ParseError err
  | ParseOk (lhs, state') ->
      parseInfixRhs lhs minPrec state'

and parseInfixRhs (lhs: SkogixValue) (minPrec: int) (state: ParserState) : ParseResult<SkogixValue> =
  let op = peek state
  let prec = precedence op

  if prec >= minPrec then
    let state' = consume state
    match parseExpression (prec + 1) state' with
    | ParseError err -> ParseError err
    | ParseOk (rhs, state'') ->
        let combined = combineOperator op lhs rhs
        parseInfixRhs combined minPrec state''
  else
    ParseOk (lhs, state)

and combineOperator (op: Token) (lhs: SkogixValue) (rhs: SkogixValue) : SkogixValue =
  match op with
  | TDot ->
      match rhs with
      | SRef field -> SMember (lhs, field)
      | _ -> failwith "Right side of . must be identifier"
  | TColon -> SType (lhs, rhs)
  | TAt -> SBind (lhs, rhs)
  | TStar -> SProduct (lhs, rhs)
  | TArrow -> SMorphism (lhs, rhs)
  | TPipe -> SChoice (lhs, rhs)
  | TEquals -> SEquality (lhs, rhs)
  | TNotEquals -> SInequality (lhs, rhs)
  | _ -> failwith $"Not an infix operator: {tokenToString op}"
```

**Test Cases**:
```fsharp
[<Fact>]
let ``Parse simple reference`` () =
  let tokens = tokenize "$id"
  let state = initState tokens
  match parseExpression 0 state with
  | ParseOk (value, _) -> Assert.Equal(SRef "id", value)
  | ParseError err -> Assert.Fail(err)

[<Fact>]
let ``Parse product`` () =
  let tokens = tokenize "$int*$unique"
  let state = initState tokens
  match parseExpression 0 state with
  | ParseOk (value, _) ->
      Assert.Equal(SProduct(SRef "int", SRef "unique"), value)
  | ParseError err -> Assert.Fail(err)
```

---

### **Phase 4: Definition Registry (Days 11-12)**

#### **Task 4.1: Registry Types** (`SkogixRegistry.fs`)

```fsharp
module Core.SkogixRegistry

open System.Collections.Generic

/// Definition registry (maps names to definitions)
type DefinitionRegistry = Map<string, string>

/// Create empty registry
let empty : DefinitionRegistry = Map.empty

/// Add definition
let add (name: string) (definition: string) (registry: DefinitionRegistry) : DefinitionRegistry =
  Map.add name definition registry

/// Lookup definition
let tryFind (name: string) (registry: DefinitionRegistry) : string option =
  Map.tryFind name registry

/// Built-in definitions
let builtIns : DefinitionRegistry =
  Map.ofList [
    ("int", "0")
    ("string", "\"\"")
    ("bool", "true")
    ("null", "null")
    ("list", "[]")
    ("object", "{}")
  ]

/// Load from JSON file
let loadFromJson (path: string) : DefinitionRegistry =
  // TODO: Implement JSON loading
  empty

/// Merge registries (right takes precedence)
let merge (left: DefinitionRegistry) (right: DefinitionRegistry) : DefinitionRegistry =
  Map.fold (fun acc key value -> Map.add key value acc) right left
```

---

### **Phase 5: Resolution Engine (Days 13-16)**

#### **Task 5.1: Resolver** (`SkogixResolver.fs`)

```fsharp
module Core.SkogixResolver

open Core.SkogixAST
open Core.SkogixRegistry
open Core.SkogixLexer
open Core.SkogixParser

/// Maximum recursion depth
let maxDepth = 100

/// Resolve a value by expanding references
let rec resolve (value: SkogixValue) (registry: DefinitionRegistry) (depth: int) : SkogixValue =
  // Check depth limit
  if depth > maxDepth then
    failwith $"Maximum resolution depth {maxDepth} exceeded (possible circular reference)"

  match value with
  // Base cases - stop here
  | SNull | SBool _ | SNumber _ | SString _ | SExistence ->
      value

  // References - lookup and expand
  | SRef name ->
      match tryFind name registry with
      | Some definition ->
          // Parse definition
          let tokens = tokenize definition
          let state = initState tokens
          match parseExpression 0 state with
          | ParseOk (ast, _) ->
              // Recursively resolve
              resolve ast registry (depth + 1)
          | ParseError err ->
              failwith $"Error parsing definition of ${name}: {err}"
      | None ->
          failwith $"Undefined reference: ${name}"

  // Actions - similar to references
  | SAction name ->
      match tryFind name registry with
      | Some definition ->
          let tokens = tokenize definition
          let state = initState tokens
          match parseExpression 0 state with
          | ParseOk (ast, _) -> resolve ast registry (depth + 1)
          | ParseError err -> failwith $"Error parsing definition of @{name}: {err}"
      | None ->
          failwith $"Undefined action: @{name}"

  // Member access
  | SMember (obj, field) ->
      let objResolved = resolve obj registry (depth + 1)
      match objResolved with
      | SObject map ->
          match Map.tryFind field map with
          | Some value -> resolve value registry (depth + 1)
          | None -> failwith $"Field '{field}' not found"
      | _ -> failwith $"Cannot access field '{field}' on non-object"

  // Compound operators - resolve both sides
  | SProduct (left, right) ->
      SProduct (resolve left registry (depth + 1), resolve right registry (depth + 1))
  | SMorphism (left, right) ->
      SMorphism (resolve left registry (depth + 1), resolve right registry (depth + 1))
  | SChoice (left, right) ->
      SChoice (resolve left registry (depth + 1), resolve right registry (depth + 1))
  | SEquality (left, right) ->
      SEquality (resolve left registry (depth + 1), resolve right registry (depth + 1))
  | SInequality (left, right) ->
      SInequality (resolve left registry (depth + 1), resolve right registry (depth + 1))
  | SType (value, typ) ->
      SType (resolve value registry (depth + 1), resolve typ registry (depth + 1))
  | SBind (left, right) ->
      SBind (resolve left registry (depth + 1), resolve right registry (depth + 1))

  // Arrays and objects
  | SArray values ->
      SArray (List.map (fun v -> resolve v registry (depth + 1)) values)
  | SObject map ->
      SObject (Map.map (fun _ v -> resolve v registry (depth + 1)) map)

  // Bracketed structures
  | SSimilarity expr ->
      SSimilarity (resolve expr registry (depth + 1))
  | SDifference expr ->
      SDifference (resolve expr registry (depth + 1))

  | SCommand _ -> value  // Legacy - don't resolve

/// Resolve from string input
let resolveString (input: string) (registry: DefinitionRegistry) : SkogixValue =
  let tokens = tokenize input
  let state = initState tokens
  match parseExpression 0 state with
  | ParseOk (ast, _) -> resolve ast registry 0
  | ParseError err -> failwith $"Parse error: {err}"
```

**Test Cases**:
```fsharp
[<Fact>]
let ``Resolve simple chain`` () =
  let registry = Map.ofList [
    ("id", "$int*$unique")
    ("int", "0")
    ("unique", "$string")
    ("string", "\"\"")
  ]
  let result = resolveString "$id" registry
  Assert.Equal(SProduct(SNumber 0.0, SString ""), result)

[<Fact>]
let ``Resolve member access`` () =
  let registry = Map.ofList [
    ("entity", "{\"id\": 0, \"gen\": 1}")
  ]
  let result = resolveString "$entity.id" registry
  Assert.Equal(SNumber 0.0, result)
```

---

### **Phase 6: Public API (Day 17)**

#### **Task 6.1: Main Entry Point** (`Skogix.fs`)

```fsharp
module Core.Skogix

open Core.SkogixRegistry
open Core.SkogixResolver
open Core.SkogixAST

/// Parse and resolve Skogix notation
let parse (input: string) (registry: DefinitionRegistry option) : SkogixValue =
  let reg = defaultArg registry builtIns
  resolveString input reg

/// Parse with custom registry
let parseWith (input: string) (userDefs: (string * string) list) : SkogixValue =
  let registry = merge builtIns (Map.ofList userDefs)
  resolveString input registry

/// Pretty print result
let printParse (input: string) (registry: DefinitionRegistry option) : unit =
  try
    let result = parse input registry
    printfn "%s" (astToString result)
  with ex ->
    printfn "Error: %s" ex.Message
```

---

### **Phase 7: Integration & Testing (Days 18-20)**

#### **Task 7.1: Update Console App**

```fsharp
// Console/Program.fs
open Core.Skogix
open Core.SkogixRegistry

// Load definitions
let registry = Map.ofList [
  ("id", "$int*$unique")
  ("int", "0")
  ("unique", "$string")
  ("string", "\"\"")
  ("entity", "{\"id\": $id, \"gen\": $int}")
  ("eid", "$entity.id*$entity.gen")
]

// Test cases
printParse "$id" (Some registry)
printParse "$entity.id" (Some registry)
printParse "$eid" (Some registry)

// REPL
let mutable running = true
while running do
    printf "Skogix> "
    let input = Console.ReadLine()
    if input = "exit" then
        running <- false
    else
        printParse input (Some registry)
```

#### **Task 7.2: Comprehensive Tests**

See `Tests/` folder for full test suite.

---

## Summary Timeline

| Phase | Duration | Deliverable |
|-------|----------|-------------|
| 0: Foundation | 2 days | Module structure |
| 1: Tokens | 2 days | Tokenizer working |
| 2: AST | 1 day | Type definitions |
| 3: Parser | 5 days | Token → AST |
| 4: Registry | 2 days | Definition storage |
| 5: Resolver | 4 days | AST → Resolved value |
| 6: API | 1 day | Public interface |
| 7: Integration | 3 days | Tests + Console app |
| **Total** | **20 days** | **Full implementation** |

---

## Next Steps

1. **Fill out `DESIGN_QUESTIONS.md`** - Critical design decisions
2. **Review `RESOLUTION_MODEL.md`** - Understand expansion strategy
3. **Approve this plan** or suggest modifications
4. **Start Phase 0** - Set up module structure

Ready to begin implementation when you provide design answers!
