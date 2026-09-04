def cleanExplicitIf(is_logged_in):
    if is_logged_in:
        nav_to_dashboard()


def flaggedAndAsIf(is_logged_in):
    is_logged_in and nav_to_dashboard()


def flaggedOrAsUnless(is_logged_in):
    is_logged_in or nav_to_dashboard()
