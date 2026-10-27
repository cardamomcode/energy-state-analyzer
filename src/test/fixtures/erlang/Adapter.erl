-module(adapter).
-import(lists, [reverse/1]).

clean_values(Value) ->
    case Value of
        ready -> 0;
        _ -> 1
    end.

flagged_magic() -> 42.

flagged_parameters(A, B, C, D, E, F) ->
    {A, B, C, D, E, F}.

flagged_boolean() -> configure(true).

branching(Value) ->
    case Value of
        first -> 10;
        second -> 20;
        third -> 30;
        _ -> 40
    end.

recovery(Value) ->
    try parse(Value) of
        {ok, Parsed} -> Parsed
    catch
        error:Reason -> log(Reason), error
    after
        cleanup()
    end.

anonymous() ->
    Transform = fun(Value) ->
        case Value of
            zero -> 0;
            _ -> 1
        end
    end,
    Transform(first).
