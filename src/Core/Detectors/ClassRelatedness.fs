module Energy.Core.Detectors.ClassRelatedness

open System.Collections.Generic

open Energy.Core.TreeSitter
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Violation
open Energy.Core.NamingCohesion
open Energy.Core.TypeCohesion

// Class side of the file-coherence detector.
//
// checkFunctionCountSprawl's signal is "too many unrelated *functions* in one file"; this is its
// class-level counterpart: "several small, internally-cohesive *classes* that don't belong together
// in the same file". It groups a file's classes into families using three independent signals, then
// flags the file when those families can't be unified by a naming affix.

// A class defined in the file, along with the methods nested directly (or transitively, through
// non-class nesting like a method's own closures) inside it. `Methods` is mutable to mirror the TS
// array that methods are pushed into during collection; read as a sequence elsewhere.
type ClassInfo =
    { Name: string option
      Node: Node
      BaseNames: string list
      mutable Methods: ResizeArray<Node> }

// decision: a branded alias, not a bare `number` — two adjacent bare-`number` parameters would
// themselves trip this project's own primitive-obsession swap-risk check (see primitiveObsession.ts),
// and a union-find over array positions is exactly that shape.
type ClassIndex = int

// decision: a tiny local union-find over the file's classes, not a general graph library — the only
// operation needed is "merge these two classes' families" then "list the resulting families", which a
// parent-pointer array covers in a few lines.
type private UnionFind =
    { Find: ClassIndex -> ClassIndex
      Union: ClassIndex -> ClassIndex -> unit }

let private unionFind (size: int) : UnionFind =
    let parent = Array.init size (fun i -> i)

    let rec find (i: ClassIndex) : ClassIndex =
        if parent.[i] <> i then
            // path halving: point this node at its grandparent, then keep climbing.
            parent.[i] <- parent.[parent.[i]]
            find parent.[i]
        else
            i

    { Find = find
      Union =
        fun a b ->
            let rootA = find a
            let rootB = find b

            if rootA <> rootB then
                parent.[rootA] <- rootB }

// Links two classes directly — one's base name is literally the other's own name (e.g. `class
// CancellationToken(Disposable)` where `Disposable` is itself another class in this file).
let private linkDirectInheritance (classes: ClassInfo list) (names: string option list) (uf: UnionFind) =
    classes
    |> List.iteri (fun i cls ->
        for baseName in cls.BaseNames do
            match
                names
                |> List.tryFindIndex (fun n -> n |> Option.exists (fun nn -> nn = baseName))
            with
            | Some baseIndex -> uf.Union i baseIndex
            | None -> ())

// Links sibling classes that share a base name in common, even one not itself defined in this file at
// all (e.g. a whole file of exception classes that all extend `Exception` but never reference each
// other).
let private linkSharedBase (classes: ClassInfo list) (uf: UnionFind) =
    let indicesByBaseName = Dictionary<string, ResizeArray<int>>()

    classes
    |> List.iteri (fun i cls ->
        for baseName in cls.BaseNames do
            let group =
                if indicesByBaseName.ContainsKey baseName then
                    indicesByBaseName.[baseName]
                else
                    let newGroup = ResizeArray<int>()

                    indicesByBaseName.[baseName] <- newGroup
                    newGroup

            group.Add(i))

    for group in indicesByBaseName.Values do
        // union the first index of each shared-base group with every subsequent one.
        for k in 1 .. group.Count - 1 do
            uf.Union group.[0] group.[k]

// Links two classes whenever a method's signature (via collectTypeSignals, the same signal
// checkFunctionCountSprawl's type cohesion uses) touches another class defined in the file, as with a
// token/token-source pair where one constructs or returns the other.
let private linkTypeCrossReference
    (classes: ClassInfo list)
    (names: string option list)
    (language: LanguageAdapter)
    (uf: UnionFind)
    =
    classes
    |> List.indexed
    |> List.collect (fun (classIndex, cls) ->
        cls.Methods
        |> Seq.toList
        |> List.collect (fun method ->
            collectTypeSignals method language
            |> Seq.toList
            |> List.choose (fun typeName ->
                names
                |> List.tryFindIndex (fun name -> name |> Option.exists ((=) typeName))
                |> Option.filter ((<>) classIndex)
                |> Option.map (fun relatedIndex -> classIndex, relatedIndex))))
    |> List.iter (fun (classIndex, relatedIndex) -> uf.Union classIndex relatedIndex)

