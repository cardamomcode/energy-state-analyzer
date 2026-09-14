// clean — not flagged by logical control flow
fun cleanExplicitIf(isLoggedIn: Boolean) {
    if (isLoggedIn) {
        navToDashboard()
    }
}

// clean — logical control flow shorthand (cond && action / cond || action) not detected in Kotlin; grammar limitation
fun flaggedAndAsIf(isLoggedIn: Boolean) {
    isLoggedIn && navToDashboard()
}

// clean — logical control flow shorthand (cond && action / cond || action) not detected in Kotlin; grammar limitation
fun flaggedOrAsUnless(isLoggedIn: Boolean) {
    isLoggedIn || navToDashboard()
}
