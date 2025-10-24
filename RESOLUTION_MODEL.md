# Skogix Notation - Resolution & Expansion Model

## Overview

Unlike traditional parsers that build an AST and stop, Skogix uses a **resolution-based model** where references are recursively expanded until base cases are reached.

---

## Core Concept

### Traditional Parser:
```
Input: "$id"
Output: SRef "id"  ← DONE
```

### Skogix Resolution Parser:
```
Input: "$id"
Step 1: Look up $id → "$int*$unique"
Step 2: Parse "$int*$unique" → SProduct(SRef "int", SRef "unique")
Step 3: Look up $int → "0"
Step 4: Look up $unique → "$string"
Step 5: Parse "$string" → SRef "string"
Step 6: Look up $string → "\"\""
Step 7: Parse "\"\"" → SString ""
Output: SProduct(SNumber 0.0, SString "")  ← DONE (base cases reached)
```

---

## Architecture Components

### 1. Definition Registry

**Purpose**: Store all definitions/bindings

**Structure**:
```fsharp
type DefinitionRegistry = Map<string, string>

// Example:
let registry = Map.ofList [
  ("id", "$int*$unique")
  ("int", "0")
  ("unique", "$string")
  ("string", "\"\"")
  ("entity", "{\"id\": $id, \"gen\": $int}")
  ("eid", "$entity.id*$entity.gen")
]
```

### 2. Base Cases (Terminals)

**Base cases** that stop expansion:

```fsharp
type BaseCase =
  | BNull              // null
  | BBool of bool      // true, false
  | BNumber of float   // 0, 42, 3.14
  | BString of string  // "", "text"
  | BExistence         // _
  | BEmptyArray        // []
  | BEmptyObject       // {}
```

**When do we stop expanding?**
- When we reach a literal value (number, string, null, bool)
- When we reach `_` (existence)
- When we reach empty structures `[]` or `{}`
- When expansion limit reached (prevent infinite loops)

### 3. Resolution Algorithm

```fsharp
let rec resolve (input: string) (registry: DefinitionRegistry) (depth: int) : SkogixValue =
  // 1. Check depth limit (prevent infinite recursion)
  if depth > maxDepth then
    failwith $"Maximum expansion depth {maxDepth} exceeded"

  // 2. Parse input to get initial AST
  let ast = parse input

  // 3. Expand/resolve the AST
  match ast with
  // Base cases - stop here
  | SNull | SBool _ | SNumber _ | SString "" -> ast
  | SExistence -> ast

  // References - look up and expand
  | SRef name ->
      match Map.tryFind name registry with
      | Some definition ->
          // Recursively resolve the definition
          resolve definition registry (depth + 1)
      | None ->
          // Undefined reference - what to do?
          failwith $"Undefined reference: ${name}"

  // Actions - look up and expand
  | SAction name ->
      match Map.tryFind name registry with
      | Some definition -> resolve definition registry (depth + 1)
      | None -> failwith $"Undefined action: @{name}"

  // Compound expressions - expand components
  | SProduct (left, right) ->
      let leftResolved = resolveValue left registry (depth + 1)
      let rightResolved = resolveValue right registry (depth + 1)
      SProduct (leftResolved, rightResolved)

  | SMember (obj, field) ->
      let objResolved = resolveValue obj registry (depth + 1)
      // Access field on resolved object
      accessField objResolved field

  // ... similar for other operators

  | _ -> ast

and resolveValue (value: SkogixValue) (registry: DefinitionRegistry) (depth: int) : SkogixValue =
  match value with
  | SRef name -> resolve name registry depth
  | SAction name -> resolve name registry depth
  | _ -> value
```

---

## Examples

### Example 1: Simple Reference Chain

**Registry**:
```
$id = $int*$unique
$int = 0
$unique = $string
$string = ""
```

**Input**: `$id`

