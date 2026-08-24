import * as fs from 'fs';
import * as path from 'path';

const { Parser, Language } = require('web-tree-sitter');

import { LanguageAdapter } from '../../core/language';
import { EnergyViolation, VIOLATION_TYPE } from '../../types';

// decision: reads fixtures from the source tree, not __dirname (out/test/integration) -
// tsc only emits compiled .js for the .ts fixtures, it never copies fixture files
// verbatim, so out/ would be missing the .py/.fs sources and the .ts source text.
const FIXTURES_ROOT = path.join(__dirname, '..', '..', '..', 'src', 'test', 'fixtures');

export async function parseFixture(language: LanguageAdapter, relativePath: string) {
    await Parser.init();
    const parser = new Parser();
    const grammarPath = path.join(__dirname, '..', '..', '..', language.grammarPath);
    const grammar = await Language.load(grammarPath);
    parser.setLanguage(grammar);

    const fixturePath = path.join(FIXTURES_ROOT, relativePath);
    const sourceCode = fs.readFileSync(fixturePath, 'utf8');
    const tree = parser.parse(sourceCode);
    return { sourceCode, tree };
}

// Line range (inclusive, 0-indexed like tree-sitter/EnergyViolation.line) that a
// named function occupies within a fixture, so tests can assert a violation
// belongs to a specific example function without hardcoding exact line numbers.
export function findFunctionRange(sourceCode: string, functionName: string): { start: number; end: number } {
    const lines = sourceCode.split('\n');
    const start = lines.findIndex(line => line.includes(functionName));
    if (start === -1) {
        throw new Error(`fixture does not contain a function named '${functionName}'`);
    }
    let end = lines.length - 1;
    for (let i = start + 1; i < lines.length; i++) {
        // decision: a top-level definition starts in column 0 with no leading whitespace
        // (def/function/let at module scope) - this is the fixture convention every
        // rule fixture follows, so it's a reliable "next function starts here" marker.
        if (/^\S/.test(lines[i]) && lines[i].trim().length > 0) {
            end = i - 1;
            break;
        }
    }
    return { start, end };
}

export function violationsIn(violations: EnergyViolation[], range: { start: number; end: number }): EnergyViolation[] {
    return violations.filter(v => v.line >= range.start && v.line <= range.end);
}

export function assertValidPositions(violations: EnergyViolation[], sourceCode: string): void {
    const assert = require('assert');
    const lineCount = sourceCode.split('\n').length;
    for (const violation of violations) {
        assert.ok(violation.line >= 0 && violation.line < lineCount,
            `violation line ${violation.line} out of range for a ${lineCount}-line file (${violation.message})`);
        assert.ok(violation.column >= 0, `violation column ${violation.column} should be non-negative`);
        assert.ok(Object.values(VIOLATION_TYPE).includes(violation.type as any),
            `unknown violation type: ${violation.type}`);
    }
    assert.doesNotThrow(() => JSON.stringify(violations));
}
