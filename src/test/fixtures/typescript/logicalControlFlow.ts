// clean — not flagged by logical control flow
function cleanExplicitIf(isLoggedIn: boolean) {
    if (isLoggedIn) {
        navToDashboard();
    }
}

// flagged — logical control flow (low)
function flaggedAndAsIf(isLoggedIn: boolean) {
    isLoggedIn && navToDashboard();
}

// flagged — logical control flow (low)
function flaggedOrAsUnless(isLoggedIn: boolean) {
    isLoggedIn || navToDashboard();
}
