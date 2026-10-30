module Energy.Core.Detectors.TestFile

/// Split a filename into words on separators and camel-case boundaries for test-file recognition.
///
/// decision: shared test-file recognition used by literal detectors. Recognizes test files by
/// path segment (test/, tests/) and camel-case word boundaries in the filename stem rather than a
/// substring search — names such as latest_pricing.py must still be analyzed.
let private splitIntoWords (text: string) : string list =
    let _, separated =
        text
        |> Seq.fold
            (fun (previous, result) current ->
                let isCamelCaseBoundary =
                    previous
                    |> Option.exists (fun previous ->
                        (System.Char.IsLower previous || System.Char.IsDigit previous)
                        && System.Char.IsUpper current)

                let separator = if isCamelCaseBoundary then " " else ""
                Some current, result + separator + string<char> current)
            (None, "")

    separated.Split([| ' '; '_'; '-'; '.' |])
    |> Array.filter (System.String.IsNullOrWhiteSpace >> not)
    |> Array.toList

/// Recognize test directories and test words at either end of a filename stem.
/// Rust also uses the plural `tests` for test modules in separate files.
let isTestFile (fileName: string) : bool =
    let segments =
        fileName.Replace("\\", "/").Split('/')
        |> Array.filter (System.String.IsNullOrWhiteSpace >> not)

    let isTestDirectory (segment: string) =
        let normalized = segment.ToLowerInvariant()
        normalized = "test" || normalized = "tests"

    if segments |> Array.exists isTestDirectory then
        true
    else
        let baseName = segments |> Array.tryLast |> Option.defaultValue ""
        let extensionStart = baseName.LastIndexOf('.')

        let stem =
            if extensionStart > 0 then
                baseName.Substring(0, extensionStart)
            else
                baseName

        let isRustFile = baseName.EndsWith(".rs", System.StringComparison.OrdinalIgnoreCase)

        let isTestWord word =
            word = "test" || (isRustFile && word = "tests")

        match splitIntoWords stem |> List.map _.ToLowerInvariant() with
        | first :: _ when isTestWord first -> true
        | words -> words |> List.tryLast |> Option.exists isTestWord
