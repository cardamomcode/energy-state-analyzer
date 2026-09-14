# clean — not flagged by logical control flow
def cleanExplicitIf(is_logged_in):
    if is_logged_in:
        nav_to_dashboard()


# flagged — logical control flow (low)
def flaggedAndAsIf(is_logged_in):
    is_logged_in and nav_to_dashboard()


# flagged — logical control flow (low)
def flaggedOrAsUnless(is_logged_in):
    is_logged_in or nav_to_dashboard()
