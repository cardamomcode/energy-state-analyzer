module Energy.Core.Detectors.TestFile

// decision: shared test-file recognition used by both magic detectors. Recognizes test files by
// path segment (test/, tests/) and camel-case word boundaries in the filename stem rather than a
// substring search — names such as latest_pricing.py must still be analyzed.
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

        match splitIntoWords stem |> List.map _.ToLowerInvariant() with
        | first :: _ when first = "test" -> true
        | words -> words |> List.tryLast = Some "test"
