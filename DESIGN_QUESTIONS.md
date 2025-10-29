# Skogix Notation - Design Questionnaire

**Purpose**: Define the complete grammar, semantics, and implementation strategy for Skogix Notation parser.

---

## Section 1: Operator Precedence & Associativity

### 1.1 Precedence Order

**Question**: Confirm the operator precedence from highest to lowest:

```
Proposed Precedence (highest → lowest):
1. .  (member access)        - $a.$b
2. :  (type annotation)       - $x:$type
3. @  (action/bind)           - $x@$y
4. *  (product)               - $x*$y
5. -> (morphism)              - $x->$y
6. |  (choice)                - $x|$y
7. =  (equality)              - $x=$y
8. != (inequality)            - $x!=$y
```

**Your Answer**:
- [ ] Correct as-is
- [ ] Different order (please specify):

```
1. ___________________
2. ___________________
3. ___________________
4. ___________________
5. ___________________
6. ___________________
7. ___________________
8. ___________________
```

### 1.2 Associativity

**Question**: For each operator, specify left-to-right or right-to-left associativity:

| Operator | Example | Left (a op b) op c | Right a op (b op c) | Your Choice |
|----------|---------|-------------------|---------------------|-------------|
| `.`      | `$a.$b.$c` | `($a.$b).$c` | `$a.($b.$c)` | _______ |
| `:`      | `$a:$b:$c` | `($a:$b):$c` | `$a:($b:$c)` | _______ |
| `@`      | `$a@$b@$c` | `($a@$b)@$c` | `$a@($b@$c)` | _______ |
| `*`      | `$a*$b*$c` | `($a*$b)*$c` | `$a*($b*$c)` | _______ |
| `->`     | `$a->$b->$c` | `($a->$b)->$c` | `$a->($b->$c)` | _______ |
| `\|`     | `$a\|$b\|$c` | `($a\|$b)\|$c` | `$a\|($b\|$c)` | _______ |
| `=`      | `$a=$b=$c` | `($a=$b)=$c` | `$a=($b=$c)` | _______ |
| `!=`     | `$a!=$b!=$c` | `($a!=$b)!=$c` | `$a!=($b!=$c)` | _______ |

---

## Section 2: Bracket Disambiguation

### 2.1 Array vs Similarity Brackets `[]`

**Question**: How do we distinguish between JSON arrays and Skogix similarity brackets?

**Proposed Rules**:
- `[1, 2, 3]` → Contains commas separating values → **JSON Array**
- `[$id=$id]` → Contains operators without commas → **Skogix Similarity**
- `[]` → Empty → **JSON Array** (or Skogix?)
- `[[$id]]` → Nested → **?**

**Your Answer**:
- [ ] Use proposed rules
- [ ] Different rules (please specify):

```
Rule 1: ___________________________________________
Rule 2: ___________________________________________
Rule 3: ___________________________________________
```

### 2.2 Object vs Difference Braces `{}`

**Question**: How do we distinguish between JSON objects and Skogix difference braces?

**Proposed Rules**:
- `{"key": "value"}` → Contains quoted string followed by `:` → **JSON Object**
- `{$id@$id}` → Contains operators → **Skogix Difference**
- `{}` → Empty → **JSON Object** (or Skogix?)
- `{{$id}}` → Nested → **?**

**Your Answer**:
- [ ] Use proposed rules
- [ ] Different rules (please specify):

```
Rule 1: ___________________________________________
Rule 2: ___________________________________________
Rule 3: ___________________________________________
```

---

## Section 3: Whitespace Sensitivity

### 3.1 Whitespace in References

**Question**: Is whitespace significant between prefix and identifier?

| Input | Interpretation | Valid? |
|-------|---------------|--------|
| `$id` | Reference to "id" | ✓ |
| `$ id` | Space between $ and id | ? |
| `$  id` | Multiple spaces | ? |

**Your Answer**: ___________________________________________

### 3.2 Whitespace in Operators

**Question**: Is whitespace significant around operators?

