// # FsPath
//
// This Node facade centralizes synchronous filesystem and platform path operations used by hosts,
// scanning, and tests. Path wrappers distinguish locations at F# call sites while emitted calls retain
// Node behavior, including throwing I/O failures. NodeInterop.fs converts selected failures into Result
// values; path calculations themselves do not read disk.
//
// Reading path: [Source guide](../../docs/source-guide.md).

module Energy.Core.FsPath

// Internal facade exposing the filesystem, path, and working-directory Node bindings used across
// scanning, hosts, and tests.
//
// decision: single internal facade for filesystem, path, and working-directory Node bindings shared
// by scanning, hosts, and tests. Fable's `[<Import>]` is the idiomatic way to bind a Node module, so
// this keeps every `node:fs`/`node:path` reference in one place instead of duplicating it per file.
// decision: path and encoding arguments are Core.Paths newtypes (erased to their backing strings)
// so callers can no longer transpose a path with another string at the binding boundary.

open Fable.Core
open Energy.Core.Paths

/// Read the process working directory used to resolve CLI inputs and report-relative paths.
[<Emit("process.cwd()")>]
let cwd () : string = nativeOnly

/// Check path existence through Node; false does not explain why the path is unavailable.
[<Import("existsSync", "node:fs")>]
let existsSync (path: Path) : bool = nativeOnly

/// Read a whole file with the requested encoding; filesystem failures throw unless a caller uses
/// NodeInterop's safe wrapper.
[<Import("readFileSync", "node:fs")>]
let readFileSync (path: Path) (encoding: Encoding) : string = nativeOnly

/// Raw Node directory reader passed to Scan's typed-entry binding.
[<Import("readdirSync", "node:fs")>]
let readdirSync: obj = nativeOnly

/// Read filesystem metadata, following symlinks; missing or inaccessible paths throw.
[<Import("statSync", "node:fs")>]
let statSync (path: Path) : obj = nativeOnly

/// Raw Node directory-creation function used by the recursive creation wrapper.
[<Import("mkdirSync", "node:fs")>]
let private mkdirSync: obj = nativeOnly

/// Create the directory and any missing parents using Node's recursive option; filesystem failures
/// propagate.
[<Emit("$0($1, { recursive: true })")>]
let private makeDirectory (maker: obj) (directory: Path) : unit = nativeOnly

/// Create a directory and any missing parents; succeeds when it already exists.
let ensureDirectory (directory: Path) = makeDirectory mkdirSync directory

/// Synchronously write text to a file, replacing existing contents; errors propagate and writes are not
/// atomic.
[<Import("writeFileSync", "node:fs")>]
let writeTextFile (path: Path) (contents: string) : unit = nativeOnly

/// Return the final path component using the host platform's path rules.
[<Import("basename", "node:path")>]
let basename (path: Path) : string = nativeOnly

/// Return Path-typed results so path-producing bindings feed the other bindings directly.
///
/// decision: path-producing bindings return Path so results flow straight into other bindings
/// (existsSync/readFileSync/joinPath) without a string round-trip at every call site.
[<Import("join", "node:path")>]
let joinPath (left: Path) (right: Path) : Path = nativeOnly

/// Calculate the path from one location to another using Node's platform-specific separators; neither path
/// is read from disk.
[<Import("relative", "node:path")>]
let relativePath (fromPath: Path) (toPath: Path) : string = nativeOnly

/// Return a path's parent directory without accessing the filesystem.
[<Import("dirname", "node:path")>]
let dirname (path: Path) : Path = nativeOnly

/// Resolve a path against the process working directory and normalize it without checking existence.
[<Import("resolve", "node:path")>]
let resolvePath (path: Path) : Path = nativeOnly

/// Host platform's path separator, used when normalizing report and ignore paths.
[<Import("sep", "node:path")>]
let pathSeparator: string = nativeOnly
