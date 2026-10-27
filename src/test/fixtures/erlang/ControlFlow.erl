-module(control_flow).

clean_branching(X) ->
    case X of
        ready -> ok;
        _ -> error
    end.

incomplete_branching(X) ->
    case X of
        ready -> ok
    end.

flagged_branching(X) ->
    case X of
        a -> a;
        b -> b;
        c -> c;
        d -> d;
        e -> e;
        f -> f;
        g -> g;
        h -> h;
        i -> i;
        j -> j;
        k -> k;
        _ -> unknown
    end.

deep_nesting(X) ->
    case X of
        a -> case X of
            b -> case X of
                c -> case X of
                    d -> case X of
                        e -> case X of
                            f -> ok;
                            _ -> error
                        end;
                        _ -> error
                    end;
                    _ -> error
                end;
                _ -> error
            end;
            _ -> error
        end;
        _ -> error
    end.

clean_strings(X) -> X =:= "idle".

flagged_strings(X) ->
    X =:= "ready" orelse X =:= "ready".