**Resolution Steps**:
```
Step 1: $id
  ↓ lookup "id" → "$int*$unique"

Step 2: $int*$unique
  ↓ parse → SProduct(SRef "int", SRef "unique")
  ↓ resolve left: SRef "int"
    ↓ lookup "int" → "0"
    ↓ parse → SNumber 0.0
    ✓ base case reached
  ↓ resolve right: SRef "unique"
    ↓ lookup "unique" → "$string"
    ↓ parse → SRef "string"
    ↓ lookup "string" → "\"\""
    ↓ parse → SString ""
    ✓ base case reached

Step 3: Combine
  ↓ SProduct(SNumber 0.0, SString "")
  ✓ DONE
```

**Output**: `SProduct(SNumber 0.0, SString "")`

---

### Example 2: Member Access Resolution

**Registry**:
```
$entity = {"id": $int, "gen": $int}
$int = 0
```

**Input**: `$entity.id`

**Resolution Steps**:
```
Step 1: $entity.id
  ↓ parse → SMember(SRef "entity", "id")
  ↓ resolve object: SRef "entity"
    ↓ lookup "entity" → "{\"id\": $int, \"gen\": $int}"
    ↓ parse → SObject(Map["id" → SRef "int"; "gen" → SRef "int"])
    ↓ resolve values in object:
      ↓ "id" value: SRef "int"
        ↓ lookup "int" → "0"
        ↓ parse → SNumber 0.0
        ✓ base case
      ↓ "gen" value: SRef "int"
        ↓ lookup "int" → "0"
        ↓ parse → SNumber 0.0
        ✓ base case
    ↓ Result: SObject(Map["id" → SNumber 0.0; "gen" → SNumber 0.0])
  ↓ access field "id" → SNumber 0.0
  ✓ DONE
```

**Output**: `SNumber 0.0`

---

### Example 3: Complex Expression

**Registry**:
```
$eid = $entity.id*$entity.gen
$entity = {"id": $int, "gen": $int}
$int = 0
```

**Input**: `$eid`

**Resolution Steps**:
```
Step 1: $eid
  ↓ lookup "eid" → "$entity.id*$entity.gen"

Step 2: $entity.id*$entity.gen
  ↓ parse → SProduct(
      SMember(SRef "entity", "id"),
      SMember(SRef "entity", "gen")
    )
  ↓ resolve left: SMember(SRef "entity", "id")
    ↓ [same as Example 2]
    ↓ Result: SNumber 0.0
  ↓ resolve right: SMember(SRef "entity", "gen")
    ↓ [similar to Example 2]
    ↓ Result: SNumber 0.0

Step 3: Combine
  ↓ SProduct(SNumber 0.0, SNumber 0.0)
  ✓ DONE
```

**Output**: `SProduct(SNumber 0.0, SNumber 0.0)`

---

## Circular Reference Handling

### Problem: Infinite Loops

**Registry**:
```
$a = $b
$b = $a
```

**Input**: `$a`

**Without protection**:
```
$a → $b → $a → $b → $a → ... ∞
```

### Solution Options

#### Option A: Depth Limit
```fsharp
let maxDepth = 100

if depth > maxDepth then
  failwith "Maximum expansion depth exceeded (possible circular reference)"
```

**Pros**: Simple to implement
**Cons**: Arbitrary limit, might cut off valid deep expansions

#### Option B: Cycle Detection
```fsharp
let rec resolve (input: string) (visited: Set<string>) (registry: DefinitionRegistry) =
  if Set.contains input visited then
    failwith $"Circular reference detected: {input}"

  let newVisited = Set.add input visited
  // ... continue resolution with newVisited
```

**Pros**: Precise cycle detection
**Cons**: More complex, needs to track state

#### Option C: Lazy/Symbolic (Don't Expand)
```fsharp
let rec resolve (input: string) (registry: DefinitionRegistry) =
  match Map.tryFind input registry with
  | Some definition ->
      if isCircular definition then
        SRef input  // Keep as symbolic reference
      else
        resolve definition registry
  | None -> SRef input
```

