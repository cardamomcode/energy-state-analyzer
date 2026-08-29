// Domain: editor decoration presentation. Owns all visual energy-state rendering — creating and
// disposing decoration types (createDecorations), applying per-violation highlight ranges and the
// complexity heatmap (applyDecorations / applyComplexityHeat). State is threaded in as a DecorationSet
// from the composition root; this module holds no mutable singletons. Coherence: keep ≤12 functions,
// ≤10 imports — if it grows, split by concern (e.g. icon generation vs. range computation).
import * as vscode from 'vscode';
import { EnergyViolation, SEVERITY, VIOLATION_TYPE } from './types';
import { DEFAULT_ENERGY_COLORS, EnergyColors, getEnergyColors } from './config';

// The set of decoration types applied to an editor for a single analysis pass.
// Owned by the activation composition root (src/extension.ts) and threaded through the
// presentation functions below so this module holds no mutable state of its own.
export interface DecorationSet {
    highEnergy: vscode.TextEditorDecorationType;
    mediumEnergy: vscode.TextEditorDecorationType;
    lowEnergy: vscode.TextEditorDecorationType;
    complexityHeat: vscode.TextEditorDecorationType[];
}

// Increasing alpha steps for the complexity heatmap bands, darkest last. Not user-configurable
// — see the `decision:` note at their only use site in createDecorations.
const HEAT_BAND_ALPHAS = [0.1, 0.18, 0.28, 0.42];

const HEX_RADIX = 16;
const HEX_CHANNEL_WIDTH = 2;

