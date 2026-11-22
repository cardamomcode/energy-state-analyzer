-module(magic_string).

flagged_case_pattern(Event) ->
    case Event of
        "started" -> 1;
        {"started", _} -> 2;
        _ -> 0
    end.

clean_case_result(Event) ->
    case Event of
        "opened" -> "result";
        _ -> "result"
    end.