**Pros**: Allows self-referential definitions
**Cons**: May not reach base cases

---

## Registry Loading Strategies

### Strategy 1: Preloaded Schema File

**File**: `schema.json`
```json
{
  "$id": "$int*$unique",
  "$int": "0",
  "$unique": "$string",
  "$string": "\"\""
}
```

**Load at startup**:
```fsharp
let registry = loadSchema "schema.json"
let result = resolve "$id" registry 0
```

### Strategy 2: Inline Definitions

**Input**:
```
$int = 0
$id = $int*$int
$id
```

**Parse definitions first, then evaluate**:
```fsharp
let (definitions, expression) = parseInput input
let registry = buildRegistry definitions
let result = resolve expression registry 0
```

### Strategy 3: Built-in Primitives

```fsharp
let builtInRegistry = Map.ofList [
  ("int", "0")
  ("string", "\"\"")
  ("bool", "true")
  ("null", "null")
  ("list", "[]")
  ("object", "{}")
]

// Merge with user definitions
let fullRegistry = Map.merge builtInRegistry userRegistry
```

---

## Implementation Phases

### Phase 1: Basic Resolution (Week 1)
- [ ] Create `DefinitionRegistry` type
- [ ] Implement simple lookup
- [ ] Handle base cases (null, 0, "", _)
- [ ] Test with simple chains: `$a → $b → 0`

### Phase 2: Compound Expressions (Week 2)
- [ ] Resolve products: `$a*$b`
- [ ] Resolve morphisms: `$a->$b`
- [ ] Resolve choices: `$a|$b`
- [ ] Test combinations

### Phase 3: Member Access (Week 3)
- [ ] Resolve object member access: `$entity.id`
- [ ] Resolve nested access: `$entity.parent.id`
- [ ] Handle undefined fields

### Phase 4: Circular Reference Protection (Week 4)
- [ ] Implement depth limit
- [ ] Add cycle detection
- [ ] Test circular cases

### Phase 5: Schema Loading (Week 5)
- [ ] Design schema format
- [ ] Implement schema parser
- [ ] Load at startup
- [ ] Merge with built-ins

---

## Open Questions

### Q1: Partial Expansion
Should we allow stopping expansion before base cases?

**Example**:
```
$eid = $entity.id*$entity.gen
```

**Option A**: Expand fully to `SProduct(SNumber 0.0, SNumber 0.0)`
**Option B**: Expand one level to `SProduct(SRef "entity.id", SRef "entity.gen")`
**Option C**: Keep symbolic `SRef "eid"`

### Q2: Side Effects in Actions
What if `@action` has side effects?

**Example**:
```
@print:$string
```

Should resolution **execute** the action or just **represent** it?

### Q3: Type Checking
Should we validate types during resolution?

**Example**:
```
$id:$int = "not a number"
```

Should this **fail** during resolution or be allowed?

### Q4: Caching
Should we cache resolved values?

**Example**:
```
$id appears 100 times in input
```

**Option A**: Resolve once, cache result
**Option B**: Resolve every time (pure)

---

## Summary

**The Resolution Model transforms Skogix from a parser into an evaluator:**

1. **Parse** input to AST
2. **Resolve** references by looking up definitions
3. **Expand** recursively until base cases
4. **Return** fully resolved value

**This requires:**
- Definition registry (where to look up `$id`)
- Recursive expansion algorithm
- Cycle detection/depth limits
- Clear base cases

**Next Steps:**
1. Fill out `DESIGN_QUESTIONS.md`
2. Decide on registry format
3. Choose circular reference strategy
4. Provide example definitions
5. Start implementation

---

**Questions to answer:**
- Where do definitions come from? (file, inline, built-in)
- How to handle circular references? (depth limit, cycle detection, lazy)
- When to stop expansion? (base cases, one level, never)
- Should we cache resolved values?
