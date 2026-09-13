// straightLine: cognitive score 0.
function straightLine(ready: boolean, enabled: boolean, urgent: boolean) {
    return ready;
}

// booleanRun: cognitive score 1.
function booleanRun(ready: boolean, enabled: boolean, urgent: boolean) {
    return ready && enabled && urgent;
}

// mixedRuns: cognitive score 3.
function mixedRuns(ready: boolean, enabled: boolean, urgent: boolean) {
    return ready && enabled || urgent && ready;
}

// groupedRun: cognitive score 1.
function groupedRun(ready: boolean, enabled: boolean, urgent: boolean) {
    return ready && (enabled && urgent);
}

// negatedGroup: cognitive score 2.
function negatedGroup(ready: boolean, enabled: boolean, urgent: boolean) {
    return ready && !(enabled && urgent);
}

// plainElse: cognitive score 2.
function plainElse(ready: boolean, enabled: boolean, urgent: boolean) {
    if (ready) { deliver(); } else { queue(); }
}

// flatChain: cognitive score 3.
function flatChain(ready: boolean, enabled: boolean, urgent: boolean) {
    if (ready) { deliver(); } else if (urgent) { expedite(); } else { queue(); }
}

// nestedChain: cognitive score 4.
function nestedChain(ready: boolean, enabled: boolean, urgent: boolean) {
    if (enabled) { if (ready) { deliver(); } else if (urgent) { expedite(); } }
}

// nestedChecks: cognitive score 3.
function nestedChecks(ready: boolean, enabled: boolean, urgent: boolean) {
    if (enabled) { if (ready) { deliver(); } }
}

// dispatch: cognitive score 3.
function dispatch(ready: boolean, enabled: boolean, urgent: boolean) {
    switch (ready) { case true: if (enabled) deliver(); break; default: queue(); }
}

// loopBody: cognitive score 3.
function loopBody(ready: boolean, enabled: boolean, urgent: boolean) {
    while (ready) { if (enabled) deliver(); }
}

// lambdaBody: cognitive score 2.
function lambdaBody(ready: boolean, enabled: boolean, urgent: boolean) {
    const action = () => { if (ready) deliver(); }; action();
}

// emptyLambda: cognitive score 0.
function emptyLambda(ready: boolean, enabled: boolean, urgent: boolean) {
    const action = () => deliver(); action();
}

// guardReturn: cognitive score 1.
function guardReturn(ready: boolean, enabled: boolean, urgent: boolean) {
    if (!ready) return; deliver();
}

// unbracedChecks: cognitive score 3.
function unbracedChecks(ready: boolean, enabled: boolean, urgent: boolean) {
    if (enabled) if (ready) deliver();
}

// doLoop: cognitive score 3.
function doLoop(ready: boolean, enabled: boolean, urgent: boolean) {
    do { if (ready) deliver(); } while (enabled);
}

// nestedFunction: cognitive score 2.
function nestedFunction(ready: boolean, enabled: boolean, urgent: boolean) {
    audit(); function action() { if (ready) deliver(); } action();
}

// emptyFunction: cognitive score 0.
function emptyFunction(ready: boolean, enabled: boolean, urgent: boolean) {
    audit(); function action() { deliver(); } action();
}

// cleanup: cognitive score 0.
function cleanup(ready: boolean, enabled: boolean, urgent: boolean) {
    try { deliver(); } finally { cleanup(); }
}

// catches: cognitive score 3.
function catches(ready: boolean, enabled: boolean, urgent: boolean) {
    try { deliver(); } catch (error) { if (ready) queue(); }
}

// labelJump: cognitive score 2.
function labelJump(ready: boolean, enabled: boolean, urgent: boolean) {
    dispatch: while (ready) { break dispatch; }
}

// ordinaryBreak: cognitive score 1.
function ordinaryBreak(ready: boolean, enabled: boolean, urgent: boolean) {
    while (ready) { break; }
}

// bracedElseCheck: cognitive score 4.
function bracedElseCheck(ready: boolean, enabled: boolean, urgent: boolean) {
    if (enabled) { deliver(); } else { if (ready) { deliver(); } }
}

// multilineRuns: cognitive score 2.
function multilineRuns(ready: boolean, enabled: boolean, urgent: boolean) {
    return ready
        && enabled
        || urgent;
}

// conditionalCondition: cognitive score 2.
function conditionalCondition(ready: boolean, enabled: boolean, urgent: boolean) {
    if (ready && enabled) deliver();
}

// booleanArgument: cognitive score 1.
function booleanArgument(ready: boolean, enabled: boolean, urgent: boolean) {
    record(ready && enabled);
}

// booleanAssignment: cognitive score 1.
function booleanAssignment(ready: boolean, enabled: boolean, urgent: boolean) {
    const allowed = ready && enabled; record(allowed);
}
