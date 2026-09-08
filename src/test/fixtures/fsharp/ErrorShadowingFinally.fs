module ErrorShadowingFinally

let cleanupOnly () =
    try
        performBusinessWork ()
    finally
        cleanUpResources ()