| Input | Same as | Different from | Your Choice |
|-------|---------|----------------|-------------|
| `$a.$b` | `$a . $b` | `$ a.$b` | _________ |
| `$a*$b` | `$a * $b` | `$a* $b` | _________ |
| `$a->$b` | `$a -> $b` | `$a- >$b` | _________ |

**Your Answer**:
- [ ] Whitespace is **ignored** around operators
- [ ] Whitespace is **significant** (changes meaning)
- [ ] Other: ___________________________________________

---

## Section 4: Resolution & Expansion Model

### 4.1 Definition Registry

**Question**: Where do definitions come from?

**Example**:
```
$id = $int * $unique
```

**Options**:
- [ ] **A**: Definitions are in a separate file/schema loaded before parsing
- [ ] **B**: Definitions are parsed inline from the same input
- [ ] **C**: Definitions are built-in/hardcoded
- [ ] **D**: Combination (please specify): ___________________________________________

### 4.2 Expansion Strategy

**Question**: When parsing `$id`, what happens?

**Given**:
```
$id = $int * $unique
$int = 0
$unique = $string
$string = ""
```

**When we parse** `$id`:

**Option A - Immediate Full Expansion**:
```
$id
→ $int * $unique
→ 0 * $string
→ 0 * ""
→ Result: SProduct(SNumber 0.0, SString "")
```

**Option B - Lazy Expansion (keep references)**:
```
$id
→ SRef "id" (stored, expanded later if needed)
```

**Option C - One-Level Expansion**:
```
$id
→ SProduct(SRef "int", SRef "unique") (stop at first expansion)
```

**Your Answer**:
- [ ] Option A (full expansion)
- [ ] Option B (lazy/symbolic)
- [ ] Option C (one level)
- [ ] Other: ___________________________________________

### 4.3 Circular Reference Handling

**Question**: What if definitions are circular?

**Example**:
```
$a = $b
$b = $a
```

