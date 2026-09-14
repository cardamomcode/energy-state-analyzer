module ErrorShadowingFinally

let cleanupOnly () =
    prepareWorkspace ()

    try
        performBusinessWork ()
        completeBusinessWork ()
    finally
        cleanUpResources ()
