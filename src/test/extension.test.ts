import * as assert from 'assert';
import * as path from 'path';

// You can import and use all API from the 'vscode' module
// as well as import your extension to test it
import * as vscode from 'vscode';
// import * as myExtension from '../../extension';
const { Parser, Language } = require('web-tree-sitter');

import { analyzeFileCoherence, DEFAULT_COHERENCE_THRESHOLDS } from '../core/detectors/coherence';
import { analyzeInversionOpportunities } from '../core/detectors/inversion';
import { analyzePrimitiveObsession } from '../core/detectors/primitiveObsession';
import { analyzeMatchOpportunities, MatchOpportunityThresholds } from '../core/detectors/matchOpportunity';
import { createPositionLookup } from '../core/position';
import { LanguageAdapter } from '../core/language';
import { PYTHON } from '../languages/python';
import { TYPESCRIPT } from '../languages/typescript';
import { FSHARP } from '../languages/fsharp';
import { VIOLATION_TYPE } from '../types';

suite('Extension Test Suite', () => {
	vscode.window.showInformationMessage('Start all tests.');

	test('Sample test', () => {
		assert.strictEqual(-1, [1, 2, 3].indexOf(5));
		assert.strictEqual(-1, [1, 2, 3].indexOf(0));
	});
});

suite('analyzeFileCoherence', () => {
	async function parsePython(sourceCode: string) {
		await Parser.init();
		const parser = new Parser();
		const grammarPath = path.join(__dirname, '..', '..', PYTHON.grammarPath);
		const grammar = await Language.load(grammarPath);
		parser.setLanguage(grammar);
		return parser.parse(sourceCode);
	}

	function makeFunction(name: string, lineCount: number): string {
		const body = Array.from({ length: lineCount - 1 }, (_, i) => `    x${i} = ${i}`).join('\n');
		return `def ${name}():\n${body}\n`;
	}

	test('flags a file with too many large functions', async () => {
		const source = Array.from({ length: 6 }, (_, i) => makeFunction(`big_${i}`, 25)).join('\n');
		const tree = await parsePython(source);

		const violations = analyzeFileCoherence(tree, 'module.py', PYTHON);

		const largeFunctionViolation = violations.find(v =>
			v.type === VIOLATION_TYPE.COHERENCE && v.message.includes('exceed')
		);
		assert.ok(largeFunctionViolation, 'expected a large-function coherence violation');
	});

	test('does not flag a file with many small functions', async () => {
		const source = Array.from({ length: 20 }, (_, i) => makeFunction(`small_${i}`, 3)).join('\n');
		const tree = await parsePython(source);

		const violations = analyzeFileCoherence(tree, 'module.py', PYTHON);

		const largeFunctionViolation = violations.find(v =>
			v.type === VIOLATION_TYPE.COHERENCE && v.message.includes('exceed')
		);
		assert.strictEqual(largeFunctionViolation, undefined);
	});

	test('respects a custom maxLargeFunctions threshold', async () => {
		const source = Array.from({ length: 3 }, (_, i) => makeFunction(`big_${i}`, 25)).join('\n');
		const tree = await parsePython(source);

		const defaultViolations = analyzeFileCoherence(tree, 'module.py', PYTHON);
		assert.strictEqual(
			defaultViolations.find(v => v.message.includes('exceed')),
			undefined,
			'3 large functions should not trip the default threshold of 5'
		);

		const strictViolations = analyzeFileCoherence(tree, 'module.py', PYTHON, {
			...DEFAULT_COHERENCE_THRESHOLDS,
			maxLargeFunctions: 2
		});
		const largeFunctionViolation = strictViolations.find(v => v.message.includes('exceed'));
		assert.ok(largeFunctionViolation, 'expected a violation once maxLargeFunctions is lowered to 2');
	});

	test('flags a file with many small, differently-named functions (grab-bag sprawl)', async () => {
		const names = ['parse', 'format', 'validate', 'normalize', 'render', 'cache', 'retry', 'log', 'sanitize', 'merge', 'diff', 'sort', 'flatten'];
		const source = names.map(name => makeFunction(name, 3)).join('\n');
		const tree = await parsePython(source);

		const violations = analyzeFileCoherence(tree, 'module.py', PYTHON);

		const countViolation = violations.find(v => v.message.includes('functions in one file'));
		assert.ok(countViolation, 'expected a raw function-count coherence violation');
	});

	test('does not flag many small functions that share a leading name word (single domain)', async () => {
		const names = ['extract_functions', 'extract_classes', 'extract_imports', 'extract_variables', 'extract_types', 'extract_fields', 'extract_bases', 'extract_params', 'extract_returns', 'extract_docs', 'extract_decorators', 'extract_annotations', 'extract_defaults'];
		const source = names.map(name => makeFunction(name, 3)).join('\n');
		const tree = await parsePython(source);

		const violations = analyzeFileCoherence(tree, 'module.py', PYTHON);

		const countViolation = violations.find(v => v.message.includes('functions in one file'));
		assert.strictEqual(countViolation, undefined, 'a shared extract_* naming domain should suppress the raw count check');
	});
});

