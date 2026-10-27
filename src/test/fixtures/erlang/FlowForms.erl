-module(flow_forms).

choose(X) ->
    if
        X > 0 -> positive;
        true -> other
    end.

wait() ->
    receive
        {ok, Value} -> Value;
        stop -> stopped
    after 1000 ->
        timeout
    end.
