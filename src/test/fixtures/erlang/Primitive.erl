-module(primitive).

clean_dispatch(Mode) ->
    Mode =:= "open" orelse Mode =:= "closed".

flagged_dispatch(Mode) ->
    Mode =:= "open" orelse
    Mode =:= "closed" orelse
    Mode =:= "pending".

validation_query(Value) -> is_integer(Value).
