# Skogix Notation - Design Decisions Summary

**Based on PR Review #6**: https://github.com/Skogix/SParse/pull/6#pullrequestreview-3379266131

---

## Core Parsing Strategy

### **Direction**: Left→Right, Top→Bottom, Inside→Out
```
$foo.bar.baz
└─ Parse left to right
   1. $foo
   2. .bar
   3. .baz
```

### **Resolution Depth**: Flat, Not Deep
- **Flat resolution**: Parse at surface level, don't recursively expand everything
- **Deep resolution only when**: Type-checking is required
- **Early exit**: On objects/named types (don't expand further)

**Example**:
```
Input: $id
Registry: $id = $int*$unique

Flat Parse:
$id → SProduct(SRef "int", SRef "unique")  ← STOP

Deep Parse (only if type-checking):
$id → SProduct(SRef "int", SRef "unique")
   → SProduct(SNumber 0.0, SString "")  ← Continue to base cases
```

---

## Storage Format

### **Values**: Key-Value Pairs
```
"id" → "$int*$unique"
"int" → "0"
"unique" → "$string"
"string" → "\"\""
```

### **Objects**: JSON with Type + Value + Optional Name
```json
{
  "type": "entity",
  "value": {
    "id": "$id",
    "gen": "$int"
  },
  "name": "User"  // optional
}
```

---

## Output Format

### **Internal vs External Representation**

**Internal** (AST):
```fsharp
SRef "foo"
SNumber 5.5
```

**External** (Output string):
```
foo$string
5.5$number
```

**Output shows types explicitly** - append type to value when printing.

**Implementation**:
```fsharp
let rec astToOutputString (value: SkogixValue) : string =
  match value with
  | SRef name -> $"{name}$ref"
  | SNumber n -> $"{n}$number"
  | SString s -> $"\"{s}\"$string"
  | SBool b -> $"{b}$bool"
  | SNull -> "null$null"
  // etc.
```

---

## Member Access Semantics

### **Chained Access**: Parse as Unit
```
$foo.bar.baz
```

**Key insight**: `baz` needs knowledge back to defining term `foo`
- Don't resolve each part independently
- Resolve as a chain, carrying context forward

**Implementation**:
```fsharp
// Parse $foo.bar.baz as:
let resolveChain (parts: string list) (registry: Registry) =
  // Start with $foo
  let base = resolve (SRef parts.[0]) registry
  // Access .bar on result
  let withBar = accessField base parts.[1]
  // Access .baz on result
  let withBaz = accessField withBar parts.[2]
  withBaz
```

### **Grouped Access**: Resolve Inner First
```
$foo.(&bar)    // or $foo($bar)
```

**With grouping**:
1. Resolve `$bar` first
2. Then infer `.` as relationship operator
3. Apply to `$foo`

**Order changes**:
- `$foo.bar` → bar is literal field name
- `$foo.($bar)` → bar is resolved first, result used as field

---

## Circular Reference Handling

### **Strategy**: Flat Parse + Max Passes

**Approach**:
- Flat parse allows cycles to persist until explicitly resolved
- Use **max passes** or **flag for manual review**

**Example**:
```
$a = $b
$b = $a

Pass 1: $a → SRef "b"  (keep symbolic)
Pass 2: $b → SRef "a"  (cycle detected)
Result: Flag for manual review OR keep symbolic
```

**Implementation**:
```fsharp
let resolveWithMaxPasses (value: SkogixValue) (registry: Registry) (maxPasses: int) =
  let rec resolveRec (v: SkogixValue) (visited: Set<string>) (pass: int) =
    if pass > maxPasses then
      // Flag for manual review
      v  // Keep as symbolic reference
    else
      match v with
      | SRef name when Set.contains name visited ->
          // Cycle detected - keep symbolic
          SRef name
      | SRef name ->
          let newVisited = Set.add name visited
          match Map.tryFind name registry with
          | Some def -> resolveRec (parse def) newVisited (pass + 1)
          | None -> v
      | _ -> v
  resolveRec value Set.empty 0
```

---

## Registry Loading Strategies

### **Use All Three** (Flexible)

#### **1. JSON Schema Files**
```json
{
  "definitions": {
    "$id": "$int*$unique",
    "$int": "0"
  }
}
```

#### **2. Inline Skogix Syntax**
```
$id = $int*$unique
$int = 0

// Then evaluate:
$id
```

#### **3. F# Code (Built-ins)**
```fsharp
let builtIns = Map.ofList [
  ("int", "0")
  ("string", "\"\"")
  ("bool", "true")
]
```

#### **4. User-Defined Compositional Types** ⭐ NEW
```
// Named groupings (not strict types)
type Entity = {
  id: $id,
  gen: $int
}

// Use:
$myEntity:Entity
```

**Implementation**: Support all 4, merge registries with precedence:
```fsharp
let fullRegistry =
  builtIns
  |> merge schemaFromFile
  |> merge inlineDefinitions
  |> merge userCompositions
```

---

## Open Questions - RESOLVED

### **Q1: Partial Expansion**
✅ **Answer**: Early exit on objects/named types
- Don't expand objects fully
- Stop at named type boundaries

**Example**:
```
$entity → SObject {...}  ← STOP (don't expand object fields)
$id:Entity → SRef "id" with type Entity  ← STOP (named type)
```

### **Q2: Actions / Side Effects**
✅ **Answer**: Replace with stdout
- Actions produce output (print to console)
- Don't execute arbitrary side effects
- Keep it simple: actions = output

**Example**:
```
@print:$message → writes to stdout
@log:$data → writes to stdout
```

### **Q3: Type Checking**
✅ **Answer**: Optional, open to suggestions
- Not required initially
- Can add later if needed
- Focus on parsing/resolution first

### **Q4: Caching**
✅ **Answer**: Not initially
- Don't cache resolved values in first version
- Pure resolution (re-resolve each time)
- Can optimize later if performance issue

---

## Copilot Bugs Caught

### **1. Tokenization - Minus Sign Handling**
```fsharp
// Issue: Negative numbers vs arrow operator
"-5" vs "->"

// Solution: Check ahead
| '-' :: '>' :: rest -> TArrow
| '-' :: rest when Char.IsDigit (peek rest) -> parseNumber
```

### **2. Member Access Parsing Order**
```fsharp
// Issue: $foo.bar - which parses first?

// Solution: Left-to-right, parse $foo, then .bar
parseExpression should handle postfix operators correctly
```

### **3. Merge Parameter Precedence**
```fsharp
// Issue: Which registry takes precedence?
let merge (left: Registry) (right: Registry) : Registry

// Fixed in commit: right takes precedence
Map.fold (fun acc key value -> Map.add key value acc) right left
// Changed to:
Map.fold (fun acc key value -> Map.add key value acc) left right
```

---

## Implementation Priorities

Based on decisions above, prioritize:

1. **Phase 1**: Tokenizer (handle `-` correctly)
2. **Phase 2**: Parser (left→right, member access chains)
3. **Phase 3**: Flat resolution (one-level expansion)
4. **Phase 4**: Registry loading (all 4 strategies)
5. **Phase 5**: Output formatting (type annotations)
6. **Phase 6**: Circular reference detection (max passes)
7. **Phase 7**: (Optional) Type checking layer

---

## Key Takeaways

### **Parsing Philosophy**
- **Flat > Deep**: Parse at surface level, don't over-resolve
- **Context matters**: Member chains need context from base
- **Flexibility**: Support multiple registry sources
- **Practicality**: Actions = stdout, keep it simple

### **Type System**
- **Compositional types**: Named groupings, not strict enforcement
- **Type annotations in output**: Show types explicitly when printing
- **Optional checking**: Add later if needed

### **Resolution Strategy**
- **Lazy by default**: Don't expand everything
- **Early exit**: Stop at object/type boundaries
- **Cycles OK**: Detect but don't error, flag for review

---

## Next Steps

1. ✅ Update IMPLEMENTATION_PLAN.md with flat resolution
2. ✅ Start Phase 0: Module structure
3. ✅ Implement tokenizer with proper `-` handling
4. ✅ Build parser with left→right chaining
5. ✅ Create flat resolver (one-level expansion)
6. ✅ Add output formatter with type annotations
7. ✅ Implement all 4 registry loading strategies

**Ready to begin implementation!**
