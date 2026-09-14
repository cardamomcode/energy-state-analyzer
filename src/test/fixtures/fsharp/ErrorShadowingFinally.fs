module ErrorShadowingFinally

// clean — not flagged by recovery dominance
let cleanupOnly () =
    prepareWorkspace ()

    try
        performBusinessWork ()
        completeBusinessWork ()
    finally
        cleanUpResources ()
