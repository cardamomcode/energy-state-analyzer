module ErrorShadowingFinally

let cleanupOnly () =
    prepareWorkspace ()

    try
        performBusinessWork ()
    finally
        cleanUpResources ()
