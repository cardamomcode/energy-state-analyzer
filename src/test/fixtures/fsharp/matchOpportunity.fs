module MatchOpportunity

let cleanMixedConditions a b c =
    if a > 10 then 1
    elif b = "urgent" then 2
    elif c = None then 3
    else 0

let flaggedThreeWayChain status =
    if status = "open" then 1
    elif status = "closed" then 2
    elif status = "pending" then 3
    else 0
