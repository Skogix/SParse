# Skogix Parser - Revised Implementation Plan

**Based on Design Decisions from PR #6**

---

## Key Changes from Original Plan

### **1. Flat Resolution (Not Deep)**
- **Original**: Recursively expand `$id` → `$int*$unique` → `0*""`
- **Revised**: One-level expansion `$id` → `SProduct(SRef "int", SRef "unique")` STOP
- **Deep only for**: Type checking (optional later phase)

### **2. Output Format with Type Annotations**
- **Original**: `SNumber 5.5` → `"5.5"`
- **Revised**: `SNumber 5.5` → `"5.5$number"`
- All outputs show explicit types

### **3. Member Access as Chained Units**
- **Original**: Resolve each part independently
- **Revised**: Parse `$foo.bar.baz` as unit, carry context forward
- `baz` needs knowledge back to defining term `foo`

### **4. All 4 Registry Strategies**
1. JSON schema files
2. Inline Skogix syntax
3. F# built-ins
4. **NEW**: User-defined compositional types

### **5. Circular References**
- **Original**: Detect cycle and error
- **Revised**: Flat parse allows cycles, use max passes or flag for review

### **6. Actions as Stdout**
- Actions produce console output
- No arbitrary side effects
- Simple: `@print:$msg` → writes to stdout

---

## Simplified Architecture

```
Input → Tokenizer → Parser → Flat Resolver → Output Formatter
                                    ↓
                              Registry (4 sources)
```

---

## Phase 0: Foundation (START HERE) 🚀

### **Task 0.1: Create Module Files**
```bash
cd /home/user/SParse/Core
touch SkogixTokens.fs
touch SkogixLexer.fs
touch SkogixAST.fs
touch SkogixParser.fs
touch SkogixRegistry.fs
touch SkogixResolver.fs
touch SkogixOutput.fs    # NEW: Output formatter
touch Skogix.fs
```

### **Task 0.2: Update Core.fsproj**
Add new files to compilation order.

### **Task 0.3: Create Test Project**
```bash
dotnet new xunit -n Tests -lang F#
dotnet sln add Tests/Tests.fsproj
```

---

## Phase 1: Tokens & Lexer (Days 1-2)

### **Critical: Handle `-` Correctly**
```fsharp
// Tokenizer must distinguish:
"-5"      → TNumber (-5.0)
"->"      → TArrow
"- 5"     → TMinus, TNumber 5.0  (if we support binary minus)
```

### **Implementation**:
```fsharp
// In lexer, check ahead:
| '-' :: '>' :: rest -> tokenizeRec rest (TArrow :: acc)
| '-' :: c :: rest when Char.IsDigit c ->
    let (num, remaining) = parseNumber chars []
    tokenizeRec remaining (TNumber num :: acc)
| '-' :: rest -> tokenizeRec rest (TMinus :: acc)  // binary operator
```

---

## Phase 2: Parser (Days 3-5)

### **Left→Right, Top→Bottom, Inside→Out**

### **Member Access Chaining**
```fsharp
// Parse $foo.bar.baz as chain
let parseChain (state: ParserState) : ParseResult<SkogixValue> =
  match parseAtom state with
  | ParseOk (base, state') ->
      // Collect all .field accesses
      let rec collectFields acc st =
        match peek st with
        | TDot ->
            let st' = consume st
            match peek st' with
            | TIdentifier field ->
                collectFields (field :: acc) (consume st')
            | _ -> (List.rev acc, st)
        | _ -> (List.rev acc, st)

      let (fields, finalState) = collectFields [] state'
      // Build SMember chain
      let result = List.fold (fun obj field -> SMember(obj, field)) base fields
      ParseOk (result, finalState)
```

### **Grouped Member Access**
```
$foo.($bar)  or  $foo($bar)
→ Resolve $bar first, use result as field name
```

---

## Phase 3: Flat Resolver (Days 6-8)

### **One-Level Expansion**

