class RequiredFollowupExample
{
    // clean — not flagged by inversion feedback
    static void CleanRequiredFollowup(bool a, bool b, bool c, bool d)
    {
        if (a) {
            if (b) {
                process();
            }
        }
        finish();
    }
}


class DominantFollowupExample
{
    // clean — not flagged by inversion feedback
    static void CleanDominantFollowup(bool a, bool b, bool c, bool d)
    {
        if (a) {
            recordAttempt();
            processRequest();
            recordResult();
        }
        finish();
    }
}


class InterveningWorkExample
{
    // clean — not flagged by inversion feedback
    static int CleanInterveningWork(bool a, bool b, bool c, bool d)
    {
        prepare();
        if (a) {
            recordAttempt();
            if (b) {
                process();
            }
            recordResult();
        }
        return 0;
    }
}


class AlternativeBranchExample
{
    // clean — not flagged by inversion feedback
    static int CleanAlternativeBranch(bool a, bool b, bool c, bool d)
    {
        if (a) {
            if (b) {
                return 1;
            }
        } else {
            return 2;
        }
        return 0;
    }
}


class FlatAlternativesExample
{
    // clean — not flagged by inversion feedback
    static int CleanFlatAlternatives(bool a, bool b, bool c, bool d)
    {
        if (a) {
            return 1;
        } else if (b) {
            return 2;
        } else if (c) {
            return 3;
        } else if (d) {
            return 4;
        } else {
            return 0;
        }
    }
}


class TwoLevelsExample
{
    // clean — not flagged by inversion feedback
    static int CleanTwoLevels(bool a, bool b, bool c, bool d)
    {
        prepare();
        if (a) {
            if (b) {
                process();
            }
        }
        return 0;
    }
}


class ThreeLevelsExample
{
    // flagged — inversion feedback (medium)
    static int FlaggedThreeLevels(bool a, bool b, bool c, bool d)
    {
        prepare();
        if (a) {
            if (b) {
                if (c) {
                    process();
                }
            }
        }
        return 0;
    }
}


class FourLevelsExample
{
    // flagged — inversion feedback (medium)
    static int FlaggedFourLevels(bool a, bool b, bool c, bool d)
    {
        prepare();
        if (a) {
            if (b) {
                if (c) {
                    if (d) {
                        process();
                    }
                }
            }
        }
        return 0;
    }
}


class FiveGuardsExample
{
    // flagged — inversion feedback (medium)
    static int FlaggedFiveGuards(bool a, bool b, bool c, bool d)
    {
        if (a) {
            if (b) {
                if (c) {
                    if (d) {
                        if (a) {
                            return 1;
                        }
                    }
                }
            }
        }
        return 0;
    }
}


class NestedElseExample
{
    // flagged — inversion feedback (medium)
    static int FlaggedNestedElse(bool a, bool b, bool c, bool d)
    {
        if (a) {
            return 1;
        } else {
            if (b) {
                return 2;
            } else {
                if (c) {
                    return 3;
                }
            }
        }
        return 0;
    }
}


class ImplicitFallthroughExample
{
    // flagged — inversion feedback (medium)
    static void FlaggedImplicitFallthrough(bool a, bool b, bool c, bool d)
    {
        if (a) {
            if (b) {
                process();
            }
        }
    }
}


class CommentHeavyBlockExample
{
    // clean — not flagged by inversion feedback
    static int CleanCommentHeavyBlock(bool a, bool b, bool c, bool d)
    {
        if (a) {
            /* This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block. This long explanation documents why processing is conditional and should not make a two-statement branch count as a large block.  */
            process();
            return 1;
        }
        return 0;
    }
}


class NestedFunctionExample
{
    // clean — not flagged by inversion feedback
    static void CleanNestedFunction(bool a, bool b, bool c, bool d)
    {
        if (a) {
            if (b) {
                void Inner() {
                    if (c) { process(); }
                }
                Inner();
            }
        }
        finish();
    }
}


class AlternativeBodyExample
{
    // flagged — inversion feedback (medium)
    static int FlaggedAlternativeBody(bool a, bool b, bool c, bool d)
    {
        if (a) {
            return 1;
        } else if (b) {
            if (c) {
                if (d) { process(); }
            }
        }
        return 0;
    }
}


class UnbracedElseExample
{
    // clean — not flagged by inversion feedback
    static int CleanUnbracedElse(bool a, bool b, bool c, bool d)
    {
        if (a) {
            if (b) { return 1; }
        } else return 2;
        return 0;
    }
}
