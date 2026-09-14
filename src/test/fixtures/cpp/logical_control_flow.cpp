// clean — not flagged by logical control flow
void cleanExplicitIf(bool isLoggedIn) {
    if (isLoggedIn) {
        navToDashboard();
    }
}

// flagged — logical control flow (low)
void flaggedAndAsIf(bool isLoggedIn) {
    isLoggedIn and navToDashboard();
}

// flagged — logical control flow (low)
void flaggedOrAsUnless(bool isLoggedIn) {
    isLoggedIn or navToDashboard();
}