suite('analyzePrimitiveObsession', () => {
	async function parse(language: LanguageAdapter, sourceCode: string) {
		await Parser.init();
		const parser = new Parser();
		const grammarPath = path.join(__dirname, '..', '..', language.grammarPath);
		const grammar = await Language.load(grammarPath);
		parser.setLanguage(grammar);
		return parser.parse(sourceCode);
	}

	async function violationsFor(language: LanguageAdapter, sourceCode: string) {
		const tree = await parse(language, sourceCode);
		const positions = createPositionLookup(sourceCode);
		return analyzePrimitiveObsession(tree, positions, language);
	}

	test('Python: flags consecutive same-primitive-type parameters', async () => {
		const violations = await violationsFor(PYTHON, `def move(x: int, y: int):\n    pass\n`);
		const swapRisk = violations.find(v => v.message.includes('swap'));
		assert.ok(swapRisk, 'expected a parameter-swap-risk violation');
	});

	test('Python: flags a variable compared against 3+ distinct string literals', async () => {
		const violations = await violationsFor(PYTHON, `def classify(status):\n    if status == "open":\n        pass\n    elif status == "closed":\n        pass\n    elif status == "pending":\n        pass\n`);
		const stringly = violations.find(v => v.message.includes('Stringly-typed'));
		assert.ok(stringly, 'expected a stringly-typed-control-flow violation');
	});

	test('TypeScript: flags consecutive same-primitive-type parameters', async () => {
		const violations = await violationsFor(TYPESCRIPT, `function move(x: number, y: number) {}\n`);
		const swapRisk = violations.find(v => v.message.includes('swap'));
		assert.ok(swapRisk, 'expected a parameter-swap-risk violation');
	});

	test('TypeScript: flags a variable compared against 3+ distinct string literals', async () => {
		const violations = await violationsFor(TYPESCRIPT, `function classify(status: string): number {\n    if (status === "open") { return 1; }\n    else if (status === "closed") { return 2; }\n    else if (status === "pending") { return 3; }\n    return 0;\n}\n`);
		const stringly = violations.find(v => v.message.includes('Stringly-typed'));
		assert.ok(stringly, 'expected a stringly-typed-control-flow violation');
	});

	test('F#: flags consecutive same-primitive-type parameters', async () => {
		const violations = await violationsFor(FSHARP, `let move (x: int) (y: int) =\n    x\n`);
		const swapRisk = violations.find(v => v.message.includes('swap'));
		assert.ok(swapRisk, 'expected a parameter-swap-risk violation');
	});

	test('F#: flags a variable compared against 3+ distinct string literals', async () => {
		const violations = await violationsFor(FSHARP, `let classify (status: string) =\n    if status = "open" then 1\n    elif status = "closed" then 2\n    elif status = "pending" then 3\n    else 0\n`);
		const stringly = violations.find(v => v.message.includes('Stringly-typed'));
		assert.ok(stringly, 'expected a stringly-typed-control-flow violation');
	});
});