// Groups the file's classes into families using three independent signals, checked in this order
// because each is progressively weaker evidence: (1) direct inheritance, (2) a shared base class,
// (3) a type cross-reference between method signatures.
let private groupClassesIntoFamilies (classes: ClassInfo list) (language: LanguageAdapter) : int list list =
    let names = classes |> List.map (fun c -> c.Name)
    let uf = unionFind classes.Length

    linkDirectInheritance classes names uf
    linkSharedBase classes uf
    linkTypeCrossReference classes names language uf

    classes
    |> List.indexed
    |> List.groupBy (fun (index, _) -> uf.Find index)
    |> List.map (fun (_, members) -> members |> List.map fst)

let private namesHaveSharedDomain (classes: ClassInfo list) (singleDomainNameShare: float) =
    let names = classes |> List.map (fun c -> c.Name)
    let definiteNames = names |> List.choose id

    definiteNames.Length = names.Length
    && looksLikeSingleDomainByNames definiteNames singleDomainNameShare

let private groupDescription (groups: int list list) (names: string option list) =
    groups
    |> List.map (List.map (fun index -> Option.defaultValue "(anonymous)" names.[index]))
    |> List.sortByDescending List.length
    |> List.map (String.concat ", " >> sprintf "{%s}")
    |> String.concat " vs "

// Flag a file whose classes split into multiple families with no relationship to each other — the
// class-level counterpart to checkFunctionCountSprawl's "unrelated types" message (coherence.ts), but
// for a different shape of sprawl: several small, internally-cohesive classes that don't belong
// together in the same file.
//
// decision: unlike checkFunctionCountSprawl, this has no minimum class count before it can fire — a
// class is already a much stronger unit of cohesion than a single function (it's a whole type, not one
// operation), so two totally unrelated classes are worth flagging even at just 2. If the three signals
// still leave more than one family, a naming-affix fallback (shared prefix or suffix across class
// names, same mechanism as looksLikeSingleDomain for functions) gets one last chance to unify the
// whole file before it's flagged — unlike the function-level type-diversity signal, an unconnected
// class graph is an absence of positive evidence, not a positive diversity measurement, so it's not
// treated as authoritative over naming the way checkFunctionCountSprawl's type signal is. Takes only
// the one threshold it needs (singleDomainNameShare) rather than the whole CoherenceThresholds object
// — that type lives in coherence.ts, which itself needs ClassInfo and checkClassRelatedness from this
// file; importing CoherenceThresholds back here would make the two files circularly dependent for no
// reason beyond convenience.
let checkClassRelatedness
    (classes: ClassInfo list)
    (singleDomainNameShare: float)
    (language: LanguageAdapter)
    (positions: PositionLookup)
    : EnergyViolation option =
    if classes.Length < 2 then
        None
    else
        let groups = groupClassesIntoFamilies classes language
        let names = classes |> List.map (fun c -> c.Name)

        match groups with
        | [ _ ] -> None
        | _ when namesHaveSharedDomain classes singleDomainNameShare -> None
        | _ ->
            let position = positions.toPosition (nodeStartIndex classes.[0].Node)

            Some
                { Line = position.Line
                  Column = position.Column
                  Type = Coherence
                  Severity = if groups.Length > 2 then High else Medium
                  Message =
                    sprintf
                        "File coherence warning: %d classes in one file split into %d unrelated groups: %s. These share no inheritance, type relationship, or naming pattern — each group likely belongs in its own file."
                        classes.Length
                        groups.Length
                        (groupDescription groups names)
                  Hotspots = [] }

// God-class side of the file-coherence detector.
//
// checkFunctionCountSprawl's signal is "too many unrelated *functions* in one file";
// checkClassRelatedness's is "several unrelated *classes* in one file". This is the third angle:
// "one class carrying too many *unrelated responsibilities*" — a single type whose methods touch a
// wide set of unrelated domain types. It reuses coherence's type-diversity measurement, but with the
// logic inverted: there, cohesion *exempts* a file from flagging; here, diversity *triggers* the flag
// on a single type.
//
// decision: this emits ViolationType.Coherence rather than its own case — it is the class-level half
// of the same single-responsibility concern, so inventing a new wire string and a new presentation
// branch would duplicate work for no gain (DecorationModel already renders Coherence as a full-line
// highlight). AGENTS.md says add a ViolationType case plus special presentation only when required;
// here it isn't.
//
// decision: a stateless value type with a rich but cohesive API — an Option/Result-style tagged union
// of pure combinators where every method transforms one domain type — is NOT a god class. Its type
// diversity ratio stays low, so this check skips it regardless of method count. Only a class whose
// methods are genuinely diverse across unrelated types fires, which is the "too many responsibilities"
// signal at type granularity. This mirrors coherence's own rejection of count-only heuristics (the
// entropy-dump message notes type-diversity alone isn't reliable below ~12 functions).

