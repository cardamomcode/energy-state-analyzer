module LogicalControlFlow

// clean — not flagged by logical control flow
let cleanExplicitIf (isLoggedIn: bool) =
    if isLoggedIn then
        navToDashboard ()

// clean — not flagged by logical control flow
let notFlaggedAndAsIf (isLoggedIn: bool) = isLoggedIn && navToDashboard ()
