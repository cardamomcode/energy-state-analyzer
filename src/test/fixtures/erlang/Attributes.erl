-module(attributes).
-export([work/6]).
-include_lib("stdlib/include/ms_transform.hrl").
-define(LIMIT, 42).

work(A, B, C, D, E, F) ->
    {A, B, C, D, E, F, ?LIMIT}.
