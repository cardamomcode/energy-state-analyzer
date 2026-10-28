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

flagged_rejection_comment(Value) ->
    case Value of
        undefined ->
            % The rejection is still the only expression.
            error(badarg);
        _ -> Value
    end.

flagged_success_comment(Value) ->
    case Value of
        undefined -> error(badarg);
        _ ->
            % The unchanged value is still the only expression.
            Value
    end.

flagged_throw_argument_comment(Value) ->
    case Value of
        undefined -> throw(
            % The call still has one argument.
            badarg
        );
        _ -> Value
    end.

flagged_if_comments(Value) ->
    if
        Value =< 0 ->
            % The guard still rejects.
            error(badarg);
        true ->
            % The fallback still returns the input.
            Value
    end.

flagged_function_body_comment(Value) ->
    % The case is still the only function-body expression.
    case Value of
        undefined -> error(badarg);
        _ -> Value
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

clean_commented_intervening_work(Items) ->
    case Items of
        [] ->
            log(empty),
            % Comments do not hide the second executable expression.
            error(empty);
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
