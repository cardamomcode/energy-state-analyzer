module Energy.Core.Detectors.ClassRelatedness

open System.Collections.Generic

open Energy.Core.TreeSitter
open Energy.Core.Position
open Energy.Core.LanguageAdapter
open Energy.Core.Violation
open Energy.Core.NamingCohesion
open Energy.Core.TypeCohesion

// Port of src/core/detectors/classRelatedness.ts — the class side of the file-coherence detector.
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