**Options**:
- [ ] **A**: Detect cycle and throw error
- [ ] **B**: Allow infinite expansion (stack overflow)
- [ ] **C**: Stop at depth limit (e.g., 100 expansions)
- [ ] **D**: Keep as symbolic reference (don't expand)
- [ ] **E**: Other: ___________________________________________

### 4.4 Base Cases

**Question**: What are the terminal/base cases that stop expansion?

**Proposed Base Cases**:
- [ ] `null`
- [ ] Numbers: `0`, `1`, `42`, `3.14`
- [ ] Strings: `""`, `"text"`
- [ ] `_` (existence)
- [ ] Empty array: `[]`
- [ ] Empty object: `{}`
- [ ] Other: ___________________________________________

---

## Section 5: Identifier & Naming Rules

### 5.1 Valid Identifiers

**Question**: What characters are allowed in identifiers?

**Examples**:
- `id` ✓
- `entity` ✓
- `entity_id` ? (underscore)
- `entity-id` ? (hyphen)
- `entity.id` ? (dot - or is this member access?)
- `entity:id` ? (colon - or is this type annotation?)
- `123id` ? (starting with digit)

**Your Answer**:
```
Valid characters: ___________________________________________
Must start with: ___________________________________________
Cannot contain: ___________________________________________
```

### 5.2 Reserved Keywords

**Question**: Are there reserved keywords that cannot be used as identifiers?

**Proposed**:
- [ ] `null`
- [ ] `true`
- [ ] `false`
- [ ] `_` (exists as operator)
- [ ] Others: ___________________________________________

---

## Section 6: Complex Expression Examples

### 6.1 Parsing Examples

**Question**: For each input, specify the expected parse result:

#### Example 1: `$entity.id`
**Your Answer**:
- [ ] `SMember(SRef "entity", "id")`
- [ ] Expand $entity first, then access .id
- [ ] Other: ___________________________________________

#### Example 2: `$int*$unique`
**Your Answer**:
- [ ] `SProduct(SRef "int", SRef "unique")`
- [ ] Expand both, then product: `SProduct(SNumber 0, ...)`
- [ ] Other: ___________________________________________

#### Example 3: `[$id=$id]`
**Your Answer**:
- [ ] `SSimilarity(SEquality(SRef "id", SRef "id"))`
- [ ] `SArray [SEquality(...)]` (JSON array with one element)
- [ ] Other: ___________________________________________

#### Example 4: `{$id@$id}`
**Your Answer**:
- [ ] `SDifference(SBind(SRef "id", SRef "id"))`
- [ ] Error (invalid JSON object key)
- [ ] Other: ___________________________________________

#### Example 5: `$a.$b:$c*$d|$e->$f`
**Assuming precedence: . > : > * > | > ->**

**Your Answer**:
```
Parse tree (use parentheses):
___________________________________________
___________________________________________
___________________________________________
```

#### Example 6: `$entity.id*$entity.gen`
**Given**: `$entity.id = $int`, `$entity.gen = $int`

**Your Answer** (after expansion):
```
Step 1: ___________________________________________
Step 2: ___________________________________________
Final: ___________________________________________
```

---

## Section 7: JSON Integration

### 7.1 JSON Compatibility

**Question**: Should all valid JSON parse successfully?

**Examples**:
```json
null
true
42
"string"
[1, 2, 3]
{"key": "value"}
{"nested": {"object": [1, 2]}}
```

**Your Answer**:
- [ ] Yes, all valid JSON must work
- [ ] No, some JSON may conflict with Skogix notation
- [ ] Specify exceptions: ___________________________________________

### 7.2 JSON Objects in Skogix

**Question**: Can JSON objects contain Skogix expressions as values?

**Example**:
```json
{
  "name": "test",
  "type": $int,
  "relation": $a->$b
}
```

**Your Answer**:
- [ ] Yes, values can be Skogix expressions
- [ ] No, inside JSON objects only JSON values allowed
- [ ] Other: ___________________________________________

---

## Section 8: Command Compatibility

### 8.1 Legacy Commands

**Question**: Should we keep the old command syntax?

**Old Syntax**:
```
foo "string"  → SCommand("foo", Some(SString "string"))
bar           → SCommand("bar", None)
```

**Your Answer**:
- [ ] **A**: Keep commands for backward compatibility
- [ ] **B**: Replace with Skogix notation (e.g., `@foo:$string`)
- [ ] **C**: Support both
- [ ] **D**: Other: ___________________________________________

### 8.2 Command vs Action

**Question**: What's the difference between:
- `command "arg"` (old style)
- `@action` (new style)
- `$ref@$action` (bound action)

**Your Answer**: ___________________________________________
___________________________________________
___________________________________________

---

## Section 9: Error Handling

### 9.1 Parse Errors

**Question**: What should happen on parse error?

**Example**: `$id $id` (two references without operator)

**Your Answer**:
- [ ] **A**: Fail with error message
- [ ] **B**: Interpret as implicit operation (which one?): ___________
- [ ] **C**: Other: ___________________________________________

### 9.2 Undefined References

**Question**: What if we parse `$foo` but `$foo` is not defined?

**Your Answer**:
- [ ] **A**: Error immediately
- [ ] **B**: Keep as `SRef "foo"` (unresolved)
- [ ] **C**: Default to `SNull`
- [ ] **D**: Other: ___________________________________________

---

## Section 10: Implementation Strategy

### 10.1 Refactoring Approach

**Question**: Which approach do you prefer?

- [ ] **A**: Big Bang Rewrite (3 weeks)
  - Replace everything at once
  - ✓ Clean design
  - ✗ High risk, no working parser during development

- [ ] **B**: Parallel Parser (4-5 weeks) ⭐ RECOMMENDED
  - Build new parser alongside old one
  - ✓ Old code keeps working
  - ✓ Test incrementally

- [ ] **C**: Iterative Extension (6 weeks)
  - Add operators one at a time
  - ✓ Lowest risk
  - ✗ Slowest

### 10.2 Testing Strategy

**Question**: What testing approach?

- [ ] **A**: Write tests first (TDD)
- [ ] **B**: Write tests alongside implementation
- [ ] **C**: Write tests after implementation
- [ ] **D**: Use property-based testing (FsCheck)
- [ ] **E**: Combination: ___________________________________________

---

## Section 11: Priority & Scope

### 11.1 Feature Priority

**Question**: Rank features by priority (1 = highest, 10 = lowest):

- [ ] ___ Reference resolution (`$id`)
- [ ] ___ Action notation (`@action`)
- [ ] ___ Member access (`.`)
- [ ] ___ Type annotation (`:`)
- [ ] ___ Product (`*`)
- [ ] ___ Morphism (`->`)
- [ ] ___ Choice (`|`)
- [ ] ___ Equality/inequality (`=`, `!=`)
- [ ] ___ Similarity brackets `[]`
- [ ] ___ Difference braces `{}`

### 11.2 MVP Scope

**Question**: What's the minimum viable parser?

**Your Answer**: ___________________________________________
___________________________________________
___________________________________________

---

## Section 12: Example Inputs & Expected Outputs

### 12.1 Provide Test Cases

**Question**: Please provide 10 example inputs with expected outputs:

#### Test 1:
**Input**: `___________________________________________`
**Expected Output**: `___________________________________________`

#### Test 2:
**Input**: `___________________________________________`
**Expected Output**: `___________________________________________`

#### Test 3:
**Input**: `___________________________________________`
**Expected Output**: `___________________________________________`

#### Test 4:
**Input**: `___________________________________________`
**Expected Output**: `___________________________________________`

#### Test 5:
**Input**: `___________________________________________`
**Expected Output**: `___________________________________________`

#### Test 6:
**Input**: `___________________________________________`
**Expected Output**: `___________________________________________`

#### Test 7:
**Input**: `___________________________________________`
**Expected Output**: `___________________________________________`

#### Test 8:
**Input**: `___________________________________________`
**Expected Output**: `___________________________________________`

#### Test 9:
**Input**: `___________________________________________`
**Expected Output**: `___________________________________________`

#### Test 10:
**Input**: `___________________________________________`
**Expected Output**: `___________________________________________`

---

## Section 13: Definition Schema Format

### 13.1 Schema Structure

**Question**: How should definitions be stored/loaded?

**Option A - JSON Schema**:
```json
{
  "definitions": {
    "$id": "$int*$unique",
    "$int": "0",
    "$unique": "$string",
    "$string": "\"\""
  }
}
```

**Option B - Skogix Native**:
```
$id = $int*$unique
$int = 0
$unique = $string
$string = ""
```

**Option C - F# Map**:
```fsharp
let definitions = Map.ofList [
  ("id", "$int*$unique")
  ("int", "0")
  ("unique", "$string")
  ("string", "\"\"")
]
```

**Your Answer**:
- [ ] Option A (JSON)
- [ ] Option B (Native Skogix)
- [ ] Option C (F# code)
- [ ] Option D: ___________________________________________

### 13.2 Built-in Definitions

**Question**: What definitions should be built-in/hardcoded?

**Your Answer**:
```
$_______  = _______
$_______  = _______
$_______  = _______
```

---

## Section 14: Additional Notes

**Any other requirements, constraints, or clarifications?**

___________________________________________
___________________________________________
___________________________________________
___________________________________________
___________________________________________

---

## Summary Checklist

Before starting implementation, ensure all sections are answered:

- [ ] Section 1: Precedence & Associativity
- [ ] Section 2: Bracket Disambiguation
- [ ] Section 3: Whitespace Sensitivity
- [ ] Section 4: Resolution & Expansion Model ⭐ CRITICAL
- [ ] Section 5: Identifier Rules
- [ ] Section 6: Complex Examples
- [ ] Section 7: JSON Integration
- [ ] Section 8: Command Compatibility
- [ ] Section 9: Error Handling
- [ ] Section 10: Implementation Strategy
- [ ] Section 11: Priority & Scope
- [ ] Section 12: Test Cases ⭐ CRITICAL
- [ ] Section 13: Definition Schema ⭐ CRITICAL
- [ ] Section 14: Additional Notes

**Once completed, we can proceed with detailed implementation planning!**
