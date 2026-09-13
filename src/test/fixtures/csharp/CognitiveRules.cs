class CognitiveRules {
// straightLine: cognitive score 0.
static bool straightLine(bool ready, bool enabled, bool urgent) {
    return ready;
}

// booleanRun: cognitive score 1.
static bool booleanRun(bool ready, bool enabled, bool urgent) {
    return ready && enabled && urgent;
}

// mixedRuns: cognitive score 3.
static bool mixedRuns(bool ready, bool enabled, bool urgent) {
    return ready && enabled || urgent && ready;
}

// groupedRun: cognitive score 1.
static bool groupedRun(bool ready, bool enabled, bool urgent) {
    return ready && (enabled && urgent);
}

// negatedGroup: cognitive score 2.
static bool negatedGroup(bool ready, bool enabled, bool urgent) {
    return ready && !(enabled && urgent);
}

// plainElse: cognitive score 2.
static void plainElse(bool ready, bool enabled, bool urgent) {
    if (ready) { Deliver(); } else { Queue(); }
}

// flatChain: cognitive score 3.
static void flatChain(bool ready, bool enabled, bool urgent) {
    if (ready) { Deliver(); } else if (urgent) { Expedite(); } else { Queue(); }
}

// nestedChain: cognitive score 4.
static void nestedChain(bool ready, bool enabled, bool urgent) {
    if (enabled) { if (ready) { Deliver(); } else if (urgent) { Expedite(); } }
}

// nestedChecks: cognitive score 3.
static void nestedChecks(bool ready, bool enabled, bool urgent) {
    if (enabled) { if (ready) { Deliver(); } }
}

// dispatch: cognitive score 3.
static void dispatch(bool ready, bool enabled, bool urgent) {
    switch (ready) { case true: if (enabled) Deliver(); break; default: Queue(); break; }
}

// loopBody: cognitive score 3.
static void loopBody(bool ready, bool enabled, bool urgent) {
    while (ready) { if (enabled) Deliver(); }
}

// lambdaBody: cognitive score 2.
static void lambdaBody(bool ready, bool enabled, bool urgent) {
    System.Action action = () => { if (ready) Deliver(); }; action();
}

// emptyLambda: cognitive score 0.
static void emptyLambda(bool ready, bool enabled, bool urgent) {
    System.Action action = () => Deliver(); action();
}

// guardReturn: cognitive score 1.
static void guardReturn(bool ready, bool enabled, bool urgent) {
    if (!ready) return; Deliver();
}

// unbracedChecks: cognitive score 3.
static void unbracedChecks(bool ready, bool enabled, bool urgent) {
    if (enabled) if (ready) Deliver();
}

// doLoop: cognitive score 3.
static void doLoop(bool ready, bool enabled, bool urgent) {
    do { if (ready) Deliver(); } while (enabled);
}

// nestedFunction: cognitive score 2.
static void nestedFunction(bool ready, bool enabled, bool urgent) {
    Audit(); void Action() { if (ready) Deliver(); } Action();
}

// emptyFunction: cognitive score 0.
static void emptyFunction(bool ready, bool enabled, bool urgent) {
    Audit(); void Action() { Deliver(); } Action();
}

// cleanup: cognitive score 0.
static void cleanup(bool ready, bool enabled, bool urgent) {
    try { Deliver(); } finally { Cleanup(); }
}

// catches: cognitive score 4.
static void catches(bool ready, bool enabled, bool urgent) {
    try { Deliver(); } catch (TimeoutException error) { if (ready) Queue(); } catch (ArgumentException error) { Reject(); }
}

// labelJump: cognitive score 1.
static void labelJump(bool ready, bool enabled, bool urgent) {
    goto finished; finished: Deliver();
}

// ordinaryBreak: cognitive score 1.
static void ordinaryBreak(bool ready, bool enabled, bool urgent) {
    while (ready) { break; }
}

// bracedElseCheck: cognitive score 4.
static void bracedElseCheck(bool ready, bool enabled, bool urgent) {
    if (enabled) { Deliver(); } else { if (ready) { Deliver(); } }
}
}