```fsharp
let resolveFlatOnce (value: SkogixValue) (registry: Registry) : SkogixValue =
  match value with
  | SRef name ->
      match Map.tryFind name registry with
      | Some definition ->
          // Parse definition but DON'T recursively resolve
          let tokens = tokenize definition
          let state = initState tokens
          match parseExpression 0 state with
          | ParseOk (ast, _) -> ast  // ← STOP HERE (flat)
          | ParseError err -> failwith err
      | None -> SRef name  // Keep unresolved

  | SMember (obj, field) ->
      // Resolve object one level
      let objResolved = resolveFlatOnce obj registry
      match objResolved with
      | SObject map ->
          match Map.tryFind field map with
          | Some value -> value  // ← Return but don't resolve further
          | None -> failwith $"Field '{field}' not found"
      | _ -> SMember (objResolved, field)  // Keep symbolic

  | SProduct (l, r) ->
      SProduct (resolveFlatOnce l registry, resolveFlatOnce r registry)

  // ... similar for other operators (don't recurse deeply)
```

### **Deep Resolution (Optional, for Type Checking)**
```fsharp
let resolveDeep (value: SkogixValue) (registry: Registry) (maxDepth: int) : SkogixValue =
  let rec resolveRec v depth =
    if depth > maxDepth then v  // Stop at max depth
    else
      let onceResolved = resolveFlatOnce v registry
      if onceResolved = v then v  // No change, stop
      else resolveRec onceResolved (depth + 1)  // Continue

  resolveRec value 0
```

---

## Phase 4: Registry (Days 9-10)

### **Support All 4 Loading Strategies**

```fsharp
module SkogixRegistry

type Registry = Map<string, string>

/// 1. Built-in definitions
let builtIns : Registry =
  Map.ofList [
    ("int", "0")
    ("string", "\"\"")
    ("bool", "true")
    ("null", "null")
  ]

/// 2. Load from JSON schema
let loadFromJson (path: string) : Registry =
  // Parse JSON file into registry
  // ...

/// 3. Parse inline definitions
let parseInline (input: string) : Registry * string =
  // Parse lines like "$id = $int*$unique"
  // Return (definitions map, remaining expression)
  // ...

/// 4. User compositional types
type UserType = {
  name: string
  definition: string
}

let registerUserType (userType: UserType) (registry: Registry) : Registry =
  Map.add userType.name userType.definition registry

/// Merge with precedence (right overrides left)
let merge (left: Registry) (right: Registry) : Registry =
  Map.fold (fun acc key value -> Map.add key value acc) left right

/// Build full registry from all sources
let buildRegistry (jsonPath: string option) (inline: string option) (userTypes: UserType list) : Registry =
  let mutable reg = builtIns

  // Load JSON schema if provided
  match jsonPath with
  | Some path -> reg <- merge reg (loadFromJson path)
  | None -> ()

  // Parse inline definitions if provided
  match inline with
  | Some defs ->
      let (inlineReg, _) = parseInline defs
      reg <- merge reg inlineReg
  | None -> ()

  // Add user types
  for userType in userTypes do
    reg <- registerUserType userType reg

  reg
```

---

## Phase 5: Output Formatter (Days 11-12)

### **Type Annotations in Output**

```fsharp
module SkogixOutput

open SkogixAST

/// Format value with type annotation
let rec formatWithType (value: SkogixValue) : string =
  match value with
  | SNull -> "null$null"
  | SBool b -> $"{b}$bool"
  | SNumber n -> $"{n}$number"
  | SString s -> $"\"{s}\"$string"
  | SRef name -> $"{name}$ref"
  | SAction name -> $"@{name}$action"
  | SExistence -> "_$existence"

  | SMember (obj, field) ->
      $"{formatWithType obj}.{field}$member"

  | SProduct (l, r) ->
      $"{formatWithType l}*{formatWithType r}$product"

  | SArray values ->
      let formatted = values |> List.map formatWithType |> String.concat ", "
      $"[{formatted}]$array"

  | SObject map ->
      let formatted =
        map
        |> Map.toList
        |> List.map (fun (k, v) -> $"\"{k}\": {formatWithType v}")
        |> String.concat ", "
      $"{{{formatted}}}$object"

  // ... etc for other types
```

