# Energy State Analyzer VSCode Extension - Project Summary

## Project Goal

Create a VSCode plugin that visualizes "energy states" in Python code using real-time analysis. High-energy code (complex, nested, hard to maintain) gets highlighted with visual indicators.

## Current Status

- Basic VSCode extension structure created
- TypeScript code written for energy detection
- Dependencies installed but having build issues
- Ready for debugging and compilation in Claude Code

## Architecture

### Core Concept: Energy State Principle

Software systems naturally evolve toward configurations that minimize the energy required to understand, modify, and maintain them.

**Low Energy (Good):**

- Clear boundaries, easy to understand independently
- Natural organization that mirrors problem structure
- Easy deletion without cascading changes

**High Energy (Bad):**

- Excessive nesting, tangled dependencies
- Functions that solve multiple problems
- Cognitive overload, fear-driven development

### Detection Agents (Planned)

1. **Nesting Agent** - Detects excessive control structure nesting
2. **Complexity Agent** - Calculates cyclomatic complexity
3. **Abstraction Guardian** - Finds files losing their purpose (utils/helpers sprawl)
4. **Boundary Keeper** - Detects business logic mixed with infrastructure
5. **Deletion Whisperer** - Identifies tightly coupled code
6. **Intent Translator** - Flags unclear loops/transformations

## Technical Stack

- **Language**: TypeScript (for VSCode extension)
- **AST Parsing**: web-tree-sitter with Python grammar
- **Target Language**: Python code analysis
- **Visualization**: VSCode decoration API (colored line highlights + hover tooltips)

## Current Implementation

### Files Structure

```
energy-state-analyzer/
├── src/extension.ts          # Main extension code (7.4K)
├── grammars/
│   └── tree-sitter-python.wasm  # Python grammar for parsing
├── package.json              # Extension manifest
├── webpack.config.js         # Build configuration
├── tsconfig.json            # TypeScript config
└── dist/                    # Compiled output (needs to be created)
```

### Current Detection Logic

1. **Nesting Analysis**: Flags if/for/while/with statements nested >3 levels deep
2. **Function Complexity**: Calculates cyclomatic complexity, flags >10
3. **Visual Indicators**:
   - Red background = High energy (severe issues)
   - Orange background = Medium energy (needs attention)
   - Yellow background = Low energy (minor issues)
4. **Hover Tooltips**: Explain specific energy violations

## Issues to Fix in Claude Code

### 1. Build Configuration

- Dependencies installed in parent directory vs extension directory
- TypeScript compilation errors with web-tree-sitter types
- Webpack configuration issues
- husky git hooks error

### 2. Current Compile Errors

```typescript
// These type issues need fixing:
let parser: any;  // Should be properly typed
let pythonLanguage: any;  // Should be properly typed
```

### 3. VSCode Extension Setup

- `package.json` may need proper manifest fields
- Extension not loading due to invalid configuration
- Need to ensure `dist/extension.js` is created properly

## Next Steps for Claude Code

### Immediate (Get it Working)

1. **Fix build configuration** - resolve dependency/workspace issues
2. **Compile successfully** - get `dist/extension.js` created
3. **Test basic extension** - ensure it loads in VSCode dev host
4. **Verify Python file detection** - make sure it activates on .py files

### Short Term (Improve Detection)

1. **Refine nesting detection** - test with real Python files
2. **Add more energy patterns** - utils/helpers detection, naming issues
3. **Improve visualization** - better colors, clearer hover messages
4. **Test with edge cases** - complex Python codebases

### Medium Term (Advanced Features)

1. **Add more agent types** - business logic mixing, deletion difficulty
2. **Configuration options** - user-adjustable thresholds
3. **Performance optimization** - efficient parsing for large files
4. **Multi-language support** - extend beyond Python

## Test Cases to Try

### High Energy Python Code

```python
def complex_function(data):
    if data:
        for item in data:
            if item.valid:
                for subitem in item.children:
                    if subitem.active:
                        for detail in subitem.details:
                            if detail.important:
                                # Deep nesting!
                                process(detail)

def many_params(a, b, c, d, e, f, g, h, i, j, k):
    if a and b:
        if c or d:
            if e and f:
                return "complex"
```

### Expected Behavior

- Deep nesting should get red/orange highlights
- Hover should show "Excessive nesting depth: 6. Consider extracting functions."
- Complex functions should show cyclomatic complexity warnings

## Key Learnings So Far

- VSCode extension development has specific build requirements
- web-tree-sitter TypeScript definitions are tricky
- Energy state visualization is conceptually sound
- Real-time analysis is feasible with proper AST parsing

## Resources

- VSCode Extension API docs
- tree-sitter Python grammar documentation
- Energy State Principle document (attached)

---

**Ready for Claude Code!** The foundation is solid, just needs build system debugging and testing.
