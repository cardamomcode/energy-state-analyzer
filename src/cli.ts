#!/usr/bin/env node
// Headless entry point: run the same detectors the extension uses, without
// vscode, so an external process (e.g. an AI coding agent) can gate on
// complexity without opening an editor.
import * as fs from 'fs';
import * as path from 'path';
const { Parser, Language } = require('web-tree-sitter');

import { analyzeSource } from './core/analyze';
import { resolveLanguageForFile } from './languages';
import { EnergyViolation, SEVERITY } from './types';

function printUsage(): void {
    console.error('Usage: energy-state-cli <file.py|.fs|.fsx|.ts> [--medium-cyclomatic N] [--high-cyclomatic N] [--medium-cognitive N] [--high-cognitive N]');
}

function parseArgs(argv: string[]) {
    const filePath = argv.find(arg => !arg.startsWith('--'));
    const flag = (name: string): number | undefined => {
        const index = argv.indexOf(`--${name}`);
        return index !== -1 ? Number(argv[index + 1]) : undefined;
    };

    return {
        filePath,
        cyclomatic: {
            mediumThreshold: flag('medium-cyclomatic'),
            highThreshold: flag('high-cyclomatic')
        },
        cognitive: {
            mediumThreshold: flag('medium-cognitive'),
            highThreshold: flag('high-cognitive')
        }
    };
}

async function main(): Promise<void> {
    const { filePath, cyclomatic, cognitive } = parseArgs(process.argv.slice(2));

    if (!filePath) {
        printUsage();
        process.exit(2);
    }

    const adapter = resolveLanguageForFile(filePath);
    if (!adapter) {
        console.error(`Unsupported file type: ${filePath}`);
        printUsage();
        process.exit(2);
    }

    const sourceCode = fs.readFileSync(filePath, 'utf8');

    await Parser.init();
    const parser = new Parser();
    const grammarPath = path.join(__dirname, '..', adapter.grammarPath);
    const grammar = await Language.load(grammarPath);
    parser.setLanguage(grammar);

    const tree = parser.parse(sourceCode);
    const violations: EnergyViolation[] = analyzeSource(sourceCode, tree, adapter, filePath, {
        cyclomatic: cyclomatic.mediumThreshold !== undefined || cyclomatic.highThreshold !== undefined
            ? { mediumThreshold: cyclomatic.mediumThreshold ?? 10, highThreshold: cyclomatic.highThreshold ?? 15 }
            : undefined,
        cognitive: cognitive.mediumThreshold !== undefined || cognitive.highThreshold !== undefined
            ? { mediumThreshold: cognitive.mediumThreshold ?? 15, highThreshold: cognitive.highThreshold ?? 25 }
            : undefined
    });

    console.log(JSON.stringify(violations, null, 2));

    const hasBlockingViolation = violations.some(v => v.severity === SEVERITY.HIGH || v.severity === SEVERITY.MEDIUM);
    process.exit(hasBlockingViolation ? 1 : 0);
}

main().catch(error => {
    console.error('energy-state-cli failed:', error);
    process.exit(1);
});
