import * as assert from 'assert';
import * as path from 'path';

// You can import and use all API from the 'vscode' module
// as well as import your extension to test it
import * as vscode from 'vscode';
// import * as myExtension from '../../extension';
const { Parser, Language } = require('web-tree-sitter');

import { analyzeFileCoherence, DEFAULT_COHERENCE_THRESHOLDS } from '../core/detectors/coherence';
import { PYTHON } from '../languages/python';
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
});