**Example Output**:
```
Input:  $id
Parsed: SProduct(SRef "int", SRef "unique")
Output: "int$ref*unique$ref$product"
```

---

## Phase 6: Circular References (Day 13)

### **Max Passes Strategy**

```fsharp
let resolveWithMaxPasses (value: SkogixValue) (registry: Registry) (maxPasses: int) =
  let rec resolveRec v (visited: Set<string>) pass =
    if pass > maxPasses then
      // Flag for manual review
      printfn "Warning: Max passes exceeded, possible circular reference"
      v
    else
      match v with
      | SRef name when Set.contains name visited ->
          printfn $"Warning: Circular reference detected: {name}"
          SRef name  // Keep symbolic
      | SRef name ->
          let newVisited = Set.add name visited
          resolveFlatOnce v registry |> fun resolved ->
            resolveRec resolved newVisited (pass + 1)
      | _ -> resolveFlatOnce v registry

  resolveRec value Set.empty 0
```

---

## Phase 7: Public API (Day 14)

```fsharp
module Core.Skogix

/// Parse with flat resolution
let parse (input: string) (registry: Registry option) : SkogixValue =
  let reg = defaultArg registry SkogixRegistry.builtIns
  let tokens = SkogixLexer.tokenize input
  let state = SkogixParser.initState tokens
  match SkogixParser.parseExpression 0 state with
  | ParseOk (ast, _) ->
      SkogixResolver.resolveFlatOnce ast reg
  | ParseError err ->
      failwith $"Parse error: {err}"

/// Parse and format output with type annotations
let parseAndFormat (input: string) (registry: Registry option) : string =
  let result = parse input registry
  SkogixOutput.formatWithType result

/// Pretty print
let printParse (input: string) (registry: Registry option) : unit =
  try
    let formatted = parseAndFormat input registry
    printfn "%s" formatted
  with ex ->
    printfn "Error: %s" ex.Message
```

---

## Testing Strategy

### **Phase by Phase Tests**

```fsharp
// Tests/LexerTests.fs
[<Fact>]
let ``Tokenize negative number vs arrow`` () =
  Assert.Equal([TNumber -5.0; TEOF], tokenize "-5")
  Assert.Equal([TArrow; TEOF], tokenize "->")

// Tests/ParserTests.fs
[<Fact>]
let ``Parse member chain`` () =
  let result = parse "$foo.bar.baz"
  // Should be: SMember(SMember(SRef "foo", "bar"), "baz")
  Assert.Equal(expected, result)

// Tests/ResolverTests.fs
[<Fact>]
let ``Flat resolve one level`` () =
  let registry = Map.ofList [("id", "$int*$unique")]
  let result = resolveFlatOnce (SRef "id") registry
  // Should be: SProduct(SRef "int", SRef "unique") - NOT resolved further
  Assert.Equal(SProduct(SRef "int", SRef "unique"), result)

// Tests/OutputTests.fs
[<Fact>]
let ``Format with type annotations`` () =
  let value = SNumber 5.5
  Assert.Equal("5.5$number", formatWithType value)
```

---

## Timeline

| Phase | Duration | Deliverable |
|-------|----------|-------------|
| 0: Foundation | 1 day | Module structure |
| 1: Tokenizer | 2 days | Tokens with `-` handling |
| 2: Parser | 3 days | Left→right chaining |
| 3: Flat Resolver | 3 days | One-level expansion |
| 4: Registry | 2 days | 4 loading strategies |
| 5: Output Formatter | 2 days | Type annotations |
| 6: Circular Refs | 1 day | Max passes |
| 7: Public API | 1 day | Complete interface |
| **Total** | **15 days** | **Working parser** |

---

## Immediate Next Steps

1. ✅ **Commit DESIGN_DECISIONS.md**
2. ✅ **Commit REVISED_PLAN.md**
3. 🚀 **Start Phase 0**: Create module files
4. 🚀 **Start Phase 1**: Build tokenizer

**Ready to start Phase 0?**