suite('analyzeMatchOpportunities', () => {
	async function parse(language: LanguageAdapter, sourceCode: string) {
		await Parser.init();
		const parser = new Parser();
		const grammarPath = path.join(__dirname, '..', '..', language.grammarPath);
		const grammar = await Language.load(grammarPath);
		parser.setLanguage(grammar);
		return parser.parse(sourceCode);
	}

	async function violationsFor(language: LanguageAdapter, sourceCode: string, thresholds?: MatchOpportunityThresholds) {
		const tree = await parse(language, sourceCode);
		const positions = createPositionLookup(sourceCode);
		return analyzeMatchOpportunities(tree, positions, language, thresholds);
	}

	test('Python: flags a 3-way if/elif chain branching on the same variable', async () => {
		const violations = await violationsFor(PYTHON, `def classify(status):\n    if status == "open":\n        return 1\n    elif status == "closed":\n        return 2\n    elif status == "pending":\n        return 3\n    else:\n        return 0\n`);
		const match = violations.find(v => v.type === VIOLATION_TYPE.MATCH_OPPORTUNITY);
		assert.ok(match, 'expected a match-opportunity violation');
	});

	test('Python: does not flag branches keyed on different variables', async () => {
		const violations = await violationsFor(PYTHON, `def f(a, b):\n    if a == "x":\n        return 1\n    elif b == "y":\n        return 2\n    else:\n        return 0\n`);
		assert.strictEqual(violations.length, 0, 'expected no match-opportunity violation for unrelated branch conditions');
	});

	test('Python: respects a configured minBranches threshold', async () => {
		const source = `def classify(status):\n    if status == "open":\n        return 1\n    elif status == "closed":\n        return 2\n    else:\n        return 0\n`;
		const withDefault = await violationsFor(PYTHON, source);
		assert.strictEqual(withDefault.length, 0, 'expected a 2-way chain to be below the default minBranches of 3');

		const withLoweredThreshold = await violationsFor(PYTHON, source, { minBranches: 2 });
		assert.strictEqual(withLoweredThreshold.length, 1, 'expected a 2-way chain to be flagged once minBranches is lowered to 2');
	});

	test('TypeScript: flags a nested else-if chain branching on the same variable', async () => {
		const violations = await violationsFor(TYPESCRIPT, `function classify(status) {\n  if (status === "open") { return 1; }\n  else if (status === "closed") { return 2; }\n  else if (status === "pending") { return 3; }\n  return 0;\n}\n`);
		const match = violations.find(v => v.type === VIOLATION_TYPE.MATCH_OPPORTUNITY);
		assert.ok(match, 'expected a match-opportunity violation');
	});

	test('F#: flags an if/elif chain branching on the same variable', async () => {
		const violations = await violationsFor(FSHARP, `let classify status =\n    if status = "open" then 1\n    elif status = "closed" then 2\n    elif status = "pending" then 3\n    else 0\n`);
		const match = violations.find(v => v.type === VIOLATION_TYPE.MATCH_OPPORTUNITY);
		assert.ok(match, 'expected a match-opportunity violation');
	});
});

suite('analyzeInversionOpportunities', () => {
	async function parsePython(sourceCode: string) {
		await Parser.init();
		const parser = new Parser();
		const grammarPath = path.join(__dirname, '..', '..', PYTHON.grammarPath);
		const grammar = await Language.load(grammarPath);
		parser.setLanguage(grammar);
		return parser.parse(sourceCode);
	}

	// Regression test: a nested function definition inside an analyzed function used to get its
	// if-nesting counted twice — once when analyzeNestedIfs walked the outer function's body
	// (with no function-boundary check, it kept going into the nested function too), and again
	// when the traversal separately visited the nested function as its own target. Found by
	// running this detector on its own source (src/core/detectors/inversion.ts), which nests
	// several helper functions this way.
	test('does not double-count if-nesting inside a nested function definition', async () => {
		const source = [
			'def outer():',
			'    def helper(a, b, c, d):',
			'        if a:',
			'            if b:',
			'                if c:',
			'                    if d:',
			'                        return 1',
			'        return 0',
			'    return helper(True, True, True, True)'
		].join('\n');
		const tree = await parsePython(source);
		const positions = createPositionLookup(source);

		const violations = analyzeInversionOpportunities(tree, positions, PYTHON);
		const deepNesting = violations.filter(v => v.message.includes('Deep if-nesting'));

		assert.strictEqual(deepNesting.length, 1,
			`expected exactly one deep-nesting violation (for helper), got ${deepNesting.length}`);
	});
});
