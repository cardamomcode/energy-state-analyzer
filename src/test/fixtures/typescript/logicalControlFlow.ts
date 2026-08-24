function cleanExplicitIf(isLoggedIn: boolean) {
    if (isLoggedIn) {
        navToDashboard();
    }
}

function flaggedAndAsIf(isLoggedIn: boolean) {
    isLoggedIn && navToDashboard();
}

function flaggedOrAsUnless(isLoggedIn: boolean) {
    isLoggedIn || navToDashboard();
}
