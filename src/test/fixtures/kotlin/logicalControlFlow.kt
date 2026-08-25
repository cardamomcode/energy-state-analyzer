fun cleanExplicitIf(isLoggedIn: Boolean) {
    if (isLoggedIn) {
        navToDashboard()
    }
}

fun flaggedAndAsIf(isLoggedIn: Boolean) {
    isLoggedIn && navToDashboard()
}

fun flaggedOrAsUnless(isLoggedIn: Boolean) {
    isLoggedIn || navToDashboard()
}
