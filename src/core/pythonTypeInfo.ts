// Python-specific type information extraction from the tree-sitter AST.
//
// decision: scaffolding for future features — currently only logged by the extension
// (see extractTypeInformation's call site in extension.ts), not wired into any violation
// invariant: this module must not import vscode — same host-independence rule as core/analyze.ts,
// so this can eventually be exercised by the headless CLI too
import { PositionLookup } from './position';

export interface TypeInfo {
    functions: FunctionTypeInfo[];
    variables: VariableTypeInfo[];
    classes: ClassTypeInfo[];
    imports: ImportInfo[];
}

export interface FunctionTypeInfo {
    name: string;
    line: number;
    parameters: ParameterTypeInfo[];
    returnType: string | null;
}

export interface ParameterTypeInfo {
    name: string;
    type: string | null;
    hasDefault: boolean;
}

export interface VariableTypeInfo {
    name: string;
    type: string;
    line: number;
}

export interface ClassTypeInfo {
    name: string;
    line: number;
    baseClasses: string[];
    isTypedDict: boolean;
    fields: VariableTypeInfo[];
}

export interface ImportInfo {
    module: string;
    items: string[];
    line: number;
}

export function extractTypeInformation(tree: any, positions: PositionLookup): TypeInfo {
    const typeInfo: TypeInfo = {
        functions: [],
        variables: [],
        classes: [],
        imports: []
    };

    function traverse(node: any) {
        switch (node.type) {
            case 'function_definition':
                typeInfo.functions.push(extractFunctionTypeInfo(node, positions));
                break;
            case 'class_definition':
                typeInfo.classes.push(extractClassTypeInfo(node, positions));
                break;
            case 'assignment':
                const varInfo = extractVariableTypeInfo(node, positions);
                if (varInfo) {
                    typeInfo.variables.push(varInfo);
                }
                break;
            case 'import_statement':
            case 'import_from_statement':
                typeInfo.imports.push(extractImportInfo(node, positions));
                break;
        }

        for (const child of node.children) {
            traverse(child);
        }
    }

    traverse(tree.rootNode);
    return typeInfo;
}

function extractFunctionTypeInfo(node: any, positions: PositionLookup): FunctionTypeInfo {
    const nameNode = node.children.find((child: any) => child.type === 'identifier');
    const parametersNode = node.children.find((child: any) => child.type === 'parameters');

    const returnType = extractReturnTypeAnnotation(node.children);
    const parameters = parametersNode ? extractParameters(parametersNode) : [];

    const position = positions.toPosition(node.startIndex);
    return {
        name: nameNode?.text || 'unknown',
        line: position.line,
        parameters,
        returnType
    };
}

function extractReturnTypeAnnotation(children: any[]): string | null {
    const arrowIndex = children.findIndex((child: any) => child.text === '->');
    if (arrowIndex === -1 || arrowIndex + 1 >= children.length) {
        return null;
    }

    const returnTypeNode = children[arrowIndex + 1];
    return returnTypeNode.type === 'type' ? extractTypeString(returnTypeNode) : null;
}

function extractParameters(parametersNode: any): ParameterTypeInfo[] {
    const parameters: ParameterTypeInfo[] = [];
    for (const child of parametersNode.children) {
        switch (child.type) {
            case 'typed_parameter':
                parameters.push(extractParameterTypeInfo(child));
                break;
            case 'default_parameter':
                parameters.push(extractDefaultParameterTypeInfo(child));
                break;
            case 'identifier':
                // Untyped parameter
                parameters.push({ name: child.text, type: null, hasDefault: false });
                break;
        }
    }
    return parameters;
}

function extractParameterTypeInfo(node: any): ParameterTypeInfo {
    const nameNode = node.children.find((child: any) => child.type === 'identifier');
    const typeNode = node.children.find((child: any) => child.type === 'type');

    return {
        name: nameNode?.text || 'unknown',
        type: typeNode ? extractTypeString(typeNode) : null,
        hasDefault: false
    };
}

function extractDefaultParameterTypeInfo(node: any): ParameterTypeInfo {
    // Default parameters might have type annotations too
    const nameNode = node.children.find((child: any) => child.type === 'identifier');
    const typeNode = node.children.find((child: any) => child.type === 'type');

    return {
        name: nameNode?.text || 'unknown',
        type: typeNode ? extractTypeString(typeNode) : null,
        hasDefault: true
    };
}

