// straightLine: cognitive score 0.
auto straightLine(bool ready, bool enabled, bool urgent) {
    return ready;
}

// booleanRun: cognitive score 1.
auto booleanRun(bool ready, bool enabled, bool urgent) {
    return ready && enabled && urgent;
}

// mixedRuns: cognitive score 3.
auto mixedRuns(bool ready, bool enabled, bool urgent) {
    return ready && enabled || urgent && ready;
}

// groupedRun: cognitive score 1.
auto groupedRun(bool ready, bool enabled, bool urgent) {
    return ready && (enabled && urgent);
}

// negatedGroup: cognitive score 2.
auto negatedGroup(bool ready, bool enabled, bool urgent) {
    return ready && !(enabled && urgent);
}

// plainElse: cognitive score 2.
auto plainElse(bool ready, bool enabled, bool urgent) {
    if (ready) { deliver(); } else { queue(); }
}

// flatChain: cognitive score 3.
auto flatChain(bool ready, bool enabled, bool urgent) {
    if (ready) { deliver(); } else if (urgent) { expedite(); } else { queue(); }
}

// nestedChain: cognitive score 4.
auto nestedChain(bool ready, bool enabled, bool urgent) {
    if (enabled) { if (ready) { deliver(); } else if (urgent) { expedite(); } }
}

// nestedChecks: cognitive score 3.
auto nestedChecks(bool ready, bool enabled, bool urgent) {
    if (enabled) { if (ready) { deliver(); } }
}

// dispatch: cognitive score 3.
auto dispatch(bool ready, bool enabled, bool urgent) {
    switch (ready) { case true: if (enabled) deliver(); break; default: queue(); }
}

// loopBody: cognitive score 3.
auto loopBody(bool ready, bool enabled, bool urgent) {
    while (ready) { if (enabled) deliver(); }
}

// lambdaBody: cognitive score 2.
auto lambdaBody(bool ready, bool enabled, bool urgent) {
    auto action = [&]() { if (ready) deliver(); }; action();
}

// emptyLambda: cognitive score 0.
auto emptyLambda(bool ready, bool enabled, bool urgent) {
    auto action = [&]() { deliver(); }; action();
}

// guardReturn: cognitive score 1.
auto guardReturn(bool ready, bool enabled, bool urgent) {
    if (!ready) return; deliver();
}

// unbracedChecks: cognitive score 3.
auto unbracedChecks(bool ready, bool enabled, bool urgent) {
    if (enabled) if (ready) deliver();
}

// doLoop: cognitive score 3.
auto doLoop(bool ready, bool enabled, bool urgent) {
    do { if (ready) deliver(); } while (enabled);
}

// catches: cognitive score 4.
auto catches(bool ready, bool enabled, bool urgent) {
    try { deliver(); } catch (const TimeoutError& error) { if (ready) queue(); } catch (...) { reject(); }
}

// labelJump: cognitive score 1.
auto labelJump(bool ready, bool enabled, bool urgent) {
    goto finished; finished: deliver();
}

// ordinaryBreak: cognitive score 1.
auto ordinaryBreak(bool ready, bool enabled, bool urgent) {
    while (ready) { break; }
}

// bracedElseCheck: cognitive score 4.
auto bracedElseCheck(bool ready, bool enabled, bool urgent) {
    if (enabled) { deliver(); } else { if (ready) { deliver(); } }
}
