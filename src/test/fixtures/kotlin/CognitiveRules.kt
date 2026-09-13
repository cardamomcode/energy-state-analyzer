// straightLine: cognitive score 0.
fun straightLine(ready: Boolean, enabled: Boolean, urgent: Boolean): Boolean {
    return ready
}

// booleanRun: cognitive score 1.
fun booleanRun(ready: Boolean, enabled: Boolean, urgent: Boolean): Boolean {
    return ready && enabled && urgent
}

// mixedRuns: cognitive score 3.
fun mixedRuns(ready: Boolean, enabled: Boolean, urgent: Boolean): Boolean {
    return ready && enabled || urgent && ready
}

// groupedRun: cognitive score 1.
fun groupedRun(ready: Boolean, enabled: Boolean, urgent: Boolean): Boolean {
    return ready && (enabled && urgent)
}

// negatedGroup: cognitive score 2.
fun negatedGroup(ready: Boolean, enabled: Boolean, urgent: Boolean): Boolean {
    return ready && !(enabled && urgent)
}

// plainElse: cognitive score 2.
fun plainElse(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    if (ready) { deliver() } else { queue() }
}

// flatChain: cognitive score 3.
fun flatChain(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    if (ready) { deliver() } else if (urgent) { expedite() } else { queue() }
}

// nestedChain: cognitive score 4.
fun nestedChain(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    if (enabled) { if (ready) { deliver() } else if (urgent) { expedite() } }
}

// nestedChecks: cognitive score 3.
fun nestedChecks(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    if (enabled) { if (ready) { deliver() } }
}

// dispatch: cognitive score 3.
fun dispatch(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    when (ready) { true -> if (enabled) deliver(); false -> queue() }
}

// loopBody: cognitive score 3.
fun loopBody(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    while (ready) { if (enabled) deliver() }
}

// lambdaBody: cognitive score 2.
fun lambdaBody(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    val action = { if (ready) deliver() }; action()
}

// emptyLambda: cognitive score 0.
fun emptyLambda(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    val action = { deliver() }; action()
}

// guardReturn: cognitive score 1.
fun guardReturn(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    if (!ready) return
    deliver()
}

// unbracedChecks: cognitive score 3.
fun unbracedChecks(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    if (enabled) if (ready) deliver();
}

// doLoop: cognitive score 3.
fun doLoop(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    do { if (ready) deliver(); } while (enabled);
}

// nestedFunction: cognitive score 2.
fun nestedFunction(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    audit(); fun action() { if (ready) deliver() }; action()
}

// emptyFunction: cognitive score 0.
fun emptyFunction(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    audit(); fun action() { deliver() }; action()
}

// cleanup: cognitive score 0.
fun cleanup(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    try { deliver() } finally { cleanup() }
}

// catches: cognitive score 4.
fun catches(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    try { deliver() } catch (error: TimeoutException) { if (ready) queue() } catch (error: IllegalArgumentException) { reject() }
}

// labelJump: cognitive score 2.
fun labelJump(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    dispatch@ while (ready) { break@dispatch }
}

// labelContinue: cognitive score 2.
fun labelContinue(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    dispatch@ while (ready) { continue@dispatch }
}

// labelReturn: cognitive score 1.
fun labelReturn(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    dispatch@ while (ready) { return@dispatch }
}

// ordinaryBreak: cognitive score 1.
fun ordinaryBreak(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    while (ready) { break }
}

// bracedElseCheck: cognitive score 4.
fun bracedElseCheck(ready: Boolean, enabled: Boolean, urgent: Boolean): Unit {
    if (enabled) { deliver(); } else { if (ready) { deliver(); } }
}