// decision: parses user-supplied hex strings defensively and falls back to the built-in default on malformed input, since a bad `energyStateAnalyzer.colors.*` setting must not crash decoration setup
function hexToRgba(hex: string, alpha: number, fallback: string): string {
    const match = /^#?([0-9a-fA-F]{6})$/.exec(hex.trim());
    const digits = match ? match[1] : fallback.replace('#', '');
    const r = parseHexChannel(digits, 0);
    const g = parseHexChannel(digits, 1);
    const b = parseHexChannel(digits, 2);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

function parseHexChannel(digits: string, channelIndex: number): number {
    const start = channelIndex * HEX_CHANNEL_WIDTH;
    return parseInt(digits.substring(start, start + HEX_CHANNEL_WIDTH), HEX_RADIX);
}

// Create lightning bolt icon for energy violations
function createLightningIcon(color: string): vscode.Uri {
    const svg = `
    <svg width="16" height="16" xmlns="http://www.w3.org/2000/svg">
        <circle cx="8" cy="8" r="7" fill="${color}" opacity="0.95"/>
        <path d="M6 3 L10 8 L8.5 8 L10.5 13 L6.5 8 L8 8 Z" fill="white" stroke="white" stroke-width="0.3"/>
    </svg>`;
    const dataUri = `data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}`;
    return vscode.Uri.parse(dataUri);
}

export function createDecorations(): DecorationSet {
    const colors = getEnergyColors();

    const highEnergyDecoration = vscode.window.createTextEditorDecorationType({
        // Subtle background highlight that's still hoverable
        backgroundColor: hexToRgba(colors.highEnergy, colors.backgroundOpacity, DEFAULT_ENERGY_COLORS.highEnergy),
        borderRadius: '2px',
        gutterIconPath: createLightningIcon(colors.highEnergy),
        gutterIconSize: 'contain'
    });

    const mediumEnergyDecoration = vscode.window.createTextEditorDecorationType({
        backgroundColor: hexToRgba(colors.mediumEnergy, colors.backgroundOpacity, DEFAULT_ENERGY_COLORS.mediumEnergy),
        borderRadius: '2px',
        gutterIconPath: createLightningIcon(colors.mediumEnergy),
        gutterIconSize: 'contain'
    });

    const lowEnergyDecoration = vscode.window.createTextEditorDecorationType({
        backgroundColor: hexToRgba(colors.lowEnergy, colors.backgroundOpacity, DEFAULT_ENERGY_COLORS.lowEnergy),
        borderRadius: '2px',
        gutterIconPath: createLightningIcon(colors.lowEnergy),
        gutterIconSize: 'contain'
    });

    // decision: complexity heat bands carry no gutter icon — the function-level violation decoration already owns the gutter icon for that line range, so these bands only paint background intensity
    // decision: heat bands derive from the same highEnergy color as the gutter icon (four increasing alpha steps) instead of a separate setting, so the heatmap and the violation it belongs to always match — kept as a fixed constant rather than a config option for the same reason
    const complexityHeatDecorations = HEAT_BAND_ALPHAS.map((alpha) =>
        vscode.window.createTextEditorDecorationType({
            backgroundColor: hexToRgba(colors.highEnergy, alpha, DEFAULT_ENERGY_COLORS.highEnergy)
        })
    );

    return {
        highEnergy: highEnergyDecoration,
        mediumEnergy: mediumEnergyDecoration,
        lowEnergy: lowEnergyDecoration,
        complexityHeat: complexityHeatDecorations
    };
}

export function disposeDecorations(set: DecorationSet): void {
    set.highEnergy.dispose();
    set.mediumEnergy.dispose();
    set.lowEnergy.dispose();
    set.complexityHeat.forEach((decoration) => decoration.dispose());
}

// Fixed-width span used to highlight a flagged element (a magic value, a parameter) when there's
// no AST range to highlight instead — not user-configurable, since it's a rendering detail of
// this decoration rather than a detection threshold.
const ELEMENT_HIGHLIGHT_WIDTH = 15;

export function applyDecorations(editor: vscode.TextEditor, set: DecorationSet, violations: EnergyViolation[]): void {
    const highEnergyRanges: vscode.DecorationOptions[] = [];
    const mediumEnergyRanges: vscode.DecorationOptions[] = [];
    const lowEnergyRanges: vscode.DecorationOptions[] = [];

    for (const violation of violations) {
        // decision: picks the decoration range by violation type rather than storing a range on EnergyViolation itself — coherence issues span the whole first line, nesting/complexity/cognitive issues span from the construct's start to end of line, and everything else highlights a fixed-width span at the violation's column
        let range: vscode.Range;
        const line = editor.document.lineAt(violation.line);

        if (violation.type === VIOLATION_TYPE.COHERENCE) {
            // Highlight entire first line for file-level issues
            range = new vscode.Range(violation.line, 0, violation.line, line.text.length);
        } else if (
            violation.type === VIOLATION_TYPE.NESTING ||
            violation.type === VIOLATION_TYPE.COMPLEXITY ||
            violation.type === VIOLATION_TYPE.COGNITIVE
        ) {
            // Highlight from function start to end of line
            const functionStart = line.text.search(/\S/); // Find first non-whitespace
            range = new vscode.Range(violation.line, functionStart, violation.line, line.text.length);
        } else {
            // For magic values and parameters, highlight the specific element
            const endColumn = Math.min(violation.column + ELEMENT_HIGHLIGHT_WIDTH, line.text.length);
            range = new vscode.Range(violation.line, violation.column, violation.line, endColumn);
        }

        const decoration: vscode.DecorationOptions = {
            range,
            hoverMessage: `🔋 Energy Violation: ${violation.message}`
        };

        switch (violation.severity) {
            case SEVERITY.HIGH:
                highEnergyRanges.push(decoration);
                break;
            case SEVERITY.MEDIUM:
                mediumEnergyRanges.push(decoration);
                break;
            case SEVERITY.LOW:
                lowEnergyRanges.push(decoration);
                break;
        }
    }

    editor.setDecorations(set.highEnergy, highEnergyRanges);
    editor.setDecorations(set.mediumEnergy, mediumEnergyRanges);
    editor.setDecorations(set.lowEnergy, lowEnergyRanges);

    applyComplexityHeat(editor, set, violations);
}

// invariant: heat intensity is normalized per-violation — the single worst line in a function is always the darkest band, regardless of how that function compares to others in the file
function computeHeatByLine(violations: EnergyViolation[]): Map<number, number> {
    const heatByLine = new Map<number, number>();

    for (const violation of violations) {
        if (!violation.hotspots || violation.hotspots.length === 0) {
            continue;
        }

        const maxWeight = Math.max(...violation.hotspots.map((hotspot) => hotspot.weight));
        if (maxWeight <= 0) {
            continue;
        }

        for (const hotspot of violation.hotspots) {
            const intensity = hotspot.weight / maxWeight;
            heatByLine.set(hotspot.line, Math.max(heatByLine.get(hotspot.line) ?? 0, intensity));
        }
    }

    return heatByLine;
}

// Paints a progressive heatmap (in the configured high-energy color) over the lines
// that actually drive a flagged function's complexity, so instead of just knowing
// "this function is complex" you can see exactly which branches to break apart first.
function applyComplexityHeat(editor: vscode.TextEditor, set: DecorationSet, violations: EnergyViolation[]): void {
    const heatByLine = computeHeatByLine(violations);
    const bandCount = set.complexityHeat.length;
    const bandRanges: vscode.Range[][] = set.complexityHeat.map(() => []);

    for (const [line, intensity] of heatByLine) {
        if (line < 0 || line >= editor.document.lineCount) {
            continue;
        }
        const bandIndex = Math.min(bandCount - 1, Math.floor(intensity * bandCount));
        const lineText = editor.document.lineAt(line).text;
        bandRanges[bandIndex].push(new vscode.Range(line, 0, line, lineText.length));
    }

    set.complexityHeat.forEach((decoration, index) => {
        editor.setDecorations(decoration, bandRanges[index]);
    });
}
