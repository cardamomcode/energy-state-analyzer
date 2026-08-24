// Shared types for energy violation detectors (cyclomatic.ts, cognitive.ts, extension.ts)

export const SEVERITY = {
    LOW: 'low' as const,
    MEDIUM: 'medium' as const,
    HIGH: 'high' as const
};

export const VIOLATION_TYPE = {
    NESTING: 'nesting' as const,
    COMPLEXITY: 'complexity' as const,
    COGNITIVE: 'cognitive' as const,
    NAMING: 'naming' as const,
    COHERENCE: 'coherence' as const,
    MAGIC: 'magic' as const,
    PARAMETERS: 'parameters' as const,
    INVERSION: 'inversion' as const
};

// A single contributor line inside a flagged function, weighted by how much
// it drives up the complexity score (nesting depth for cognitive, decision
// density for cyclomatic). Used to paint a progressive heatmap across the
// function body instead of a single flat highlight, so the worst lines stand
// out visually.
export interface ComplexityHotspot {
    line: number;
    weight: number;
}

export interface EnergyViolation {
    line: number;
    column: number;
    type: 'nesting' | 'complexity' | 'cognitive' | 'naming' | 'coherence' | 'magic' | 'parameters' | 'inversion';
    severity: 'low' | 'medium' | 'high';
    message: string;
    hotspots?: ComplexityHotspot[];
}