function extractVariableTypeInfo(node: any, positions: PositionLookup): VariableTypeInfo | null {
    // Look for assignments with type annotations: x: int = 5
    const identifierNode = node.children.find((child: any) => child.type === 'identifier');
    const typeNode = node.children.find((child: any) => child.type === 'type');

    if (identifierNode && typeNode) {
        const position = positions.toPosition(node.startIndex);
        return {
            name: identifierNode.text,
            type: extractTypeString(typeNode),
            line: position.line
        };
    }

    return null;
}

function extractClassTypeInfo(node: any, positions: PositionLookup): ClassTypeInfo {
    const nameNode = node.children.find((child: any) => child.type === 'identifier');
    const argumentListNode = node.children.find((child: any) => child.type === 'argument_list');

    const baseClasses = extractBaseClasses(argumentListNode);
    const isTypedDict = baseClasses.includes('TypedDict');

    const blockNode = node.children.find((child: any) => child.type === 'block');
    const fields = isTypedDict ? extractTypedDictFields(blockNode, positions) : [];

    const position = positions.toPosition(node.startIndex);
    return {
        name: nameNode?.text || 'unknown',
        line: position.line,
        baseClasses,
        isTypedDict,
        fields
    };
}

function extractBaseClasses(argumentListNode: any): string[] {
    if (!argumentListNode) {
        return [];
    }

    return argumentListNode.children
        .filter((child: any) => child.type === 'identifier')
        .map((child: any) => child.text);
}

function extractTypedDictFields(blockNode: any, positions: PositionLookup): VariableTypeInfo[] {
    if (!blockNode) {
        return [];
    }

    const fields: VariableTypeInfo[] = [];
    for (const child of blockNode.children) {
        if (child.type !== 'expression_statement') {
            continue;
        }
        const assignment = child.children.find((grandchild: any) => grandchild.type === 'assignment');
        if (!assignment) {
            continue;
        }
        const fieldInfo = extractVariableTypeInfo(assignment, positions);
        if (fieldInfo) {
            fields.push(fieldInfo);
        }
    }
    return fields;
}

function extractImportInfo(node: any, positions: PositionLookup): ImportInfo {
    const line = positions.toPosition(node.startIndex).line;

    switch (node.type) {
        case 'import_statement':
            return extractPlainImportInfo(node, line);
        case 'import_from_statement':
            return extractFromImportInfo(node, line);
        default:
            return { module: '', items: [], line };
    }
}

function extractPlainImportInfo(node: any, line: number): ImportInfo {
    // import module1, module2
    const items = node.children
        .filter((child: any) => child.type === 'dotted_name' || child.type === 'identifier')
        .map((child: any) => child.text);

    return { module: items[0] || '', items, line };
}

function extractFromImportInfo(node: any, line: number): ImportInfo {
    // from module import item1, item2
    const fromIndex = node.children.findIndex((child: any) => child.text === 'from');
    const importIndex = node.children.findIndex((child: any) => child.text === 'import');

    if (fromIndex === -1 || importIndex === -1) {
        return { module: '', items: [], line };
    }

    const module = findImportModuleName(node.children, fromIndex, importIndex);
    const items = collectImportedItems(node.children, importIndex);

    return { module, items, line };
}

function findImportModuleName(children: any[], fromIndex: number, importIndex: number): string {
    for (let i = fromIndex + 1; i < importIndex; i++) {
        const child = children[i];
        if (child.type === 'dotted_name' || child.type === 'identifier') {
            return child.text;
        }
    }
    return '';
}

function collectImportedItems(children: any[], importIndex: number): string[] {
    const items: string[] = [];
    for (let i = importIndex + 1; i < children.length; i++) {
        const child = children[i];
        if (child.type === 'identifier') {
            items.push(child.text);
        }
    }
    return items;
}

function extractTypeString(typeNode: any): string {
    if (typeNode.type !== 'type' || typeNode.children.length !== 1) {
        return typeNode.text || 'unknown';
    }

    const child = typeNode.children[0];
    if (child.type === 'generic_type') {
        return extractGenericTypeString(child);
    }
    if (child.type === 'identifier') {
        return child.text;
    }

    return typeNode.text || 'unknown';
}

function extractGenericTypeString(genericTypeNode: any): string {
    const baseType = genericTypeNode.children.find((child: any) => child.type === 'identifier');
    const typeParameterNode = genericTypeNode.children.find((child: any) => child.type === 'type_parameter');

    if (baseType && typeParameterNode) {
        const params: string[] = [];
        for (const child of typeParameterNode.children) {
            if (child.type === 'type') {
                params.push(extractTypeString(child));
            }
        }
        return `${baseType.text}[${params.join(', ')}]`;
    }

    return genericTypeNode.text || 'unknown';
}
