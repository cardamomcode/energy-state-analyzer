module NarrowImports

// Redundant repeated opens of the same module - contrived, but exercises the same
// "many import lines, one real dependency" shape the other languages hit via
// multi-symbol imports from one package. F# has no per-symbol import syntax, so
// there's no more natural way to draw 11 lines from a single module.
open Pkg
open Pkg
open Pkg
open Pkg
open Pkg
open Pkg
open Pkg
open Pkg
open Pkg
open Pkg
open Pkg

let doSomething () =
    1