// decision: method-count bars come from CoherenceThresholds (MethodCountMedium/High) so a project can
// retune where a single type's responsibility sprawl is flagged without editing this detector, exactly
// like checkFunctionCountSprawl's configurable 8/12/15. The cohesion gate still reuses the existing
// CoherenceThresholds (MaxTypeDiversityRatio/MinTypedCoverage); both live on GodClassCtx.Thresholds.

// decision: carries the per-analysis context considerGodClass needs as one record so its signature stays
// short — threading three separate arguments would push that function past the 20-line large-function bar.
// Public because checkGodClass exposes it in its signature; Coherence.fs builds this record at the call site.
type GodClassCtx =
    { Language: LanguageAdapter
      Thresholds: Energy.Core.Config.CoherenceThresholds
      Positions: PositionLookup }

// decision: branded signals so the two int counts can't be silently transposed at the call site — this
// project's own primitive-obsession detector flags adjacent bare-int params as a swap-risk pair.
type private MethodSignals =
    { MethodCount: int; DistinctTypes: int }

// A class that passed the god-class test, carrying what Create renders into a violation.
type private GodClassCandidate =
    { Class: ClassInfo
      Signals: MethodSignals
      Severity: Severity }
    // decision: rendering lives on the record as a static Create so the violation shape stays close to the
    // data it describes; positions is supplied by the caller, never stored in the candidate.
    static member Create(positions: PositionLookup) : GodClassCandidate -> EnergyViolation =
        fun candidate ->
            let pos = positions.toPosition (nodeStartIndex candidate.Class.Node)

            { Line = pos.Line
              Column = pos.Column
              Type = Coherence
              Severity = candidate.Severity
              Message =
                sprintf
                    "File coherence warning: this class has %d methods spanning %d unrelated types. Its methods touch too many distinct concerns to be one responsibility — consider splitting it, or confirm these are one cohesive API (e.g. a tagged union of combinators over a single domain type)."
                    candidate.Signals.MethodCount
                    candidate.Signals.DistinctTypes
              Hotspots = [] }

// decision: a class must exceed the medium bar and contain at least one instance method before
// type diversity can represent competing object responsibilities; all-static classes are function
// namespaces and belong to neither god-class scoring nor free-function sprawl.
let private shouldMeasureGodClass (ctx: GodClassCtx) (methods: Node list) : bool =
    methods.Length > ctx.Thresholds.MethodCountMedium
    && (methods |> List.exists (ctx.Language.IsStaticMethod >> not))

// Returns the method/distinct-type counts when a class past the method-count bar is genuinely diverse
// (non-cohesive); None otherwise, so cohesive value types and under-typed files stay quiet. Split out so
// considerGodClass stays short — this match alone would push it past the 20-line large-function bar.
let private distinctSignals (ctx: GodClassCtx) (cls: ClassInfo) : (int * int) option =
    let methods = List.ofSeq cls.Methods
    // decision: pass the cohesion thresholds as one value so the typeCohesionResult call stays a single line.
    let thresholds =
        { MaxDiversityRatio = ctx.Thresholds.MaxTypeDiversityRatio
          MinCoverage = ctx.Thresholds.MinTypedCoverage }

    if not (shouldMeasureGodClass ctx methods) then
        None
    else
        match typeCohesionResult methods ctx.Language thresholds with
        | Measured r when not r.Result -> Some(methods.Length, r.DistinctTypes)
        | _ -> None

// Decides whether one class is a god class: diverse classes past the method-count bar become candidates,
// tagged High only when they cross the higher bar. InsufficientData stays quiet rather than guessing.
let private considerGodClass (ctx: GodClassCtx) (cls: ClassInfo) : GodClassCandidate option =
    match distinctSignals ctx cls with
    | Some(methodCount, distinctTypes) ->
        Some
            { Class = cls
              Signals =
                { MethodCount = methodCount
                  DistinctTypes = distinctTypes }
              Severity =
                if methodCount > ctx.Thresholds.MethodCountHigh then
                    High
                else
                    Medium }
    | _ -> None

// Flag the worst single class whose methods span too many unrelated domain types (a god class), as one
// coherence violation anchored at that class's start, or None when every class is cohesive enough to be
// one coherent value/type. Takes its analysis context as one record so the signature stays a single line.
let checkGodClass (classes: ClassInfo list) (ctx: GodClassCtx) : EnergyViolation option =
    let candidates = classes |> List.choose (considerGodClass ctx)

    // Severity is monotonic in method count, so the worst class has the most methods.
    if List.isEmpty candidates then
        None
    else
        Some(
            candidates
            |> List.maxBy (fun c -> c.Signals.MethodCount)
            |> GodClassCandidate.Create ctx.Positions
        )
