# decision: an exceptions.py-style file - several classes that never reference each other and
# share no naming affix at all (ValidationError/ParseError share a suffix, TimeoutFailure
# deliberately doesn't), but all extend the same base (Exception, not itself defined in this
# file). Must NOT be flagged: shared inheritance from a common base is itself a real cohesion
# signal, independent of naming.


class ValidationError(Exception):
    pass


class ParseError(Exception):
    pass


class TimeoutFailure(Exception):
    pass
