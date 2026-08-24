module LogicalControlFlow

let cleanExplicitIf (isLoggedIn: bool) =
    if isLoggedIn then
        navToDashboard ()

let notFlaggedAndAsIf (isLoggedIn: bool) =
    isLoggedIn && navToDashboard ()
