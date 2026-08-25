import { EnergyViolation, VIOLATION_TYPE, SEVERITY } from '../../types';
import { PositionLookup } from '../position';
import { LanguageAdapter } from '../language';

// The "Opaque Boolean Literal" detector: a bare `true`/`false` passed positionally
// into a call gives the reader no information about what it means without tracing
// into the callee's signature — unlike primitive-obsession's swap-risk check, this
// doesn't need a second adjacent parameter to be a problem; one opaque literal is
// enough. Naming it at the call site (whatever the language allows: a keyword
// argument, an object-literal field, F#'s named-argument syntax) fixes the
// readability problem even where it isn't enforced by the type checker — see
// isPositionalCallArgument's per-language doc for how each grammar distinguishes
// labeled from positional.
function buildViolation(node: any, positions: PositionLookup): EnergyViolation {
    const position = positions.toPosition(node.startIndex);
    return {
        line: position.line,
        column: position.column,
        type: VIOLATION_TYPE.OPAQUE_BOOLEAN,
        severity: SEVERITY.LOW,
        message: `Opaque boolean literal: a bare '${node.text}' passed positionally tells the reader nothing without checking the callee's signature. Name it at the call site (a keyword argument, an object-literal field, or F#'s named-argument syntax) — or better, split into two clearly named functions (e.g. enableX()/disableX()) or use an enum.`
    };
}

export function analyzeOpaqueBooleanLiteral(tree: any, positions: PositionLookup, language: LanguageAdapter): EnergyViolation[] {
    const violations: EnergyViolation[] = [];

    function traverse(node: any) {
        if (language.isBooleanLiteral(node) && language.isPositionalCallArgument(node)) {
            violations.push(buildViolation(node, positions));
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(tree.rootNode);
    return violations;
}
