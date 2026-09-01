module Energy.Core.Paths

open Fable.Core

/// A file or directory path for the host filesystem's `node:fs`/`node:path` API.
///
/// `Path` is erased to its backing string in generated JavaScript, so binding calls preserve the
/// prior runtime representation while callers can no longer transpose a path with another string
/// parameter.
///
/// decision: uses an erased single-case union (the Core.TreeSitter.NodeType pattern) so the F#
/// type checker catches transposed path/encoding arguments without changing the JavaScript API.
/// invariant: every `Path` value has exactly its wrapped string as its JavaScript representation.
[<Erase>]
type Path = Path of string

/// A text encoding name for file reads (e.g. `utf8`).
///
/// decision: typed rather than left as a raw string so a read encoding can no longer be confused
/// with the path it reads.
/// invariant: every `Encoding` value has exactly its wrapped string as its JavaScript
/// representation.
[<Erase>]
type Encoding = Encoding of string
