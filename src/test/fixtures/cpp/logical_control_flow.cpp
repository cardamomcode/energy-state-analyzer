void cleanExplicitIf(bool isLoggedIn) {
    if (isLoggedIn) {
        navToDashboard();
    }
}

void flaggedAndAsIf(bool isLoggedIn) {
    isLoggedIn and navToDashboard();
}

void flaggedOrAsUnless(bool isLoggedIn) {
    isLoggedIn or navToDashboard();
}
