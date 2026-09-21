module DocCommentBoundary

let largeFunction0 () =
    let v0 = 0
    let v1 = 1
    let v2 = 2
    let v3 = 3
    let v4 = 4
    let v5 = 5
    let v6 = 6
    let v7 = 7
    let v8 = 8
    let v9 = 9
    let v10 = 10
    let v11 = 11
    let v12 = 12
    let v13 = 13
    let v14 = 14
    let v15 = 15
    let v16 = 16
    let v17 = 17
    let v18 = 18
    let v19 = 19
    let v20 = 20
    let v21 = 21
    v0

let largeFunction1 () =
    let v0 = 0
    let v1 = 1
    let v2 = 2
    let v3 = 3
    let v4 = 4
    let v5 = 5
    let v6 = 6
    let v7 = 7
    let v8 = 8
    let v9 = 9
    let v10 = 10
    let v11 = 11
    let v12 = 12
    let v13 = 13
    let v14 = 14
    let v15 = 15
    let v16 = 16
    let v17 = 17
    let v18 = 18
    let v19 = 19
    let v20 = 20
    let v21 = 21
    v0

let largeFunction2 () =
    let v0 = 0
    let v1 = 1
    let v2 = 2
    let v3 = 3
    let v4 = 4
    let v5 = 5
    let v6 = 6
    let v7 = 7
    let v8 = 8
    let v9 = 9
    let v10 = 10
    let v11 = 11
    let v12 = 12
    let v13 = 13
    let v14 = 14
    let v15 = 15
    let v16 = 16
    let v17 = 17
    let v18 = 18
    let v19 = 19
    let v20 = 20
    let v21 = 21
    v0

let largeFunction3 () =
    let v0 = 0
    let v1 = 1
    let v2 = 2
    let v3 = 3
    let v4 = 4
    let v5 = 5
    let v6 = 6
    let v7 = 7
    let v8 = 8
    let v9 = 9
    let v10 = 10
    let v11 = 11
    let v12 = 12
    let v13 = 13
    let v14 = 14
    let v15 = 15
    let v16 = 16
    let v17 = 17
    let v18 = 18
    let v19 = 19
    let v20 = 20
    let v21 = 21
    v0

let smallFunction () =
    let v0 = 0
    let v1 = 1
    let v2 = 2
    let v3 = 3
    let v4 = 4
    let v5 = 5
    let v6 = 6
    let v7 = 7
    let v8 = 8
    let v9 = 9
    let v10 = 10
    let v11 = 11
    let v12 = 12
    let v13 = 13
    let v14 = 14
    let v15 = 15
    v0

/// largeFunction5 is a fifth large function. The tree-sitter-fsharp grammar attaches this doc block
/// to the preceding declaration_expression, so counting it against smallFunction would push that
/// 18-line function past the 20-line large-function threshold and add a spurious sixth large
/// function. Function line counts must exclude the following binding's documentation.
let largeFunction5 () =
    let v0 = 0
    let v1 = 1
    let v2 = 2
    let v3 = 3
    let v4 = 4
    let v5 = 5
    let v6 = 6
    let v7 = 7
    let v8 = 8
    let v9 = 9
    let v10 = 10
    let v11 = 11
    let v12 = 12
    let v13 = 13
    let v14 = 14
    let v15 = 15
    let v16 = 16
    let v17 = 17
    let v18 = 18
    let v19 = 19
    let v20 = 20
    let v21 = 21
    v0
