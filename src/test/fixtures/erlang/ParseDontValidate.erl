-module(parse_dont_validate).

flagged_empty_case(Items) ->
    case Items of
        [] -> error(empty);
        _ -> Items
    end.

flagged_positive_if(Amount) ->
    if
        Amount =< 0 -> erlang:error(nonpositive);
        true -> Amount
    end.

flagged_predicate_case(Value) ->
    case is_integer(Value) of
        false -> throw(badarg);
        true -> Value
    end.

flagged_error_two_args(Value) ->
    case Value of
        undefined -> erlang:error(badarg, [Value]);
        _ -> Value
    end.

flagged_boolean_success(Value) ->
    case Value of
        undefined -> exit(badarg);
        _ -> true
    end.

clean_constructed(Items) ->
    case Items of
        [] -> error(empty);
        [Head | Tail] -> {Head, Tail}
    end.

clean_transformed(Items) ->
    case Items of
        [] -> error(empty);
        _ -> lists:reverse(Items)
    end.

clean_boolean_query(Value) ->
    case is_integer(Value) of
        false -> false;
        true -> true
    end.

clean_if_query(Value) ->
    if
        Value > 0 -> false;
        true -> true
    end.

clean_other_input(Value, Limit) ->
    if
        Limit =< 0 -> error(nonpositive);
        true -> Value
    end.

clean_intervening_work(Items) ->
    case Items of
        [] -> log(empty), error(empty);
        _ -> Items
    end.

clean_exit_signal(Value) ->
    case Value of
        undefined -> exit(self(), badarg);
        _ -> Value
    end.

clean_other_module(Value) ->
    case Value of
        undefined -> custom:error(badarg);
        _ -> Value
    end.

clean_head_guard(Value) when is_integer(Value) ->
    case Value of
        0 -> error(zero);
        _ -> Value
    end.

clean_cross_clause([]) -> error(empty);
clean_cross_clause(Items) -> Items.
