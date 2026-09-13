# straightLine: cognitive score 0.
def straightLine(ready, enabled, urgent):
    return ready

# booleanRun: cognitive score 1.
def booleanRun(ready, enabled, urgent):
    return ready and enabled and urgent

# mixedRuns: cognitive score 3.
def mixedRuns(ready, enabled, urgent):
    return ready and enabled or urgent and ready

# groupedRun: cognitive score 1.
def groupedRun(ready, enabled, urgent):
    return ready and (enabled and urgent)

# negatedGroup: cognitive score 2.
def negatedGroup(ready, enabled, urgent):
    return ready and not (enabled and urgent)

# plainElse: cognitive score 2.
def plainElse(ready, enabled, urgent):
    if ready:
        deliver()
    else:
        queue()

# flatChain: cognitive score 3.
def flatChain(ready, enabled, urgent):
    if ready:
        deliver()
    elif urgent:
        expedite()
    else:
        queue()

# nestedChain: cognitive score 4.
def nestedChain(ready, enabled, urgent):
    if enabled:
        if ready:
            deliver()
        elif urgent:
            expedite()

# nestedChecks: cognitive score 3.
def nestedChecks(ready, enabled, urgent):
    if enabled:
        if ready:
            deliver()

# dispatch: cognitive score 3.
def dispatch(ready, enabled, urgent):
    match ready:
        case True:
            if enabled: deliver()
        case False:
            queue()

# loopBody: cognitive score 3.
def loopBody(ready, enabled, urgent):
    while ready:
        if enabled: deliver()

# lambdaBody: cognitive score 2.
def lambdaBody(ready, enabled, urgent):
    action = lambda: deliver() if ready else queue()
    action()

# emptyLambda: cognitive score 0.
def emptyLambda(ready, enabled, urgent):
    action = lambda: deliver()
    action()

# guardReturn: cognitive score 1.
def guardReturn(ready, enabled, urgent):
    if not ready: return
    deliver()

# catches: cognitive score 4.
def catches(ready, enabled, urgent):
    try:
        deliver()
    except TimeoutError:
        if ready: queue()
    except ValueError:
        reject()

# cleanup: cognitive score 0.
def cleanup(ready, enabled, urgent):
    try:
        deliver()
    finally:
        cleanup()

# nestedFunction: cognitive score 2.
def nestedFunction(ready, enabled, urgent):
    audit()
    def action():
        if ready: deliver()
    action()

# emptyFunction: cognitive score 0.
def emptyFunction(ready, enabled, urgent):
    audit()
    def action():
        deliver()
    action()

# bracedElseCheck: cognitive score 4.
def bracedElseCheck(ready, enabled, urgent):
    if enabled:
        deliver()
    else:
        if ready:
            deliver()
