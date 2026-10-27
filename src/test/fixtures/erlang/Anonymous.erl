-module(anonymous).

clean_anon() ->
    fun(X) -> X end.

many_args() ->
    fun(A, B, C, D, E, F) -> {A, B, C, D, E, F} end.

branchy_anon() ->
    fun(X) ->
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
        end
    end.

deep_anon() ->
    fun(X) ->
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
        end
    end.
